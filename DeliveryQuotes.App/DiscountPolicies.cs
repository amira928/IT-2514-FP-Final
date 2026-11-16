namespace DeliveryQuotes.App;

public static class DiscountPolicies
{
    public static int NoDiscount(Customer customer, int price) => price;

    public static readonly Func<int, Func<Customer, int, int>> CreateTierDiscount =
        goldPercent => (customer, price) =>
            customer.Tier == CustomerTier.Gold
                ? price - price * Math.Clamp(goldPercent, 0, 100) / 100
                : price;
}