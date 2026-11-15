namespace DeliveryQuotes.App;

// A real lookup: find a raw request by its Id.
public static class RequestLookup
{
    public static Maybe<RawRequest> FindById(IReadOnlyList<RawRequest> requests, string? id)
    {
        ArgumentNullException.ThrowIfNull(requests);

        if (string.IsNullOrWhiteSpace(id))
        {
            return Maybe<RawRequest>.Empty();
        }

        string key = id.Trim();

        foreach (var request in requests)
        {
            if (request is not null
                && request.Id is not null
                && string.Equals(request.Id.Trim(), key, StringComparison.OrdinalIgnoreCase))
            {
                return Maybe<RawRequest>.Of(request);
            }
        }

        return Maybe<RawRequest>.Empty();
    }
}