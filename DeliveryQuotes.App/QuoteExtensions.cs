namespace DeliveryQuotes.App;

public static class QuoteExtensions
{
    public static IEnumerable<Quote> OrderByMostExpensive(this IEnumerable<Quote> quotes)
    {
        ArgumentNullException.ThrowIfNull(quotes);

        return quotes
            .OrderByDescending(q => q.Price)
            .ThenBy(q => q.Id, StringComparer.Ordinal);
    }

    public static string DistanceBand(this Quote quote)
    {
        ArgumentNullException.ThrowIfNull(quote);

        return quote.DistanceKm switch
        {
            <= 5 => "Short",
            <= 20 => "Medium",
            _ => "Long"
        };
    }
}