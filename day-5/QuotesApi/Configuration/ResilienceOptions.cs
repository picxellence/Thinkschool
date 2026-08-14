namespace QuotesApi.Configuration;

public record ResilienceOptions
{
    public const string SectionName = "Resilience";

    // The brief's three numbers.
    public int RetryAttempts { get; init; } = 3;
    public double CircuitBreakerFailureRatio { get; init; } = 0.5;
    public TimeSpan CircuitBreakerSamplingDuration { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan TotalTimeout { get; init; } = TimeSpan.FromSeconds(10);

    // Polly requires these but the brief didn't specify them; kept configurable
    // rather than hardcoded, with production-sensible defaults. RetryBaseDelay
    // in particular needs to be overridable so tests can run the real exponential
    // backoff without a multi-second test.
    public TimeSpan RetryBaseDelay { get; init; } = TimeSpan.FromMilliseconds(200);
    public int CircuitBreakerMinimumThroughput { get; init; } = 10;
    public TimeSpan CircuitBreakerBreakDuration { get; init; } = TimeSpan.FromSeconds(30);
}
