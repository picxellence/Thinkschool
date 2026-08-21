using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;
using QuotesApi.Extensions;
using QuotesApi.HealthChecks;
using QuotesApi.Middleware;
using QuotesApi.Models;
using Serilog;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.ConfigureSerilog();
builder.ConfigureTracing();

builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddApiAuthentication(builder.Configuration);
builder.Services.AddRandomQuoteClient(builder.Configuration);
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("database");

var app = builder.Build();

// Correlation goes outermost so every log line - including from ExceptionMiddleware
// and the request-logging middleware - carries the request's trace id.
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();

// Exception handling wraps the auth middleware.
app.UseMiddleware<ExceptionMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

// Container/orchestrator probe - no bearer token available, so this must stay anonymous.
app.MapHealthChecks("/health").AllowAnonymous();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<QuotesDbContext>();
    db.Database.Migrate();

    // Seeded credentials must never be created in a deployed environment.
    if (app.Environment.IsDevelopment() && !db.Users.Any())
    {
        db.Users.Add(User.Create("test@example.com", "Password123!"));
        db.SaveChanges();
    }

    // Profiling exercise fixture: enough volume that GET /api/authors/slow's N+1
    // and unindexed sort are measurably slow. Development only.
    if (app.Environment.IsDevelopment() && db.Quotes.Count() < 5000)
    {
        const int authorCount = 200;
        const int quotesPerAuthor = 25;
        var random = new Random(20260821);
        var quotes = new List<Quote>(authorCount * quotesPerAuthor);

        for (var a = 0; a < authorCount; a++)
        {
            var author = $"Seed Author {a + 1}";
            for (var q = 0; q < quotesPerAuthor; q++)
            {
                var words = Enumerable.Range(0, 12 + random.Next(20))
                    .Select(_ => new string((char)('a' + random.Next(26)), 3 + random.Next(6)));
                quotes.Add(new Quote
                {
                    Author = author,
                    Text = $"Quote {q + 1} by {author}: {string.Join(' ', words)}"
                });
            }
        }

        db.Quotes.AddRange(quotes);
        db.SaveChanges();
    }
}

app.MapQuoteEndpoints();

// Proof endpoint for the Day 3 exercise: reports which scheme validated the
// request, so one curl distinguishes an internal token from an Entra one.
app.MapGet("/api/auth/whoami", (ClaimsPrincipal user) => Results.Ok(new
{
    validatedBy = user.Identity?.AuthenticationType,
    subject = user.FindFirst("oid")?.Value ?? user.FindFirst("sub")?.Value,
    name = user.Identity?.Name,
    scopes = user.FindFirst("scp")?.Value
}))
.RequireAuthorization();

app.Run();

// Required for WebApplicationFactory<Program> in QuotesApi.Tests.
public partial class Program { }