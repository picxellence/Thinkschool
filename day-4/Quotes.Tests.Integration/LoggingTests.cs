using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;
using Serilog.Events;

namespace Quotes.Tests.Integration;

// Opts this one host back up from the assembly-wide Warning floor (see
// TestLoggingConfig) to Information, so UseSerilogRequestLogging's completion log
// actually reaches the CapturingSink for the assertion below.
public class LoggingTestFactory : PolicyTestFactory
{
    public CapturingSink Sink { get; } = new();

    public LoggingTestFactory() : base(new Dictionary<string, string?>
    {
        ["Serilog__MinimumLevel__Default"] = "Information"
    })
    {
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<ILogEventSink>(Sink);
        });
    }
}

[Collection("EnvironmentMutatingTests")]
public class LoggingTests : IDisposable
{
    private readonly LoggingTestFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Request_EmitsLogLine_CarryingTraceIdThatMatchesResponse()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/quotes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var traceId = response.Headers.GetValues("X-Trace-Id").Single();

        var matched = _factory.Sink.Events.Any(e =>
            e.Properties.TryGetValue("TraceId", out var value)
            && value is ScalarValue { Value: string s }
            && s == traceId);

        Assert.True(matched, "expected a captured log event whose TraceId property matches the response's X-Trace-Id header");
    }
}
