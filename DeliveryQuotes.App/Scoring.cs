namespace DeliveryQuotes.App;

public record ScoredQuote(string Id, int Price, int Score);

public static class Scoring
{
    public static readonly Func<int, Func<int, Func<Quote, int>>> CreateScore =
        thresholdKm => weightPerKm => quote =>
            quote.Price + Math.Max(0, quote.DistanceKm - thresholdKm) * weightPerKm;

    public static IReadOnlyList<ScoredQuote> Rank(IEnumerable<Quote> quotes, Func<Quote, int> score)
    {
        ArgumentNullException.ThrowIfNull(quotes);
        ArgumentNullException.ThrowIfNull(score);

        return quotes
            .Select(q => new ScoredQuote(q.Id, q.Price, score(q)))
            .OrderByDescending(s => s.Score)
            .ThenBy(s => s.Id, StringComparer.Ordinal)
            .ToArray();
    }
}