using Microsoft.Extensions.Diagnostics.HealthChecks;
using QuotesApi.Data;

namespace QuotesApi.HealthChecks;

public class DatabaseHealthCheck : IHealthCheck
{
    private readonly QuotesDbContext _context;

    public DatabaseHealthCheck(QuotesDbContext context)
    {
        _context = context;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        return await _context.Database.CanConnectAsync(cancellationToken)
            ? HealthCheckResult.Healthy("Database is reachable.")
            : HealthCheckResult.Unhealthy("Database is not reachable.");
    }
}
