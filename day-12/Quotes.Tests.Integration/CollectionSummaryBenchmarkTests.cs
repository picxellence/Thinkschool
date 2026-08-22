using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;
using QuotesApi.Features.Collections.Queries;
using QuotesApi.Models;
using Xunit.Abstractions;

namespace Quotes.Tests.Integration;

// Measures the EF projection against the hand-written Dapper query on the
// same database - not asserting a winner, just reporting numbers so the
// comparison is there for a human to read.
public class CollectionSummaryBenchmarkTests : IDisposable
{
    private const int ItemCount = 50;
    private const int Iterations = 1000;

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"quotesapi-summary-benchmark-{Guid.NewGuid():N}.db");
    private readonly QuotesDbContext _context;
    private readonly ITestOutputHelper _output;

    public CollectionSummaryBenchmarkTests(ITestOutputHelper output)
    {
        _output = output;

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

    private async Task<int> SeedCollectionAsync()
    {
        var quotes = Enumerable.Range(1, ItemCount)
            .Select(i => new Quote { Author = $"Benchmark Author {i}", Text = $"Benchmark quote number {i}" })
            .ToList();
        _context.Quotes.AddRange(quotes);
        await _context.SaveChangesAsync();

        var collection = new Collection("Benchmark Collection", ownerId: 0, ownerUserId: "benchmark-owner");
        for (var i = 0; i < quotes.Count; i++)
            collection.AddItem(quotes[i].Id, DateTimeOffset.UtcNow.AddSeconds(i));

        _context.Collections.Add(collection);
        await _context.SaveChangesAsync();

        return collection.Id;
    }

    [Fact]
    public async Task EfAndDapper_ProduceIdenticalResults_AndAreBothTimed()
    {
        var collectionId = await SeedCollectionAsync();

        var efHandler = new CollectionSummaryQueryHandler(_context);
        var dapperHandler = new CollectionSummaryDapperQueryHandler(_context);

        // Warm-up: JIT, EF query-plan caching, first connection open - discarded
        // so the timed loop below measures steady-state calls only.
        var efWarmup = await efHandler.HandleAsync(collectionId, CancellationToken.None);
        var dapperWarmup = await dapperHandler.HandleAsync(collectionId, CancellationToken.None);
        AssertSameDto(efWarmup, dapperWarmup);

        var efStopwatch = Stopwatch.StartNew();
        CollectionSummaryDto? efResult = null;
        for (var i = 0; i < Iterations; i++)
            efResult = await efHandler.HandleAsync(collectionId, CancellationToken.None);
        efStopwatch.Stop();

        var dapperStopwatch = Stopwatch.StartNew();
        CollectionSummaryDto? dapperResult = null;
        for (var i = 0; i < Iterations; i++)
            dapperResult = await dapperHandler.HandleAsync(collectionId, CancellationToken.None);
        dapperStopwatch.Stop();

        AssertSameDto(efResult, dapperResult);

        var efTotalMs = efStopwatch.Elapsed.TotalMilliseconds;
        var dapperTotalMs = dapperStopwatch.Elapsed.TotalMilliseconds;
        var efAverageMs = efTotalMs / Iterations;
        var dapperAverageMs = dapperTotalMs / Iterations;

        _output.WriteLine($"Items per collection: {ItemCount}, iterations: {Iterations}");
        _output.WriteLine($"EF     - total: {efTotalMs:F2} ms, average: {efAverageMs:F4} ms/call");
        _output.WriteLine($"Dapper - total: {dapperTotalMs:F2} ms, average: {dapperAverageMs:F4} ms/call");
    }

    private static void AssertSameDto(CollectionSummaryDto? expected, CollectionSummaryDto? actual)
    {
        Assert.NotNull(expected);
        Assert.NotNull(actual);
        Assert.Equal(expected!.Id, actual!.Id);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Owner, actual.Owner);
        Assert.Equal(expected.ItemCount, actual.ItemCount);
        Assert.Equal(expected.QuoteTexts, actual.QuoteTexts);
    }
}
