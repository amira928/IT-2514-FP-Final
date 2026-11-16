namespace DeliveryQuotes.App;

public sealed record ApprovedOrder
{
    public string OrderId { get; }
    public string CustomerId { get; }
    public int DistanceKm { get; }
    public int ListPrice { get; }
    public int FinalPrice { get; }

    internal ApprovedOrder(string orderId, string customerId, int distanceKm, int listPrice, int finalPrice)
    {
        OrderId = orderId;
        CustomerId = customerId;
        DistanceKm = distanceKm;
        ListPrice = listPrice;
        FinalPrice = finalPrice;
    }
}

public sealed record OrderBook
{
    public IReadOnlyList<ApprovedOrder> Orders { get; }

    private OrderBook(IReadOnlyList<ApprovedOrder> orders)
    {
        Orders = orders;
    }

    public static OrderBook Empty { get; } = new OrderBook(Array.Empty<ApprovedOrder>());

    public int SpentBy(string customerId) =>
        Orders
            .Where(o => string.Equals(o.CustomerId, customerId, StringComparison.OrdinalIgnoreCase))
            .Sum(o => o.FinalPrice);

    public Result<OrderBook> Add(ApprovedOrder order)
    {
        ArgumentNullException.ThrowIfNull(order);

        if (Orders.Any(o => o.OrderId == order.OrderId))
        {
            return Result<OrderBook>.Fail($"Order {order.OrderId} already exists.");
        }

        return Result<OrderBook>.Ok(new OrderBook(Orders.Append(order).ToArray()));
    }
}