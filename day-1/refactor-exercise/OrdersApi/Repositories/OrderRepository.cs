using Microsoft.EntityFrameworkCore;
using LegacyApi.Data;
using LegacyApi.Models;

namespace LegacyApi.Repositories;

public class OrderRepository : IOrderRepository
{
    private readonly AppDbContext _db;

    public OrderRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Customer?> GetCustomerAsync(int id, CancellationToken ct)
    {
        return await _db.Customers.FirstOrDefaultAsync(c => c.Id == id, ct);
    }

    // ... other interface methods (GetProductAsync, GetInventoryAsync, GetCouponAsync, GetRecentOrdersAsync) ...

    public async Task<Order> SaveOrderAsync(Order order, List<OrderItem> items, AuditLog audit, Invoice? invoice, CancellationToken ct)
    {
        using var transaction = await _db.Database.BeginTransactionAsync(ct);

        _db.Orders.Add(order);
        await _db.SaveChangesAsync(ct);

        foreach (var item in items)
        {
            item.OrderId = order.Id;
            _db.OrderItems.Add(item);
        }

        _db.AuditLogs.Add(audit);
        if (invoice != null) _db.Invoices.Add(invoice);

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return order;
    }
}