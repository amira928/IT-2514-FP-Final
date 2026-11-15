namespace DeliveryQuotes.App;

// Maybe<T> has exactly two cases: Some (a value is present) or None (nothing found).
public abstract record Maybe<T>
{
    private Maybe() { }

    public sealed record Some(T Value) : Maybe<T>;

    public sealed record None : Maybe<T>;

    public static Maybe<T> Of(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Some(value);
    }

    public static Maybe<T> Empty() => new None();

    // Required item: a missing value becomes a Result failure with a clear message.
    public Result<T> ToResult(string errorIfMissing) => this switch
    {
        Some some => Result<T>.Ok(some.Value),
        None => Result<T>.Fail(errorIfMissing),
        _ => throw new InvalidOperationException("Unknown Maybe case.")
    };
}