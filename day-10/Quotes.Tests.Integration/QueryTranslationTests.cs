using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuotesApi.Data;
using QuotesApi.Models;
using Xunit.Abstractions;

namespace Quotes.Tests.Integration;

public record QuoteDto
{
    public string Author { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
}

// Seeds a SQLite-backed QuotesDbContext with a few hundred rows and inspects the SQL
// EF Core actually sends, captured via LogTo + EnableSensitiveDataLogging, to make the
// cost of full-entity materialization, the payoff of projecting, and the sharp edge of
// client-side evaluation all visible rather than assumed.
public class QueryTranslationTests : IDisposable
{
    private static readonly string[] ShortAuthors = { "Ada", "Kant", "Rumi", "Hugo" };
    private const int GeneratedCount = 300;

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"quotesapi-querytranslation-tests-{Guid.NewGuid():N}.db");
    private readonly ITestOutputHelper _output;

    public QueryTranslationTests(ITestOutputHelper output)
    {
        _output = output;

        using var context = CreateContext(logSql: false, log: null);
        context.Database.Migrate();

        var generated = Enumerable.Range(1, GeneratedCount).Select(i => new Quote
        {
            Author = $"Author Number {i}",
            Text = $"Quote body text for generated entry {i}.",
            CreatedByUserId = i % 3 == 0 ? $"user-{i}" : null,
        });
        var namedShortAuthors = ShortAuthors.Select(author => new Quote
        {
            Author = author,
            Text = $"A quote attributed to {author}.",
        });

        context.Quotes.AddRange(generated);
        context.Quotes.AddRange(namedShortAuthors);
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

    private QuotesDbContext CreateContext(bool logSql, List<string>? log)
    {
        var builder = new DbContextOptionsBuilder<QuotesDbContext>()
            .UseSqlite($"Data Source={_dbPath}");

        if (logSql)
        {
            builder
                .LogTo(line =>
                {
                    log?.Add(line);
                    _output.WriteLine(line);
                }, LogLevel.Information)
                .EnableSensitiveDataLogging();
        }

        return new QuotesDbContext(builder.Options);
    }

    private static bool IsShortAuthorName(string author) => author.Length < 8;

    [Fact]
    public void WholeEntityQuery_SelectsEveryMappedColumn()
    {
        var log = new List<string>();
        using var context = CreateContext(logSql: true, log);

        var results = context.Quotes.Where(q => q.Author == "Ada").ToList();

        Assert.Single(results);

        var sql = string.Join(Environment.NewLine, log);
        Assert.Contains("\"Id\"", sql);
        Assert.Contains("\"Author\"", sql);
        Assert.Contains("\"Text\"", sql);
        Assert.Contains("\"CreatedByUserId\"", sql);
    }

    [Fact]
    public void ProjectionQuery_SelectsOnlyRequestedColumns()
    {
        var log = new List<string>();
        using var context = CreateContext(logSql: true, log);

        var results = context.Quotes
            .Where(q => q.Author == "Ada")
            .Select(q => new QuoteDto { Author = q.Author, Text = q.Text })
            .ToList();

        Assert.Single(results);
        Assert.Equal("Ada", results[0].Author);

        var sql = string.Join(Environment.NewLine, log);
        Assert.Contains("\"Author\"", sql);
        Assert.Contains("\"Text\"", sql);
        Assert.DoesNotContain("\"Id\"", sql);
        Assert.DoesNotContain("CreatedByUserId", sql);
    }

    [Fact]
    public void ClientSideEvaluation_ThrowsThenFixedVersionsWork()
    {
        var log = new List<string>();
        using var context = CreateContext(logSql: true, log);

        var ex = Assert.Throws<InvalidOperationException>(
            () => context.Quotes.Where(q => IsShortAuthorName(q.Author)).ToList());
        _output.WriteLine($"Untranslatable query threw: {ex.Message}");
        Assert.Contains("translat", ex.Message, StringComparison.OrdinalIgnoreCase);

        // Fix: express the same predicate in a form EF can push into SQL.
        var translatable = context.Quotes.Where(q => q.Author.Length < 8).ToList();
        Assert.Equal(
            ShortAuthors.OrderBy(a => a, StringComparer.Ordinal),
            translatable.Select(q => q.Author).OrderBy(a => a, StringComparer.Ordinal));

        // Alternative fix: opt into client evaluation explicitly via AsEnumerable(), so it's
        // a deliberate choice (full table pulled into memory first) rather than a surprise throw.
        var deliberateClientEval = context.Quotes.AsEnumerable().Where(q => IsShortAuthorName(q.Author)).ToList();
        Assert.Equal(
            ShortAuthors.OrderBy(a => a, StringComparer.Ordinal),
            deliberateClientEval.Select(q => q.Author).OrderBy(a => a, StringComparer.Ordinal));
    }
}
