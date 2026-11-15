namespace DeliveryQuotes.App;

// Pure workflow: every method takes a state and RETURNS a new state (or a failure).
// Nothing is modified and nothing is printed here.
public static class QuoteWorkflow
{
    // Raw input -> Requested (only if the factory accepts the input).
    public static Result<QuoteState> Start(RawRequest? raw) =>
        ValidatedRequest.Create(raw)
            .Map<QuoteState>(valid => new QuoteState.Requested(valid));

    // Requested -> Quoted. Every other state is rejected.
    public static Result<QuoteState> Calculate(QuoteState state, Func<int, int> pricePolicy)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(pricePolicy);

        return state switch
        {
            QuoteState.Requested requested =>
                Result<QuoteState>.Ok(new QuoteState.Quoted(
                    requested.Request, pricePolicy(requested.Request.DistanceKm))),
            QuoteState.Quoted =>
                Result<QuoteState>.Fail("The price is already calculated."),
            QuoteState.Confirmed =>
                Result<QuoteState>.Fail("The quote is already confirmed."),
            _ => Result<QuoteState>.Fail("Unknown state.")
        };
    }

    // Quoted -> Confirmed. Requested is rejected (missing prerequisite).
    // Repeated confirmation: the existing Confirmed state is returned (safe rule).
    public static Result<QuoteState> Confirm(QuoteState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return state switch
        {
            QuoteState.Quoted quoted =>
                Result<QuoteState>.Ok(new QuoteState.Confirmed(quoted.Request.Id, quoted.Price)),
            QuoteState.Confirmed confirmed =>
                Result<QuoteState>.Ok(confirmed),
            QuoteState.Requested =>
                Result<QuoteState>.Fail("Cannot confirm: the price has not been calculated yet."),
            _ => Result<QuoteState>.Fail("Unknown state.")
        };
    }

    // The whole chain: Start -> Calculate -> Confirm.
    // After the first failure the next steps are skipped (this is what Bind does).
    public static Result<QuoteState> Run(RawRequest? raw, Func<int, int> pricePolicy) =>
        Start(raw)
            .Bind(state => Calculate(state, pricePolicy))
            .Bind(state => Confirm(state));

    // Pattern matching over both Result cases and every state case.
    public static string Describe(Result<QuoteState> result) => result switch
    {
        Result<QuoteState>.Success success => success.Value switch
        {
            QuoteState.Requested r => $"{r.Request.Id}: requested, {r.Request.DistanceKm} km",
            QuoteState.Quoted q => $"{q.Request.Id}: quoted, price {q.Price}",
            QuoteState.Confirmed c => $"{c.Id}: CONFIRMED, price {c.Price}",
            _ => throw new InvalidOperationException("Unknown state.")
        },
        Result<QuoteState>.Failure failure =>
            "REJECTED: " + string.Join("; ", failure.Errors),
        _ => throw new InvalidOperationException("Unknown result.")
    };
    // Pure transformation: workflow state -> display model (used by Map).
    public static QuoteView ToView(QuoteState state) => state switch
    {
        QuoteState.Requested r => new QuoteView(r.Request.Id, "Requested", null),
        QuoteState.Quoted q => new QuoteView(q.Request.Id, "Quoted", q.Price),
        QuoteState.Confirmed c => new QuoteView(c.Id, "Confirmed", c.Price),
        _ => throw new InvalidOperationException("Unknown state.")
    };

    // The full chain: lookup -> Start -> Calculate -> Confirm -> display model.
    public static Result<QuoteView> RunById(
        IReadOnlyList<RawRequest> requests, string? id, Func<int, int> pricePolicy) =>
        RequestLookup.FindById(requests, id)
            .ToResult($"Request '{id}' was not found.")
            .Bind(raw => Run(raw, pricePolicy))
            .Map(state => ToView(state));

    // Handles the final outcome once, with pattern matching.
    public static string DescribeView(Result<QuoteView> result) => result switch
    {
        Result<QuoteView>.Success success =>
            $"{success.Value.Id}: {success.Value.Status}, price {success.Value.Price}",
        Result<QuoteView>.Failure failure =>
            "REJECTED: " + string.Join("; ", failure.Errors),
        _ => throw new InvalidOperationException("Unknown result.")
    };
    }