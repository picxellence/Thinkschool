using Microsoft.Extensions.Logging;

namespace Quotes.Tests.Unit.TestSupport;

// Minimal in-memory ILoggerProvider so a test can inspect what was actually logged,
// without pulling in a mocking library just to assert on ILogger - same rationale as
// Quotes.Tests.Integration/CapturingSink.cs, adapted for Microsoft.Extensions.Logging
// instead of Serilog (this runs in a hand-built ServiceCollection, not a full host).
public class CapturingLoggerProvider : ILoggerProvider
{
    private readonly List<(LogLevel Level, string Message)> _entries = new();
    private readonly object _gate = new();

    public IReadOnlyList<(LogLevel Level, string Message)> Entries
    {
        get { lock (_gate) { return _entries.ToList(); } }
    }

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

    public void Dispose() { }

    private void Add(LogLevel level, string message)
    {
        lock (_gate) { _entries.Add((level, message)); }
    }

    private sealed class CapturingLogger : ILogger
    {
        private readonly CapturingLoggerProvider _owner;

        public CapturingLogger(CapturingLoggerProvider owner) => _owner = owner;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            _owner.Add(logLevel, formatter(state, exception));
    }
}
