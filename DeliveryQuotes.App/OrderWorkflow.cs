namespace DeliveryQuotes.App;

public static class OrderWorkflow
{
    private sealed record PricedOrder(ValidatedRequest Request, int ListPrice, int FinalPrice);

    public static Result<OrderBook> Place(
        OrderBook book,
        IReadOnlyList<Customer> customers,
        IReadOnlyList<RawRequest> requests,
        string? customerId,
        string? requestId,
        Func<int, int> pricePolicy,
        Func<Customer, int, int> discount)
    {
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(customers);
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(pricePolicy);
        ArgumentNullException.ThrowIfNull(discount);

        return CustomerCatalog.FindById(customers, customerId)
            .ToResult($"Customer '{customerId}' was not found.")
            .Bind(customer => RequestLookup.FindById(requests, requestId)
                .ToResult($"Request '{requestId}' was not found.")
                .Bind(raw => ValidatedRequest.Create(raw))
                .Map(request => PriceOrder(customer, request, pricePolicy, discount))
                .Bind(priced => CheckCredit(book, customer, priced)))
            .Bind(order => book.Add(order));
    }

    private static PricedOrder PriceOrder(
        Customer customer,
        ValidatedRequest request,
        Func<int, int> pricePolicy,
        Func<Customer, int, int> discount)
    {
        int listPrice = pricePolicy(request.DistanceKm);
        return new PricedOrder(request, listPrice, discount(customer, listPrice));
    }

    private static Result<ApprovedOrder> CheckCredit(OrderBook book, Customer customer, PricedOrder priced)
    {
        int total = book.SpentBy(customer.Id) + priced.FinalPrice;
        if (total > customer.CreditLimit)
        {
            return Result<ApprovedOrder>.Fail(
                $"{priced.Request.Id}: total {total} exceeds the credit limit {customer.CreditLimit} of customer {customer.Id}.");
        }

        return Result<ApprovedOrder>.Ok(new ApprovedOrder(
            priced.Request.Id, customer.Id, priced.Request.DistanceKm, priced.ListPrice, priced.FinalPrice));
    }
}