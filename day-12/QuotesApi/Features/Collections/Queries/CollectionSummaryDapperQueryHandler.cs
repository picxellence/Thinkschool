using System.Data;
using Dapper;
using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;

namespace QuotesApi.Features.Collections.Queries;

// Same DTO and same database as CollectionSummaryQueryHandler, but the SQL is
// hand-written and run through Dapper on the DbContext's own connection - for
// comparing the EF projection against a raw-SQL read path.
//
// Two small queries (header, then the CollectionItem/Quote join) rather than
// a multi-mapping: a single header row plus a flat list of quote texts is
// simpler to reason about than splitting one result set on a key column.
public class CollectionSummaryDapperQueryHandler
{
    private const string HeaderSql = """
        SELECT "Id", "Name", "OwnerUserId"
        FROM "Collections"
        WHERE "Id" = @CollectionId
        """;

    private const string QuoteTextsSql = """
        SELECT q."Text"
        FROM "CollectionItems" ci
        INNER JOIN "Quotes" q ON q."Id" = ci."QuoteId"
        WHERE ci."CollectionId" = @CollectionId
        ORDER BY ci."QuoteId"
        """;

    private readonly QuotesDbContext _context;

    public CollectionSummaryDapperQueryHandler(QuotesDbContext context)
    {
        _context = context;
    }

    public async Task<CollectionSummaryDto?> HandleAsync(int collectionId, CancellationToken ct)
    {
        var connection = _context.Database.GetDbConnection();
        var wasClosed = connection.State != ConnectionState.Open;
        if (wasClosed)
            await connection.OpenAsync(ct);

        try
        {
            var header = await connection.QueryFirstOrDefaultAsync<CollectionHeader>(
                new CommandDefinition(HeaderSql, new { CollectionId = collectionId }, cancellationToken: ct));

            if (header is null)
                return null;

            var quoteTexts = (await connection.QueryAsync<string>(
                new CommandDefinition(QuoteTextsSql, new { CollectionId = collectionId }, cancellationToken: ct))).ToList();

            return new CollectionSummaryDto(header.Id, header.Name, header.OwnerUserId, quoteTexts.Count, quoteTexts);
        }
        finally
        {
            if (wasClosed)
                await connection.CloseAsync();
        }
    }

    // A mutable class rather than a record: SQLite returns INTEGER columns as
    // Int64, and Dapper only applies its numeric-widening conversions through
    // property setters, not through constructor-parameter matching (which
    // requires an exact type match and would fail against `int Id` here).
    private sealed class CollectionHeader
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? OwnerUserId { get; set; }
    }
}
