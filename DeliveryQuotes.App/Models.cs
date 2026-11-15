namespace DeliveryQuotes.App;

public record RawRequest(string? Id, string? DistanceText);

public record DeliveryRequest(string Id, int DistanceKm);

public record Quote(string Id, int DistanceKm, int Price);
public record QuoteSummary(IReadOnlyList<Quote> Quotes, int Count, int TotalPrice);
public record QuoteView(string Id, string Status, int? Price);