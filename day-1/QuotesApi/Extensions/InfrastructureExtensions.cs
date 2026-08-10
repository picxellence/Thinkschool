using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;
using QuotesApi.Repositories;

namespace QuotesApi.Extensions;

public static class InfrastructureExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<QuotesDbContext>(options =>
            options.UseSqlite(config.GetConnectionString("Default") ?? "Data Source=quotes.db"));

        services.AddScoped<IQuoteRepository, QuoteRepository>();
        services.AddScoped<ICollectionRepository, CollectionRepository>();

        return services;
    }
}