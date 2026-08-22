using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuotesApi.Data;
using QuotesApi.Features.Collections.Queries;
using QuotesApi.Models;

namespace Quotes.Tests.Integration;

// Exercises the query handler directly against a real SQLite file, with SQL
// logging enabled, so the "single query" claim can be verified rather than
// assumed.
public class CollectionSummaryQueryHandlerTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"quotesapi-summary-tests-{Guid.NewGuid():N}.db");
    private readonly List<string> _log = new();
    private readonly QuotesDbContext _context;

    public CollectionSummaryQueryHandlerTests()
    {
        var options = new DbContextOptionsBuilder<QuotesDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .LogTo(message => _log.Add(message), LogLevel.Information)
            .Options;
        _context = new QuotesDbContext(options);
        _context.Database.Migrate();
    }

    public void Dispose()
    {
        _context.Dispose();

        foreach (var path in new[] { _dbPath, $"{_dbPath}-wal", $"{_dbPath}-shm" })
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private async Task<Collection> SeedCollectionWithQuotesAsync()
    {
        var quote1 = new Quote { Author = "Author A", Text = "First Quote" };
        var quote2 = new Quote { Author = "Author B", Text = "Second Quote" };
        _context.Quotes.AddRange(quote1, quote2);
        await _context.SaveChangesAsync();

        var collection = new Collection("Reading List", ownerId: 0, ownerUserId: "owner-1");
        collection.AddItem(quote1.Id, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        collection.AddItem(quote2.Id, new DateTimeOffset(2026, 1, 1, 0, 0, 1, TimeSpan.Zero));
        _context.Collections.Add(collection);
        await _context.SaveChangesAsync();

        return collection;
    }

    [Fact]
    public async Task HandleAsync_ReturnsDtoShape_WithCorrectCountAndQuoteTexts()
    {
        var collection = await SeedCollectionWithQuotesAsync();
        var handler = new CollectionSummaryQueryHandler(_context);

        var dto = await handler.HandleAsync(collection.Id, CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal(collection.Id, dto!.Id);
        Assert.Equal("Reading List", dto.Name);
        Assert.Equal("owner-1", dto.Owner);
        Assert.Equal(2, dto.ItemCount);
        Assert.Equal(new[] { "First Quote", "Second Quote" }, dto.QuoteTexts);
    }

    [Fact]
    public async Task HandleAsync_CollectionNotFound_ReturnsNull()
    {
        var handler = new CollectionSummaryQueryHandler(_context);

        var dto = await handler.HandleAsync(999999, CancellationToken.None);

        Assert.Null(dto);
    }

    [Fact]
    public async Task HandleAsync_EmitsExactlyOneSqlStatement()
    {
        var collection = await SeedCollectionWithQuotesAsync();
        _log.Clear();
        var handler = new CollectionSummaryQueryHandler(_context);

        await handler.HandleAsync(collection.Id, CancellationToken.None);

        var executedCommands = _log.Count(line => line.Contains("Executed DbCommand"));
        Assert.Equal(1, executedCommands);
    }

    [Fact]
    public async Task HandleAsync_DoesNotAttachEntitiesToChangeTracker()
    {
        var collection = await SeedCollectionWithQuotesAsync();
        _context.ChangeTracker.Clear();
        var handler = new CollectionSummaryQueryHandler(_context);

        await handler.HandleAsync(collection.Id, CancellationToken.None);

        // AsNoTracking means the projection materialized zero tracked entries -
        // proof this is a read model, not the Collection domain entity reloaded.
        Assert.Empty(_context.ChangeTracker.Entries());
    }
}
