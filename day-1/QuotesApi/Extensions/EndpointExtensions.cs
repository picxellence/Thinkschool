using Microsoft.AspNetCore.Mvc;
using QuotesApi.Data;
using QuotesApi.Models;
using QuotesApi.Repositories;

namespace QuotesApi.Extensions;

public static class EndpointExtensions
{
    public static void MapQuoteEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/quotes");

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
        });

        group.MapDelete("/{id:int}", async (int id, IQuoteRepository repo, CancellationToken ct) =>
        {
            var deleted = await repo.DeleteAsync(id, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        });

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

        collections.MapPost("/{id:int}/items", async (int id, AddCollectionItemRequest request, ICollectionRepository repo, CancellationToken ct) =>
        {
            if (request.QuoteId <= 0)
                return Results.BadRequest(new { error = "QuoteId is required." });

            var collection = await repo.GetByIdAsync(id, ct);
            if (collection is null)
                return Results.NotFound();

            try
            {
                collection.AddItem(request.QuoteId);
                await repo.UpdateAsync(collection, ct);
                return Results.Ok(collection);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
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