namespace PilotsRUs.User.App.Services;

public readonly record struct FlightAssignmentNumbers(int Economy, int Business, int First, int CargoKg);

// Pure, deterministic-when-seeded random generation - mirrors the API's ScheduleGenerator pure-logic/
// wrapping-I/O-service split, so the actual randomization can be unit tested without touching the database.
// Each value is independently randomized to 40-100% of its capacity ("realistic load factor" - a flat
// 0-100% range could produce an empty flight, which isn't the intent). A capacity of 0 always yields 0.
public static class FlightAssignmentGenerator
{
    private const double MinLoadFactor = 0.4;

    public static FlightAssignmentNumbers Generate(
        int economyCapacity, int businessCapacity, int firstCapacity, int cargoCapacityKg, Random? random = null)
    {
        random ??= Random.Shared;

        return new FlightAssignmentNumbers(
            RandomInRange(economyCapacity, random),
            RandomInRange(businessCapacity, random),
            RandomInRange(firstCapacity, random),
            RandomInRange(cargoCapacityKg, random));
    }

    private static int RandomInRange(int capacity, Random random)
    {
        if (capacity <= 0)
        {
            return 0;
        }

        var min = (int)Math.Ceiling(capacity * MinLoadFactor);
        return random.Next(min, capacity + 1);
    }
}
