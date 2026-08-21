using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;
using QuotesApi.Repositories;
using QuotesApi.Services; 

namespace QuotesApi.Extensions;

public static class InfrastructureExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config, IHostEnvironment environment)
    {
        services.AddDbContext<QuotesDbContext>(options =>
        {
            options.UseSqlite(config.GetConnectionString("Default") ?? "Data Source=quotes.db");

            // Profiling exercise: surface every generated SQL statement on stdout so
            // the N+1 in GET /api/authors/slow is visible while the endpoint runs.
            // Development only - never emit raw SQL to logs outside local dev.
            if (environment.IsDevelopment())
            {
                options.LogTo(Console.WriteLine, LogLevel.Information);
            }
        });

        services.AddScoped<IQuoteRepository, QuoteRepository>();
        services.AddScoped<ICollectionRepository, CollectionRepository>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();

        return services;
    }
}