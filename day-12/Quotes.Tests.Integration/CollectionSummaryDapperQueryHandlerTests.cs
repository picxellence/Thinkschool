using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;
using QuotesApi.Features.Collections.Queries;
using QuotesApi.Models;

namespace Quotes.Tests.Integration;

public class CollectionSummaryDapperQueryHandlerTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"quotesapi-summary-dapper-tests-{Guid.NewGuid():N}.db");
    private readonly QuotesDbContext _context;

    public CollectionSummaryDapperQueryHandlerTests()
    {
        var options = new DbContextOptionsBuilder<QuotesDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
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
        var handler = new CollectionSummaryDapperQueryHandler(_context);

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
        var handler = new CollectionSummaryDapperQueryHandler(_context);

        var dto = await handler.HandleAsync(999999, CancellationToken.None);

        Assert.Null(dto);
    }

    [Fact]
    public async Task HandleAsync_CollectionWithNoItems_ReturnsEmptyQuoteTexts()
    {
        var collection = new Collection("Empty Collection", ownerId: 0, ownerUserId: "owner-2");
        _context.Collections.Add(collection);
        await _context.SaveChangesAsync();

        var handler = new CollectionSummaryDapperQueryHandler(_context);

        var dto = await handler.HandleAsync(collection.Id, CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal(0, dto!.ItemCount);
        Assert.Empty(dto.QuoteTexts);
    }

    [Fact]
    public async Task HandleAsync_MatchesEfHandlerResult()
    {
        var collection = await SeedCollectionWithQuotesAsync();
        var efHandler = new CollectionSummaryQueryHandler(_context);
        var dapperHandler = new CollectionSummaryDapperQueryHandler(_context);

        var efDto = await efHandler.HandleAsync(collection.Id, CancellationToken.None);
        var dapperDto = await dapperHandler.HandleAsync(collection.Id, CancellationToken.None);

        // CollectionSummaryDto is a record, but List<T> has reference equality,
        // so a whole-record Assert.Equal would spuriously fail here even when
        // the contents match - compare field by field instead.
        Assert.NotNull(efDto);
        Assert.NotNull(dapperDto);
        Assert.Equal(efDto!.Id, dapperDto!.Id);
        Assert.Equal(efDto.Name, dapperDto.Name);
        Assert.Equal(efDto.Owner, dapperDto.Owner);
        Assert.Equal(efDto.ItemCount, dapperDto.ItemCount);
        Assert.Equal(efDto.QuoteTexts, dapperDto.QuoteTexts);
    }
}
