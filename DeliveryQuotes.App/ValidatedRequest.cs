using System.Globalization;

namespace DeliveryQuotes.App;

// A delivery request that has ALREADY passed all validation rules.
// It cannot be created in any other way than through Create.
public sealed record ValidatedRequest
{
    public string Id { get; }
    public int DistanceKm { get; }

    // Private constructor: external code cannot bypass the checks.
    private ValidatedRequest(string id, int distanceKm)
    {
        Id = id;
        DistanceKm = distanceKm;
    }

    // Factory: returns Success with a valid value or Failure with all the reasons.
    public static Result<ValidatedRequest> Create(RawRequest? raw)
    {
        if (raw is null)
        {
            return Result<ValidatedRequest>.Fail("Request is missing.");
        }

        var errors = new List<string>();

        // Check 1: the Id must exist (it is trimmed and upper-cased)
        string id = raw.Id?.Trim().ToUpperInvariant() ?? string.Empty;
        if (id.Length == 0)
        {
            errors.Add("Id is missing.");
        }

        // Check 2: the distance must be an integer in the allowed range
        int distance = 0;
        if (string.IsNullOrWhiteSpace(raw.DistanceText))
        {
            errors.Add($"{id}: distance is missing.");
        }
        else if (!int.TryParse(raw.DistanceText, NumberStyles.Integer,
                     CultureInfo.InvariantCulture, out distance))
        {
            errors.Add($"{id}: distance '{raw.DistanceText}' is not a number.");
        }
        else if (distance < RequestParser.MinDistanceKm || distance > RequestParser.MaxDistanceKm)
        {
            errors.Add($"{id}: distance {distance} is out of range " +
                       $"{RequestParser.MinDistanceKm}-{RequestParser.MaxDistanceKm}.");
        }

        // If at least one rule is broken, no object is created at all.
        if (errors.Count > 0)
        {
            return new Result<ValidatedRequest>.Failure(errors);
        }

        return Result<ValidatedRequest>.Ok(new ValidatedRequest(id, distance));
    }
}