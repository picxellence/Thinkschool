using System.Threading.Tasks;

namespace OrdersApi.Services;

public interface IOrderService
{
    Task<object> CreateOrderAsync(object request);
}