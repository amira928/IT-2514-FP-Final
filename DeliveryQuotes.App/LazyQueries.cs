namespace DeliveryQuotes.App;

public static class LazyQueries
{
    public static IEnumerable<Quote> LazyQuotes(IEnumerable<RawRequest> raws, Func<int, int> policy)
    {
        ArgumentNullException.ThrowIfNull(raws);
        ArgumentNullException.ThrowIfNull(policy);

        return Iterate(raws, policy);
    }

    private static IEnumerable<Quote> Iterate(IEnumerable<RawRequest> raws, Func<int, int> policy)
    {
        foreach (var raw in raws)
        {
            if (ValidatedRequest.Create(raw) is Result<ValidatedRequest>.Success ok)
            {
                yield return new Quote(ok.Value.Id, ok.Value.DistanceKm, policy(ok.Value.DistanceKm));
            }
        }
    }
}