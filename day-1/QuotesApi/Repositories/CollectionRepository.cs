using Microsoft.EntityFrameworkCore;
using QuotesApi.Data;
using QuotesApi.Models;

namespace QuotesApi.Repositories;

public class CollectionRepository : ICollectionRepository
{
    private readonly QuotesDbContext _context;

    public CollectionRepository(QuotesDbContext context)
    {
        _context = context;
    }

    public async Task<Collection?> GetByIdAsync(int id, CancellationToken ct)
    {
        return await _context.Collections
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    public async Task<Collection> AddAsync(Collection collection, CancellationToken ct)
    {
        _context.Collections.Add(collection);
        await _context.SaveChangesAsync(ct);
        return collection;
    }

    public async Task<Collection> UpdateAsync(Collection collection, CancellationToken ct)
    {
        _context.Collections.Update(collection);
        await _context.SaveChangesAsync(ct);
        return collection;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct)
    {
        var collection = await _context.Collections.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (collection is null)
            return false;

        _context.Collections.Remove(collection);
        await _context.SaveChangesAsync(ct);
        return true;
    }
}
