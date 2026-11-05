namespace OllamaToolApproval.Shop;

public enum OrderStatus { Processing, Delivered }

public sealed record Customer(Guid Id, string Name, string Email);

public sealed record OrderLine(string Product, int Quantity, int UnitPriceCents);

public sealed class Order
{
    public required Guid Id { get; init; }
    public required int Number { get; init; }
    public required Guid CustomerId { get; init; }
    public required DateOnly PlacedOn { get; init; }
    public required IReadOnlyList<OrderLine> Lines { get; init; }
    public OrderStatus Status { get; set; }
    public int RefundedCents { get; set; }

    public int TotalCents => Lines.Sum(l => l.Quantity * l.UnitPriceCents);
    public int RefundableCents => TotalCents - RefundedCents;
    public string Summary => string.Join(", ", Lines.Select(l => $"{l.Quantity}× {l.Product}"));
}

public enum RefundResult { Refunded, OrderNotFound, NothingLeftToRefund, AmountTooLarge, InvalidAmount }

public enum DeleteResult { Deleted, CustomerNotFound, HasOpenOrders }

/// <summary>In-memory stand-in for the Gozo Pantry database.</summary>
public sealed class ShopStore
{
    private readonly Lock _lock = new();
    private readonly List<Customer> _customers;
    private readonly List<Order> _orders;

    public ShopStore()
    {
        var maria = new Customer(Guid.Parse("3b1f6c2a-8d4e-4f7a-9c21-5e8a7d6b4c10"), "Maria Borg", "maria.borg@example.mt");
        var joseph = new Customer(Guid.Parse("a7c9e2d4-1b3f-4e6a-8d5c-2f9b0e7a6c31"), "Joseph Camilleri", "joseph.c@example.mt");
        var anna = new Customer(Guid.Parse("5d8e1f3a-6c2b-4a9e-b7d4-0c3e9f2a1b52"), "Anna Zammit", "anna.zammit@example.mt");
        _customers = [maria, joseph, anna];

        _orders =
        [
            NewOrder("c41e7b9a-2d5f-4a8c-9e3b-6f1d0a7c8e01", 1039, joseph, new(2026, 6, 14), OrderStatus.Delivered,
                [new("Gbejniet (pack of 6)", 2, 750)]),
            NewOrder("e2b8d4f6-9a1c-4e7b-8d3f-5c0a2e9b7d02", 1040, joseph, new(2026, 8, 2), OrderStatus.Delivered,
                [new("Carob syrup 250ml", 1, 650), new("Gozo honey 340g", 1, 1200)]),
            NewOrder("7f3a9c1e-4b2d-4c8e-a6f0-3d9b1e5c7a04", 1042, maria, new(2026, 10, 21), OrderStatus.Delivered,
                [new("Gozo sea salt", 2, 625), new("Extra virgin olive oil 500ml", 1, 3000)]),
            NewOrder("9d6c3b8a-5e1f-4a2d-b7c9-0e4f8a3d6b05", 1043, joseph, new(2026, 9, 18), OrderStatus.Delivered,
                [new("Sun-dried tomatoes 280g", 2, 490), new("Kunserva 200g", 1, 350)]),
            NewOrder("1a8f5d2c-7b3e-4f9a-8c6d-4e2b0f9a3c06", 1045, maria, new(2026, 10, 2), OrderStatus.Delivered,
                [new("Ftira baking kit", 1, 1800)], refundedCents: 1800),
            NewOrder("6e2c9a4f-3d8b-4b1e-9f7a-2c5d8e0b4a07", 1046, anna, new(2026, 11, 3), OrderStatus.Processing,
                [new("Pastizzi frozen box (12)", 1, 1450), new("Gozo honey 340g", 1, 1200)]),
        ];
    }

    public Order? FindOrder(Guid id)
    {
        lock (_lock) return _orders.FirstOrDefault(o => o.Id == id);
    }

    public Order? FindOrderByNumber(int number)
    {
        lock (_lock) return _orders.FirstOrDefault(o => o.Number == number);
    }

    public Customer? FindCustomer(Guid id)
    {
        lock (_lock) return _customers.FirstOrDefault(c => c.Id == id);
    }

    public IReadOnlyList<Order> OrdersFor(Guid customerId)
    {
        lock (_lock) return [.. _orders.Where(o => o.CustomerId == customerId).OrderBy(o => o.PlacedOn)];
    }

    public RefundResult Refund(Guid orderId, int amountCents)
    {
        lock (_lock)
        {
            var order = _orders.FirstOrDefault(o => o.Id == orderId);
            if (order is null) return RefundResult.OrderNotFound;
            if (amountCents <= 0) return RefundResult.InvalidAmount;
            if (order.RefundableCents == 0) return RefundResult.NothingLeftToRefund;
            if (amountCents > order.RefundableCents) return RefundResult.AmountTooLarge;

            order.RefundedCents += amountCents;
            return RefundResult.Refunded;
        }
    }

    public DeleteResult DeleteCustomer(Guid customerId)
    {
        lock (_lock)
        {
            var customer = _customers.FirstOrDefault(c => c.Id == customerId);
            if (customer is null) return DeleteResult.CustomerNotFound;
            if (_orders.Any(o => o.CustomerId == customerId && o.Status == OrderStatus.Processing)) return DeleteResult.HasOpenOrders;

            _customers.Remove(customer);
            return DeleteResult.Deleted;
        }
    }

    private static Order NewOrder(string id, int number, Customer customer, DateOnly placedOn, OrderStatus status,
        IReadOnlyList<OrderLine> lines, int refundedCents = 0) => new()
    {
        Id = Guid.Parse(id),
        Number = number,
        CustomerId = customer.Id,
        PlacedOn = placedOn,
        Status = status,
        Lines = lines,
        RefundedCents = refundedCents,
    };
}
