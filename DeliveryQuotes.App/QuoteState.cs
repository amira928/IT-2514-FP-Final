namespace DeliveryQuotes.App;

// A closed set of mutually exclusive workflow states.
public abstract record QuoteState
{
    // Private constructor: no other states can be added from outside.
    private QuoteState() { }

    // Validated request, price is not calculated yet.
    public sealed record Requested(ValidatedRequest Request) : QuoteState;

    // Price is calculated, quote is not confirmed yet.
    public sealed record Quoted(ValidatedRequest Request, int Price) : QuoteState;

    // Final state: quote is confirmed.
    public sealed record Confirmed(string Id, int Price) : QuoteState;
}