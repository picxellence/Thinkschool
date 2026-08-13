using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;
using QuotesApi.Extensions;
using QuotesApi.Middleware;
using QuotesApi.Models;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddApiAuthentication(builder.Configuration);

var app = builder.Build();

// Exception handling goes outermost so it also wraps the auth middleware.
app.UseMiddleware<ExceptionMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

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