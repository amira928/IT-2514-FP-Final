namespace DeliveryQuotes.App;

public static class Pipeline
{
    // Stage 1: normalize request IDs and remove duplicates
    public static IReadOnlyList<DeliveryRequest> NormalizeRequests(
        IReadOnlyList<DeliveryRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(requests);

        return requests
            .Select(r => r with { Id = r.Id.Trim().ToUpperInvariant() })
            .DistinctBy(r => r.Id)
            .ToArray();
    }

    // Stage 2: calculate the price of each request using the supplied policy
    public static IReadOnlyList<Quote> CalculateQuotes(
        IReadOnlyList<DeliveryRequest> requests,
        Func<int, int> pricePolicy)
    {
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(pricePolicy);

        return requests
            .Select(r => new Quote(r.Id, r.DistanceKm, pricePolicy(r.DistanceKm)))
            .ToArray();
    }

    // Stage 3: order the quotes and build the final summary
    public static QuoteSummary BuildSummary(IReadOnlyList<Quote> quotes)
    {
        ArgumentNullException.ThrowIfNull(quotes);

        var ordered = quotes
            .OrderBy(q => q.Price)
            .ThenBy(q => q.Id, StringComparer.Ordinal)
            .ToArray();

        return new QuoteSummary(ordered, ordered.Length, ordered.Sum(q => q.Price));
    }

    // Full workflow: the output of each stage is the input of the next one
    public static QuoteSummary Run(
        IReadOnlyList<DeliveryRequest> requests,
        Func<int, int> pricePolicy)
    {
        var normalized = NormalizeRequests(requests);
        var quotes = CalculateQuotes(normalized, pricePolicy);
        return BuildSummary(quotes);
    }
}