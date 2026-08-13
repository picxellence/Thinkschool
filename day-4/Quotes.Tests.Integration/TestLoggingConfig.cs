using System.Runtime.CompilerServices;

namespace Quotes.Tests.Integration;

// Every test factory in this project boots a real host, and each host runs Serilog
// via QuotesApi's own appsettings.json/appsettings.Development.json (EF Command
// logging is Debug in Development). With 39+ hosts booted across the run, unfiltered
// console output would flood the test log. This sets the floor to Warning for the
// whole test process before any factory's WebApplication.CreateBuilder(args) runs -
// env vars are read into configuration synchronously at builder-creation time, so a
// module initializer (which fires once, on assembly load, before any test executes)
// reaches those reads reliably. Individual factories (see LoggingTestFactory) can
// still opt back into a lower threshold for a single host via their own overrides.
internal static class TestLoggingConfig
{
    [ModuleInitializer]
    public static void Initialize()
    {
        Environment.SetEnvironmentVariable("Serilog__MinimumLevel__Default", "Warning");
        Environment.SetEnvironmentVariable("Serilog__MinimumLevel__Override__Microsoft.AspNetCore", "Warning");
        Environment.SetEnvironmentVariable("Serilog__MinimumLevel__Override__QuotesApi", "Warning");
        Environment.SetEnvironmentVariable("Serilog__MinimumLevel__Override__Microsoft.EntityFrameworkCore.Database.Command", "Warning");
    }
}
