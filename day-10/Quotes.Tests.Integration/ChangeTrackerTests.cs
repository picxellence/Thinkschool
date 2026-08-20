using System.Globalization;
using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;
using QuotesApi.Models;
using Xunit.Abstractions;

namespace Quotes.Tests.Integration;

// Measures EF Core's identity map and change-tracking overhead against a SQLite
// database seeded once with 10,000 rows. Each test opens its own DbContext so no
// tracked state or connection leaks between measurements.
public class ChangeTrackerTests : IDisposable
{
    private const int SeedCount = 10_000;

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"quotesapi-changetracker-tests-{Guid.NewGuid():N}.db");
    private readonly ITestOutputHelper _output;

    public ChangeTrackerTests(ITestOutputHelper output)
    {
        _output = output;

        using var context = CreateContext();
        context.Database.Migrate();

        var quotes = Enumerable.Range(1, SeedCount).Select(i => new Quote
        {
            Author = $"Author {i}",
            Text = $"Quote text number {i}",
        });
        context.Quotes.AddRange(quotes);
        context.SaveChanges();
    }

    public void Dispose()
    {
        foreach (var path in new[] { _dbPath, $"{_dbPath}-wal", $"{_dbPath}-shm" })
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private QuotesDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<QuotesDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;
        return new QuotesDbContext(options);
    }

    [Fact]
    public void TrackedQuery_SameId_ReturnsSameInstance()
    {
        using var context = CreateContext();

        var first = context.Quotes.Single(q => q.Id == 1);
        var second = context.Quotes.Single(q => q.Id == 1);

        Assert.True(ReferenceEquals(first, second));
    }

    [Fact]
    public void NoTrackingQuery_SameId_ReturnsDistinctInstances()
    {
        using var context = CreateContext();

        var first = context.Quotes.AsNoTracking().Single(q => q.Id == 1);
        var second = context.Quotes.AsNoTracking().Single(q => q.Id == 1);

        Assert.False(ReferenceEquals(first, second));
    }

    [Fact]
    public void TrackedQuery_PopulatesChangeTracker_NoTrackingDoesNot()
    {
        using (var trackedContext = CreateContext())
        {
            trackedContext.Quotes.Take(100).ToList();

            Assert.Equal(100, trackedContext.ChangeTracker.Entries().Count());
        }

        using (var noTrackingContext = CreateContext())
        {
            noTrackingContext.Quotes.AsNoTracking().Take(100).ToList();

            Assert.Empty(noTrackingContext.ChangeTracker.Entries());
        }
    }

    [Fact]
    public void FullTableRead_NoTracking_AllocatesLessAndIsNotSlower()
    {
        var (trackedMs, trackedBytes) = Measure(tracking: true);
        var (noTrackingMs, noTrackingBytes) = Measure(tracking: false);

        _output.WriteLine($"Tracked:       {trackedMs} ms, {trackedBytes.ToString("N0", CultureInfo.InvariantCulture)} bytes allocated");
        _output.WriteLine($"AsNoTracking:  {noTrackingMs} ms, {noTrackingBytes.ToString("N0", CultureInfo.InvariantCulture)} bytes allocated");
        _output.WriteLine($"Allocation ratio (tracked / no-tracking): {((double)trackedBytes / noTrackingBytes).ToString("F2", CultureInfo.InvariantCulture)}x");

        Assert.True(noTrackingBytes < trackedBytes,
            $"Expected AsNoTracking ({noTrackingBytes:N0} bytes) to allocate less than tracked ({trackedBytes:N0} bytes).");
        Assert.True(noTrackingMs <= trackedMs,
            $"Expected AsNoTracking ({noTrackingMs} ms) to not be slower than tracked ({trackedMs} ms).");
    }

    private (long ElapsedMs, long AllocatedBytes) Measure(bool tracking)
    {
        using (var warmup = CreateContext())
        {
            var warmupQuery = tracking ? warmup.Quotes : warmup.Quotes.AsNoTracking();
            warmupQuery.ToList();
        }

        using var context = CreateContext();
        var query = tracking ? context.Quotes : context.Quotes.AsNoTracking();

        var before = GC.GetAllocatedBytesForCurrentThread();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var results = query.ToList();

        stopwatch.Stop();
        var after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(SeedCount, results.Count);

        return (stopwatch.ElapsedMilliseconds, after - before);
    }
}
