namespace DeliveryQuotes.App;

public record PremiumLine(string Id, int DistanceKm, int Price);

public record BandTotal(string Band, int Count, int TotalPrice);

public static class Reports
{
    public static IReadOnlyList<PremiumLine> PremiumQuotes(IEnumerable<Quote> quotes, int minPrice)
    {
        ArgumentNullException.ThrowIfNull(quotes);

        return quotes
            .Where(q => q.Price >= minPrice)
            .OrderByMostExpensive()
            .Select(q => new PremiumLine(q.Id, q.DistanceKm, q.Price))
            .ToArray();
    }

    public static IReadOnlyList<BandTotal> TotalsByBand(IEnumerable<Quote> quotes)
    {
        ArgumentNullException.ThrowIfNull(quotes);

        return quotes
            .GroupBy(q => q.DistanceBand())
            .Select(g => new BandTotal(g.Key, g.Count(), g.Sum(q => q.Price)))
            .OrderByDescending(b => b.TotalPrice)
            .ThenBy(b => b.Band, StringComparer.Ordinal)
            .ToArray();
    }
}