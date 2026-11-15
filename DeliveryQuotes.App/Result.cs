namespace DeliveryQuotes.App;

// Result<T> has exactly two cases: Success (carries a value) or Failure (carries error messages).
public abstract record Result<T>
{
    // Private constructor: nobody outside can create a third case.
    private Result() { }

    // Case 1: the operation succeeded and we have a value.
    public sealed record Success(T Value) : Result<T>;

    // Case 2: the operation failed and we have the reasons.
    public sealed record Failure(IReadOnlyList<string> Errors) : Result<T>;

    // Helper methods to create each case.
    public static Result<T> Ok(T value)
{
    ArgumentNullException.ThrowIfNull(value);
    return new Success(value);
}
    public static Result<T> Fail(string error) => new Failure(new[] { error });

    // Map: apply a simple function to the value. Failure is passed through untouched.
    public Result<U> Map<U>(Func<T, U> mapper) => this switch
    {
        Success success => new Result<U>.Success(mapper(success.Value)),
        Failure failure => new Result<U>.Failure(failure.Errors),
        _ => throw new InvalidOperationException("Unknown Result case.")
    };

    // Bind: apply a function that itself returns a Result. Failure is passed through untouched.
    public Result<U> Bind<U>(Func<T, Result<U>> binder) => this switch
    {
        Success success => binder(success.Value),
        Failure failure => new Result<U>.Failure(failure.Errors),
        _ => throw new InvalidOperationException("Unknown Result case.")
    };
}