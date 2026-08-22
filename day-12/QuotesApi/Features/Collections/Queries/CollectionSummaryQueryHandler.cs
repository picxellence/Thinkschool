using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;

namespace QuotesApi.Features.Collections.Queries;

// Read-only projection: Collection -> CollectionItem -> Quote in a single
// EF query, AsNoTracking. No Collection domain entity is materialized.
public class CollectionSummaryQueryHandler
{
    private readonly QuotesDbContext _context;

    public CollectionSummaryQueryHandler(QuotesDbContext context)
    {
        _context = context;
    }

    public Task<CollectionSummaryDto?> HandleAsync(int collectionId, CancellationToken ct)
    {
        return _context.Collections
            .AsNoTracking()
            .Where(c => c.Id == collectionId)
            .Select(c => new CollectionSummaryDto(
                c.Id,
                c.Name,
                c.OwnerUserId,
                c.Items.Count,
                c.Items
                    .OrderBy(i => i.QuoteId)
                    .Join(_context.Quotes, i => i.QuoteId, q => q.Id, (i, q) => q.Text)
                    .ToList()))
            .FirstOrDefaultAsync(ct);
    }
}
