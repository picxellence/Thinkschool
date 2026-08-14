# Resilience: GET /api/quotes/random

The API previously made no outbound HTTP calls. `IRandomQuoteClient` calls a public
random-quote API and is now the thing this resilience handler protects.

## Upstream choice

Checked reachability before choosing, per the task:

```
$ curl -s -o /dev/null -w "HTTP:%{http_code}\n" https://zenquotes.io/api/random
HTTP:200

$ curl -s -o /dev/null -w "HTTP:%{http_code}\n" https://api.quotable.io/random
HTTP:000   (connection failure)
```

`api.quotable.io` is unreachable; `zenquotes.io/api/random` is used.

## Configuration (`QuotesApi/Configuration/ResilienceOptions.cs`)

Bound from the `Resilience` section, same pattern as `JwtOptions`/`EntraOptions` -
nothing hardcoded in the pipeline builder itself:

```csharp
public record ResilienceOptions
{
    public const string SectionName = "Resilience";

    // The brief's three numbers.
    public int RetryAttempts { get; init; } = 3;
    public double CircuitBreakerFailureRatio { get; init; } = 0.5;
    public TimeSpan CircuitBreakerSamplingDuration { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan TotalTimeout { get; init; } = TimeSpan.FromSeconds(10);

    // Polly requires these but the brief didn't specify them; kept configurable
    // rather than hardcoded, with production-sensible defaults.
    public TimeSpan RetryBaseDelay { get; init; } = TimeSpan.FromMilliseconds(200);
    public int CircuitBreakerMinimumThroughput { get; init; } = 10;
    public TimeSpan CircuitBreakerBreakDuration { get; init; } = TimeSpan.FromSeconds(30);
}
```

`appsettings.json`:

```json
"Resilience": {
  "RetryAttempts": 3,
  "RetryBaseDelay": "00:00:00.200",
  "CircuitBreakerFailureRatio": 0.5,
  "CircuitBreakerSamplingDuration": "00:00:30",
  "CircuitBreakerMinimumThroughput": 10,
  "CircuitBreakerBreakDuration": "00:00:30",
  "TotalTimeout": "00:00:10"
}
```

## Pipeline shape (`QuotesApi/Extensions/RandomQuoteClientExtensions.cs`)

Registered via `AddHttpClient<IRandomQuoteClient, RandomQuoteClient>()` +
`AddResilienceHandler("default", ...)`. Composition order, outermost first:

```
Total timeout (10s)
  -> Retry (3 attempts, exponential backoff + jitter)
       -> Circuit breaker (50% failure ratio / 30s sampling window)
            -> actual HTTP call
```

Total timeout is outermost so it bounds the *whole* retry sequence's wall-clock time,
not a single attempt. Retry wraps the circuit breaker (matches Microsoft's own
"standard resilience handler" ordering) so an already-open circuit stops the retry
loop immediately rather than letting it keep hammering a downed dependency.

```csharp
builder
    .AddTimeout(options.TotalTimeout)
    .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
    {
        ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
            .Handle<HttpRequestException>()
            .HandleResult(response => !response.IsSuccessStatusCode),
        MaxRetryAttempts = options.RetryAttempts,
        Delay = options.RetryBaseDelay,
        BackoffType = DelayBackoffType.Exponential,
        UseJitter = true,
        OnRetry = args =>
        {
            logger.LogWarning(
                "Random quote request retry attempt {RetryAttempt} of {MaxRetryAttempts} after outcome {Outcome}",
                args.AttemptNumber + 1, options.RetryAttempts, DescribeOutcome(args.Outcome));
            return default;
        }
    })
    .AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>
    {
        ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
            .Handle<HttpRequestException>()
            .HandleResult(response => !response.IsSuccessStatusCode),
        FailureRatio = options.CircuitBreakerFailureRatio,
        SamplingDuration = options.CircuitBreakerSamplingDuration,
        MinimumThroughput = options.CircuitBreakerMinimumThroughput,
        BreakDuration = options.CircuitBreakerBreakDuration,
        OnOpened = args => { logger.LogError(...); return default; },
        OnClosed = _  => { logger.LogInformation("...circuit breaker closed"); return default; }
    });
```

`GET /api/quotes/random` (`EndpointExtensions.cs`) is anonymous and catches
`HttpRequestException`, `TimeoutRejectedException`, and `BrokenCircuitException`
around the client call, returning **503** instead of letting any of them fall
through to the global exception middleware as a 500.

## Tests (`Quotes.Tests.Unit/Extensions/RandomQuoteClientExtensionsTests.cs`)

