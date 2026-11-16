namespace DeliveryQuotes.App;

public enum ServiceType { Standard, Express }

public abstract record Eligibility
{
    private Eligibility() { }

    public sealed record Eligible(ApprovedOrder Order, ServiceType Service) : Eligibility;

    public sealed record TooFar(string OrderId, int DistanceKm, int MaxKm) : Eligibility;
}

public sealed record DispatchPlan(string OrderId, ServiceType Service, int Price, int ServiceFee, int Total);

public static class DispatchWorkflow
{
    public const int ExpressMaxKm = 20;

    public static Eligibility CheckEligibility(ApprovedOrder order, ServiceType service)
    {
        ArgumentNullException.ThrowIfNull(order);

        return service switch
        {
            ServiceType.Express when order.DistanceKm > ExpressMaxKm =>
                new Eligibility.TooFar(order.OrderId, order.DistanceKm, ExpressMaxKm),
            _ => new Eligibility.Eligible(order, service)
        };
    }

    public static int FeePercent(ServiceType service) => service switch
    {
        ServiceType.Standard => 10,
        ServiceType.Express => 15,
        _ => throw new InvalidOperationException("Unknown service type.")
    };

    public static Result<DispatchPlan> Plan(
        ApprovedOrder order,
        ServiceType service,
        Func<int, int, Result<int>> feeCalculator)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(feeCalculator);

        return ToEligibleResult(CheckEligibility(order, service))
            .Bind(eligible => feeCalculator(eligible.Order.FinalPrice, FeePercent(eligible.Service))
                .Map(fee => new DispatchPlan(
                    eligible.Order.OrderId,
                    eligible.Service,
                    eligible.Order.FinalPrice,
                    fee,
                    eligible.Order.FinalPrice + fee)));
    }

    public static Result<DispatchPlan> Plan(ApprovedOrder order, ServiceType service) =>
        Plan(order, service, NativeFeeAdapter.ServiceFee);

    private static Result<Eligibility.Eligible> ToEligibleResult(Eligibility eligibility) => eligibility switch
    {
        Eligibility.Eligible eligible => Result<Eligibility.Eligible>.Ok(eligible),
        Eligibility.TooFar tooFar => Result<Eligibility.Eligible>.Fail(
            $"{tooFar.OrderId}: Express delivery is limited to {tooFar.MaxKm} km, but the distance is {tooFar.DistanceKm} km."),
        _ => throw new InvalidOperationException("Unknown eligibility case.")
    };
}