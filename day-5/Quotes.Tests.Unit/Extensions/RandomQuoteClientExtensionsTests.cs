using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;
using Quotes.Tests.Unit.TestSupport;
using QuotesApi.Clients;
using QuotesApi.Extensions;

namespace Quotes.Tests.Unit.Extensions;

// Exercises the real production wiring - AddRandomQuoteClient's actual retry/circuit
// breaker/timeout pipeline - against a stub primary handler, so this is testing the
// genuine Polly behavior, not a hand-rolled substitute for it. No real network call
// is made: ConfigurePrimaryHttpMessageHandler replaces only the transport, leaving
// the resilience handler that AddRandomQuoteClient registers untouched.
public class RandomQuoteClientExtensionsTests
{
    // Real exponential backoff + jitter, just with a near-zero base delay so retries
    // don't cost multiple real seconds of wall-clock test time.
    private static Dictionary<string, string?> FastResilienceConfig() => new()
    {
        ["Resilience:RetryBaseDelay"] = "00:00:00.001"
    };

    private static ServiceProvider BuildProvider(StubHttpMessageHandler stub, CapturingLoggerProvider capturingLogger)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(FastResilienceConfig())
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(capturingLogger));
        services.AddRandomQuoteClient(config)
            .ConfigurePrimaryHttpMessageHandler(() => stub);

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task GetRandomQuoteAsync_TwoTransientFailuresThenSuccess_RetriesExactlyTwiceAndSucceeds()
    {
        var stub = new StubHttpMessageHandler(
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.OK);
        var capturingLogger = new CapturingLoggerProvider();
        using var provider = BuildProvider(stub, capturingLogger);
        var client = provider.GetRequiredService<IRandomQuoteClient>();

        var quote = await client.GetRandomQuoteAsync(CancellationToken.None);

        stub.CallCount.Should().Be(3, "two failed attempts plus the one that finally succeeded");
        quote.Author.Should().Be("Test Author");
        quote.Text.Should().Be("Test quote.");

        var retryLogs = capturingLogger.Entries.Where(e => e.Message.Contains("retry attempt")).ToList();
        retryLogs.Should().HaveCount(2, "one retry log line per failed attempt before the eventual success");
        retryLogs[0].Level.Should().Be(LogLevel.Warning);
        retryLogs[0].Message.Should().Contain("retry attempt 1 of 3").And.Contain("HTTP 503");
        retryLogs[1].Message.Should().Contain("retry attempt 2 of 3").And.Contain("HTTP 503");
    }

    [Fact]
    public async Task GetRandomQuoteAsync_FourConsecutiveFailures_ExhaustsRetriesAndFailsFastRatherThanHanging()
    {
        var stub = new StubHttpMessageHandler(
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.ServiceUnavailable);
        var capturingLogger = new CapturingLoggerProvider();
        using var provider = BuildProvider(stub, capturingLogger);
        var client = provider.GetRequiredService<IRandomQuoteClient>();

        var callTask = client.GetRandomQuoteAsync(CancellationToken.None);
        var winner = await Task.WhenAny(callTask, Task.Delay(TimeSpan.FromSeconds(5)));

        winner.Should().BeSameAs(callTask, "the request must fail fast once retries are exhausted, not hang");

        Func<Task> act = () => callTask;
        await act.Should().ThrowAsync<HttpRequestException>();

        stub.CallCount.Should().Be(4, "the initial attempt plus all 3 configured retries, then it gives up");

        var retryLogs = capturingLogger.Entries.Where(e => e.Message.Contains("retry attempt")).ToList();
        retryLogs.Should().HaveCount(3, "a retry log line before each of the 3 retry attempts");
    }

    [Fact]
    public async Task GetRandomQuoteAsync_FailuresExceedCircuitBreakerThreshold_OpensCircuitAndLogsIt()
    {
        var stub = new StubHttpMessageHandler(
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.ServiceUnavailable);
        var capturingLogger = new CapturingLoggerProvider();

        // MinimumThroughput lowered to 2 (Polly's floor) so the circuit can evaluate
        // within a single call's retry sequence instead of needing many top-level calls.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Resilience:RetryBaseDelay"] = "00:00:00.001",
                ["Resilience:CircuitBreakerMinimumThroughput"] = "2"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(capturingLogger));
        services.AddRandomQuoteClient(config).ConfigurePrimaryHttpMessageHandler(() => stub);
        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IRandomQuoteClient>();

        // The circuit opens after the 2nd failed attempt (100% failure rate >= the 50%
        // ratio, MinimumThroughput of 2 reached), so the 3rd attempt is rejected by the
        // now-open circuit rather than retried through to the stub - hence the thrown
        // type here is BrokenCircuitException, not HttpRequestException.
        Func<Task> act = () => client.GetRandomQuoteAsync(CancellationToken.None);

        await act.Should().ThrowAsync<BrokenCircuitException>();

        var openedLogs = capturingLogger.Entries.Where(e => e.Message.Contains("circuit breaker opened")).ToList();
        openedLogs.Should().ContainSingle();
        openedLogs[0].Level.Should().Be(LogLevel.Error);
    }
}
