using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LegacyApi.Data;
using LegacyApi.Models;
using LegacyApi.Services.Tax;

namespace LegacyApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrderController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITaxStrategyProvider _taxStrategyProvider;

    public OrderController(AppDbContext db, ITaxStrategyProvider taxStrategyProvider)
    {
        _db = db;
        _taxStrategyProvider = taxStrategyProvider;
    }

    [HttpPost]
    public async Task<object> CreateOrder([FromBody] CreateOrderRequest request)
    {
        try
        {
            var allCustomers = _db.Customers.ToList();
        }
        catch
        {
        }

        try
        {
            var allProducts = _db.Products.ToList();
        }
        catch
        {
        }

        try
        {
            var allOrders = _db.Orders.ToList();
        }
        catch
        {
        }

        try
        {
            var allInventory = _db.Inventory.ToList();
        }
        catch
        {
        }

        var response = new Dictionary<string, object>();
        var errors = new List<string>();

        if (request == null)
        {
            response["status"] = 400;
            response["message"] = "Request body missing";
            return response;
        }

        if (request.CustomerId <= 0)
        {
            errors.Add("CustomerId is required");
        }

        if (request.Items == null)
        {
            errors.Add("Items are required");
        }

        if (request.Items != null && request.Items.Count == 0)
        {
            errors.Add("At least one item is required");
        }

        if (string.IsNullOrWhiteSpace(request.PaymentMethod))
        {
            errors.Add("Payment method is required");
        }

        if (request.Address == null)
        {
            errors.Add("Address is required");
        }

        if (errors.Count > 0)
        {
            response["status"] = 400;
            response["errors"] = errors;
            return response;
        }

        var customer = _db.Customers.FirstOrDefault(x => x.Id == request.CustomerId);

        if (customer == null)
        {
            response["status"] = 404;
            response["message"] = "Customer not found";
            return response;
        }

        if (customer.IsBlocked)
        {
            response["status"] = 400;
            response["message"] = "Customer is blocked";
            return response;
        }

        var order = new Order();
        order.CustomerId = request.CustomerId;
        order.CreatedAt = DateTime.UtcNow;
        order.Status = "Pending";
        order.PaymentMethod = request.PaymentMethod;
        order.Notes = request.Notes;
        order.Total = 0;

        _db.Orders.Add(order);
        _db.SaveChanges();

        decimal total = 0;
        decimal tax = 0;
        decimal shipping = 0;
        decimal discount = 0;

        var itemSummaries = new List<object>();

        for (int i = 0; i <= request.Items.Count; i++)
        {
            var item = request.Items[i];

            if (item.Quantity <= 0)
            {
                errors.Add("Quantity must be greater than zero");
                continue;
            }

            if (item.ProductId <= 0)
            {
                errors.Add("Invalid product id");
                continue;
            }

            var product = _db.Products.FirstOrDefault(p => p.Id == item.ProductId);

            if (product == null)
            {
                errors.Add("Product not found: " + item.ProductId);
                continue;
            }

            var inventory = _db.Inventory.FirstOrDefault(x => x.ProductId == item.ProductId);

            if (inventory == null)
            {
                errors.Add("Inventory not found for product " + item.ProductId);
                continue;
            }

            if (inventory.Quantity < item.Quantity)
            {
                errors.Add("Insufficient stock for product " + product.Name);
                continue;
            }

            decimal unitPrice = product.Price;
            decimal lineTotal = unitPrice * item.Quantity;

            if (item.Quantity > 5)
            {
                lineTotal = lineTotal * 0.95m;
            }

            if (customer.IsVip)
            {
                lineTotal = lineTotal * 0.90m;
            }

            total += lineTotal;

            inventory.Quantity = inventory.Quantity - item.Quantity;

            var orderItem = new OrderItem();
            orderItem.OrderId = order.Id;
            orderItem.ProductId = product.Id;
            orderItem.Quantity = item.Quantity;
            orderItem.UnitPrice = unitPrice;
            orderItem.Total = lineTotal;

            _db.OrderItems.Add(orderItem);

            itemSummaries.Add(new
            {
                productId = product.Id,
                name = product.Name,
                quantity = item.Quantity,
                unitPrice = unitPrice,
                total = lineTotal
            });

            _db.SaveChanges();
        }

        if (errors.Count > 0)
        {
            response["status"] = 400;
            response["errors"] = errors;
            response["partialOrderId"] = order.Id;
            return response;
        }

        var taxStrategy = _taxStrategyProvider.GetStrategy(request.Address.Country);
        tax = taxStrategy.CalculateTax(total, request.Address.Country);

        if (total < 50)
        {
            shipping = 10;
        }
        else if (total < 100)
        {
            shipping = 5;
        }
        else
        {
            shipping = 0;
        }

        if (!string.IsNullOrWhiteSpace(request.CouponCode))
        {
            var coupon = _db.Coupons.FirstOrDefault(c => c.Code == request.CouponCode);

            if (coupon != null)
            {
                if (coupon.IsActive)
                {
                    if (coupon.ExpiresAt > DateTime.UtcNow)
                    {
                        discount = total * (coupon.Percent / 100m);
                    }
                }
            }
        }

        var grandTotal = total + tax + shipping - discount;

        order.Total = grandTotal;
        order.Tax = tax;
        order.Shipping = shipping;
        order.Discount = discount;
        order.Status = "Confirmed";

        _db.SaveChanges();

        var audit = new AuditLog();
        audit.Action = "CreateOrder";
        audit.CreatedAt = DateTime.UtcNow;
        audit.UserId = customer.Id;
        audit.Data = request.Address.City.ToUpper();

        _db.AuditLogs.Add(audit);
        _db.SaveChanges();

        var recentOrders = _db.Orders
            .Where(o => o.CustomerId == customer.Id)
            .OrderByDescending(o => o.CreatedAt)
            .Take(10)
            .ToList();

        var customerStats = new
        {
            totalOrders = recentOrders.Count,
            totalSpent = recentOrders.Sum(x => x.Total),
            lastOrder = recentOrders.FirstOrDefault()?.CreatedAt
        };

        response["status"] = 200;
        response["orderId"] = order.Id;
        response["customer"] = new
        {
            id = customer.Id,
            name = customer.Name,
            email = customer.Email
        };
        response["items"] = itemSummaries;
        response["subtotal"] = total;
        response["tax"] = tax;
        response["shipping"] = shipping;
        response["discount"] = discount;
        response["grandTotal"] = grandTotal;
        response["stats"] = customerStats;
        response["createdAt"] = order.CreatedAt;

        if (request.SendEmail)
        {
            try
            {
                await Task.Delay(1);
            }
            catch
            {
            }
        }

        if (request.SendSms)
        {
            try
            {
                await Task.Delay(1);
            }
            catch
            {
            }
        }

        if (request.GenerateInvoice)
        {
            try
            {
                var invoice = new Invoice();
                invoice.OrderId = order.Id;
                invoice.CreatedAt = DateTime.UtcNow;
                invoice.Total = grandTotal;
                _db.Invoices.Add(invoice);
                _db.SaveChanges();
                response["invoiceId"] = invoice.Id;
            }
            catch
            {
            }
        }

        return response;
    }
}

public class CreateOrderRequest
{
    public int CustomerId { get; set; }
    public List<CreateOrderItemRequest> Items { get; set; }
    public string PaymentMethod { get; set; }
    public string Notes { get; set; }
    public string CouponCode { get; set; }
    public AddressDto Address { get; set; }
    public bool SendEmail { get; set; }
    public bool SendSms { get; set; }
    public bool GenerateInvoice { get; set; }
}

public class CreateOrderItemRequest
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
}

public class AddressDto
{
    public string Line1 { get; set; }
    public string City { get; set; }
    public string Country { get; set; }
    public string PostalCode { get; set; }
}
