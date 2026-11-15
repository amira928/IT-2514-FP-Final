using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace DeliveryQuotes.App;

public static class RequestParser
{
    public const int MinDistanceKm = 1;
    public const int MaxDistanceKm = 50;

    public static bool TryParse(
        RawRequest? raw,
        [NotNullWhen(true)] out DeliveryRequest? request,
        out string error)
    {
        request = null;

        if (raw is null)
        {
            error = "Request is missing.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(raw.Id))
        {
            error = "Id is missing.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(raw.DistanceText))
        {
            error = $"{raw.Id}: distance is missing.";
            return false;
        }

        if (!int.TryParse(raw.DistanceText, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out int distance))
        {
            error = $"{raw.Id}: distance '{raw.DistanceText}' is not a number.";
            return false;
        }

        if (distance < MinDistanceKm || distance > MaxDistanceKm)
        {
            error = $"{raw.Id}: distance {distance} is out of range {MinDistanceKm}-{MaxDistanceKm}.";
            return false;
        }

        request = new DeliveryRequest(raw.Id, distance);
        error = string.Empty;
        return true;
    }
}