using LegacyApi.Models;

namespace LegacyApi.Repositories;

public interface IOrderRepository
{
    Task<Customer?> GetCustomerAsync(int id, CancellationToken ct);

    Task<Order> SaveOrderAsync(
        Order order,
        List<OrderItem> items,
        AuditLog audit,
        Invoice? invoice,
        CancellationToken ct);
}