namespace DeliveryQuotes.App;

public static class PricePolicies
{
    // Standard policy: 500 base price + 100 per kilometre
    public static int Standard(int distanceKm) => 500 + 100 * distanceKm;

    // Express policy: 800 base price + 150 per kilometre
    public static int Express(int distanceKm) => 800 + 150 * distanceKm;
}