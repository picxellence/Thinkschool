using Microsoft.AspNetCore.Mvc;
using QuotesApi.Data;
using QuotesApi.Models;
using QuotesApi.Repositories;
using QuotesApi.Services;
using Microsoft.EntityFrameworkCore;


namespace QuotesApi.Extensions;

public static class EndpointExtensions
{
    public static void MapQuoteEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/quotes");

        var auth = app.MapGroup("/api/auth");

        auth.MapPost("/login", async (LoginRequest request, QuotesDbContext db, IJwtTokenService tokenService, CancellationToken ct) =>
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return Results.BadRequest(new { error = "Email and password are required." });

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);

        if (user is null || !user.VerifyPassword(request.Password))
            return Results.Unauthorized();

        var accessToken = tokenService.GenerateAccessToken(user);
        var refreshToken = tokenService.GenerateRefreshToken();

        var response = new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = tokenService.AccessTokenMinutes * 60
        };

        return Results.Ok(response);
    });

        group.MapGet("", async (int page, int size, IQuoteRepository repo, CancellationToken ct) =>
        {
            if (page < 1) page = 1;
            if (size < 1) size = 10;
            var quotes = await repo.GetPagedAsync(page, size, ct);
            return Results.Ok(quotes);
        });

        group.MapGet("/{id:int}", async (int id, IQuoteRepository repo, CancellationToken ct) =>
        {
            var quote = await repo.GetByIdAsync(id, ct);
            return quote is null ? Results.NotFound() : Results.Ok(quote);
        });

        group.MapPost("", async (CreateQuoteRequest request, IQuoteRepository repo, CancellationToken ct) =>
        {
            var errors = new Dictionary<string, string[]>();
            if (string.IsNullOrWhiteSpace(request.Author))
                errors["author"] = new[] { "Author is required." };
            if (string.IsNullOrWhiteSpace(request.Text))
                errors["text"] = new[] { "Text is required." };

            if (errors.Count > 0)
                return Results.ValidationProblem(errors);

            var quote = new Quote { Author = request.Author, Text = request.Text };
            var created = await repo.AddAsync(quote, ct);
            return Results.Created($"/api/quotes/{created.Id}", created);
        }).RequireAuthorization();

        group.MapDelete("/{id:int}", async (int id, IQuoteRepository repo, CancellationToken ct) =>
        {
            var deleted = await repo.DeleteAsync(id, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        }).RequireAuthorization();

        var collections = app.MapGroup("/collections");

        collections.MapPost("", async (CreateCollectionRequest request, ICollectionRepository repo, CancellationToken ct) =>
        {
            var errors = new Dictionary<string, string[]>();
            if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length < 3 || request.Name.Length > 80)
                errors["name"] = new[] { "Name is required and must be between 3 and 80 characters." };
            if (request.OwnerId <= 0)
                errors["ownerId"] = new[] { "OwnerId is required." };

            if (errors.Count > 0)
                return Results.ValidationProblem(errors);

            try
            {
                var collection = new Collection(request.Name, request.OwnerId);
                var created = await repo.AddAsync(collection, ct);
                return Results.Created($"/collections/{created.Id}", created);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        collections.MapPost("/{id:int}/items", async (int id, AddCollectionItemRequest request, ICollectionRepository repo, IClock clock, CancellationToken ct) =>
        {
            if (request.QuoteId <= 0)
                return Results.BadRequest(new { error = "QuoteId is required." });
            var collection = await repo.GetByIdAsync(id, ct);
            if (collection is null)
                return Results.NotFound();
            try
            {
                collection.AddItem(request.QuoteId, clock.UtcNow);
                await repo.UpdateAsync(collection, ct);
                return Results.Ok(collection);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        collections.MapDelete("/{id:int}/items/{quoteId:int}", async (int id, int quoteId, ICollectionRepository repo, CancellationToken ct) =>
        {
            var collection = await repo.GetByIdAsync(id, ct);
            if (collection is null)
                return Results.NotFound();

            collection.RemoveItem(quoteId);
            await repo.UpdateAsync(collection, ct);
            return Results.NoContent();
        });
    }
}