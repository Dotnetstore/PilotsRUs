using PilotsRUs.User.App.Services;

namespace PilotsRUs.User.App.Tests.Services;

public sealed class FlightAssignmentGeneratorTests
{
    [Fact]
    public void Generate_AlwaysStaysWithinFortyToHundredPercentOfCapacity()
    {
        var random = new Random(12345);

        for (var i = 0; i < 200; i++)
        {
            var numbers = FlightAssignmentGenerator.Generate(100, 20, 10, 5000, random);

            Assert.InRange(numbers.Economy, 40, 100);
            Assert.InRange(numbers.Business, 8, 20);
            Assert.InRange(numbers.First, 4, 10);
            Assert.InRange(numbers.CargoKg, 2000, 5000);
        }
    }

    [Fact]
    public void Generate_WithZeroCapacity_AlwaysReturnsZero()
    {
        var random = new Random(54321);

        var numbers = FlightAssignmentGenerator.Generate(0, 0, 0, 0, random);

        Assert.Equal(0, numbers.Economy);
        Assert.Equal(0, numbers.Business);
        Assert.Equal(0, numbers.First);
        Assert.Equal(0, numbers.CargoKg);
    }

    [Fact]
    public void Generate_DifferentClasses_AreIndependentlyRandomized()
    {
        var random = new Random(999);

        var results = Enumerable.Range(0, 20)
            .Select(_ => FlightAssignmentGenerator.Generate(100, 100, 100, 100, random))
            .ToList();

        Assert.True(results.Select(r => r.Economy).Distinct().Count() > 1);
        Assert.True(results.Select(r => r.Business).Distinct().Count() > 1);
    }
}
