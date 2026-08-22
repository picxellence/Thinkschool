using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;
using QuotesApi.Features.Collections.Commands;
using QuotesApi.Features.Collections.Queries;
using QuotesApi.Repositories;
using QuotesApi.Services;

namespace QuotesApi.Extensions;

public static class InfrastructureExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<QuotesDbContext>(options =>
            options.UseSqlite(config.GetConnectionString("Default") ?? "Data Source=quotes.db"));

        services.AddScoped<IQuoteRepository, QuoteRepository>();
        services.AddScoped<ICollectionRepository, CollectionRepository>();
        services.AddScoped<CreateCollectionCommandHandler>();
        services.AddScoped<CollectionSummaryQueryHandler>();
        services.AddScoped<CollectionSummaryDapperQueryHandler>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();

        return services;
    }
}