All three build the *real* production pipeline (`AddRandomQuoteClient`) against a
stub `DelegatingHandler` registered via `ConfigurePrimaryHttpMessageHandler` - no
real network call, but genuine Polly retry/circuit-breaker/timeout behavior, not a
hand-rolled substitute for it. `RetryBaseDelay` is overridden to 1ms via in-memory
config so the real exponential backoff doesn't cost real seconds of test time.

1. **`GetRandomQuoteAsync_TwoTransientFailuresThenSuccess_RetriesExactlyTwiceAndSucceeds`**
   Stub queue: 503, 503, 200. Asserts `stub.CallCount == 3` and the call succeeds
   with the parsed quote.
2. **`GetRandomQuoteAsync_FourConsecutiveFailures_ExhaustsRetriesAndFailsFastRatherThanHanging`**
   Stub queue: 503 x4. Races the call against a 5-second `Task.Delay` and asserts
   the call wins (proves it doesn't hang), then asserts it throws
   `HttpRequestException` with `stub.CallCount == 4` (initial + 3 retries, then gives up).
3. **`GetRandomQuoteAsync_FailuresExceedCircuitBreakerThreshold_OpensCircuitAndLogsIt`**
   Extra test (not explicitly required, added because requirement 4 mandates the
   circuit-breaker logging exist and be verified, not just present). Lowers
   `CircuitBreakerMinimumThroughput` to 2 (Polly's floor) so the breaker can evaluate
   within one call's retry sequence, and asserts the "circuit breaker opened" error
   log fires exactly once.

```
Passed Quotes.Tests.Unit.Extensions.RandomQuoteClientExtensionsTests.GetRandomQuoteAsync_FourConsecutiveFailures_ExhaustsRetriesAndFailsFastRatherThanHanging [107 ms]
Passed Quotes.Tests.Unit.Extensions.RandomQuoteClientExtensionsTests.GetRandomQuoteAsync_TwoTransientFailuresThenSuccess_RetriesExactlyTwiceAndSucceeds [21 ms]
Passed Quotes.Tests.Unit.Extensions.RandomQuoteClientExtensionsTests.GetRandomQuoteAsync_FailuresExceedCircuitBreakerThreshold_OpensCircuitAndLogsIt [8 ms]

Test Run Successful.
Total tests: 3
     Passed: 3
 Total time: 0.5796 Seconds
```

## Captured retry log output

Real console output from a run of the above three tests (`CAPTURED [...]` lines are
what `CapturingLoggerProvider` recorded via the standard `ILogger` pipeline - i.e.
exactly what would reach the console/Serilog sink in the real app). Framework-level
Polly diagnostic lines ("Execution attempt...", "Resilience event occurred...") are
included alongside this project's own log lines to show they're not silent either.

**Test 1 - two 503s then 200, retries exactly twice then succeeds:**

```
CAPTURED [Information] Start processing HTTP request GET https://zenquotes.io/api/random
CAPTURED [Information] Sending HTTP request GET https://zenquotes.io/api/random
CAPTURED [Information] Received HTTP response headers after 0.0224ms - 503
CAPTURED [Warning] Execution attempt. Source: 'IRandomQuoteClient-default//Retry', Result: '503', Handled: 'True', Attempt: '0'
CAPTURED [Warning] Resilience event occurred. EventName: 'OnRetry', Source: 'IRandomQuoteClient-default//Retry', Result: '503'
CAPTURED [Warning] Random quote request retry attempt 1 of 3 after outcome HTTP 503
CAPTURED [Information] Sending HTTP request GET https://zenquotes.io/api/random
CAPTURED [Information] Received HTTP response headers after 0.0003ms - 503
CAPTURED [Warning] Execution attempt. Source: 'IRandomQuoteClient-default//Retry', Result: '503', Handled: 'True', Attempt: '1'
CAPTURED [Warning] Resilience event occurred. EventName: 'OnRetry', Source: 'IRandomQuoteClient-default//Retry', Result: '503'
CAPTURED [Warning] Random quote request retry attempt 2 of 3 after outcome HTTP 503
CAPTURED [Information] Sending HTTP request GET https://zenquotes.io/api/random
CAPTURED [Information] Received HTTP response headers after 0.0402ms - 200
CAPTURED [Information] Execution attempt. Source: 'IRandomQuoteClient-default//Retry', Result: '200', Handled: 'False', Attempt: '2'
CAPTURED [Information] End processing HTTP request after 2.5723ms - 200
```

**Test 2 - four consecutive 503s, exhausts retries, fails fast:**

```
CAPTURED [Information] Start processing HTTP request GET https://zenquotes.io/api/random
CAPTURED [Information] Sending HTTP request GET https://zenquotes.io/api/random
CAPTURED [Information] Received HTTP response headers after 0.2766ms - 503
CAPTURED [Warning] Execution attempt. Source: 'IRandomQuoteClient-default//Retry', Result: '503', Handled: 'True', Attempt: '0', Execution Time: 7.2957ms
CAPTURED [Warning] Resilience event occurred. EventName: 'OnRetry', Source: 'IRandomQuoteClient-default//Retry', Result: '503'
CAPTURED [Warning] Random quote request retry attempt 1 of 3 after outcome HTTP 503
CAPTURED [Information] Sending HTTP request GET https://zenquotes.io/api/random
CAPTURED [Information] Received HTTP response headers after 0.0103ms - 503
CAPTURED [Warning] Execution attempt. Source: 'IRandomQuoteClient-default//Retry', Result: '503', Handled: 'True', Attempt: '1'
CAPTURED [Warning] Resilience event occurred. EventName: 'OnRetry', Source: 'IRandomQuoteClient-default//Retry', Result: '503'
CAPTURED [Warning] Random quote request retry attempt 2 of 3 after outcome HTTP 503
CAPTURED [Information] Sending HTTP request GET https://zenquotes.io/api/random
CAPTURED [Information] Received HTTP response headers after 0.0008ms - 503
CAPTURED [Warning] Execution attempt. Source: 'IRandomQuoteClient-default//Retry', Result: '503', Handled: 'True', Attempt: '2'
CAPTURED [Warning] Resilience event occurred. EventName: 'OnRetry', Source: 'IRandomQuoteClient-default//Retry', Result: '503'
CAPTURED [Warning] Random quote request retry attempt 3 of 3 after outcome HTTP 503
CAPTURED [Information] Sending HTTP request GET https://zenquotes.io/api/random
CAPTURED [Information] Received HTTP response headers after 0.0008ms - 503
CAPTURED [Error] Execution attempt. Source: 'IRandomQuoteClient-default//Retry', Result: '503', Handled: 'True', Attempt: '3'
CAPTURED [Information] End processing HTTP request after 33.1765ms - 503
```

(The client then calls `EnsureSuccessStatusCode()` on that final still-failing
response, which is what actually throws the `HttpRequestException` the test
asserts on - the resilience pipeline itself returns the last failed response
rather than throwing, once retries are exhausted.)

**Test 3 - circuit breaker opens mid-sequence (`CircuitBreakerMinimumThroughput=2` for this test only):**

```
CAPTURED [Information] Start processing HTTP request GET https://zenquotes.io/api/random
CAPTURED [Information] Sending HTTP request GET https://zenquotes.io/api/random
CAPTURED [Information] Received HTTP response headers after 0.0011ms - 503
CAPTURED [Warning] Execution attempt. Source: 'IRandomQuoteClient-default//Retry', Result: '503', Handled: 'True', Attempt: '0'
CAPTURED [Warning] Resilience event occurred. EventName: 'OnRetry', Source: 'IRandomQuoteClient-default//Retry', Result: '503'
CAPTURED [Warning] Random quote request retry attempt 1 of 3 after outcome HTTP 503
CAPTURED [Information] Sending HTTP request GET https://zenquotes.io/api/random
CAPTURED [Information] Received HTTP response headers after 0.0002ms - 503
CAPTURED [Error] Resilience event occurred. EventName: 'OnCircuitOpened', Source: 'IRandomQuoteClient-default//CircuitBreaker', Result: '503'
CAPTURED [Error] Random quote client circuit breaker opened for 00:00:30 after outcome HTTP 503
CAPTURED [Warning] Execution attempt. Source: 'IRandomQuoteClient-default//Retry', Result: '503', Handled: 'True', Attempt: '1'
CAPTURED [Warning] Resilience event occurred. EventName: 'OnRetry', Source: 'IRandomQuoteClient-default//Retry', Result: '503'
CAPTURED [Warning] Random quote request retry attempt 2 of 3 after outcome HTTP 503
CAPTURED [Information] Execution attempt. Source: 'IRandomQuoteClient-default//Retry', Result: 'The circuit is now open and is not allowing calls.', Handled: 'False', Attempt: '2'
```

Note the circuit opens after only the 2nd failed attempt (100% failure rate, and
`MinimumThroughput=2` was reached) - the retry strategy had *already* decided to
retry attempt 2 before the breaker tripped, so one more "retry attempt" log line
still appears after the "circuit breaker opened" line. The 3rd attempt is then
rejected by the now-open circuit (`BrokenCircuitException`) without ever reaching
the stub handler - the retry strategy doesn't handle that exception type, so it
gives up immediately rather than retrying against a circuit it knows is open.
