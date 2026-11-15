using System.Globalization;

namespace DeliveryQuotes.App;

public enum CustomerTier { Regular, Gold }

public record RawCustomer(string? Id, string? Name, string? TierText, string? CreditText);

public sealed record Customer
{
    public const int MaxCreditLimit = 100_000;

    public string Id { get; }
    public string Name { get; }
    public CustomerTier Tier { get; }
    public int CreditLimit { get; }

    private Customer(string id, string name, CustomerTier tier, int creditLimit)
    {
        Id = id;
        Name = name;
        Tier = tier;
        CreditLimit = creditLimit;
    }

    public static Result<Customer> Create(RawCustomer? raw)
    {
        if (raw is null)
        {
            return Result<Customer>.Fail("Customer is missing.");
        }

        var errors = new List<string>();

        string id = raw.Id?.Trim().ToUpperInvariant() ?? string.Empty;
        if (id.Length == 0)
        {
            errors.Add("Customer id is missing.");
        }

        string name = raw.Name?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            errors.Add($"{id}: customer name is missing.");
        }

        CustomerTier? tier = (raw.TierText?.Trim().ToUpperInvariant()) switch
        {
            "REGULAR" => CustomerTier.Regular,
            "GOLD" => CustomerTier.Gold,
            _ => null
        };
        if (tier is null)
        {
            errors.Add($"{id}: tier '{raw.TierText}' is unknown (use Regular or Gold).");
        }

        int credit = 0;
        if (!int.TryParse(raw.CreditText, NumberStyles.Integer, CultureInfo.InvariantCulture, out credit))
        {
            errors.Add($"{id}: credit limit '{raw.CreditText}' is not a number.");
        }
        else if (credit < 0 || credit > MaxCreditLimit)
        {
            errors.Add($"{id}: credit limit {credit} is out of range 0-{MaxCreditLimit}.");
        }

        if (errors.Count > 0)
        {
            return new Result<Customer>.Failure(errors);
        }

        return Result<Customer>.Ok(new Customer(id, name, tier ?? CustomerTier.Regular, credit));
    }
}