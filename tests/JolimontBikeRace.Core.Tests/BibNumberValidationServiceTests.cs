using JolimontBikeRace.Core.Models;
using JolimontBikeRace.Core.Services;

namespace JolimontBikeRace.Core.Tests;

/// <summary>
/// Contains unit tests for <see cref="BibNumberValidationService"/>.
/// </summary>
public class BibNumberValidationServiceTests
{
    private static Category CreateCategory(int minimumBibNumber, int maximumBibNumber)
    {
        return new Category
        {
            Identifier = 1,
            Name = "Adultes",
            MinimumBibNumber = minimumBibNumber,
            MaximumBibNumber = maximumBibNumber,
        };
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(10, true)]
    [InlineData(5, true)]
    [InlineData(0, false)]
    [InlineData(11, false)]
    public void IsWithinCategoryRange_InclusiveBounds_ReturnsExpectedResult(int bibNumber, bool expectedResult)
    {
        var service = new BibNumberValidationService();
        var category = CreateCategory(1, 10);

        var isWithinRange = service.IsWithinCategoryRange(bibNumber, category);

        Assert.Equal(expectedResult, isWithinRange);
    }

    [Fact]
    public void IsAvailable_BibNumberNotYetUsed_ReturnsTrue()
    {
        var service = new BibNumberValidationService();
        var existingRegistrations = new List<Registration>
        {
            new() { BibNumber = 1 },
            new() { BibNumber = 2 },
        };

        var isAvailable = service.IsAvailable(3, existingRegistrations);

        Assert.True(isAvailable);
    }

    [Fact]
    public void IsAvailable_BibNumberAlreadyUsed_ReturnsFalse()
    {
        var service = new BibNumberValidationService();
        var existingRegistrations = new List<Registration>
        {
            new() { BibNumber = 1 },
            new() { BibNumber = 2 },
        };

        var isAvailable = service.IsAvailable(2, existingRegistrations);

        Assert.False(isAvailable);
    }

    [Fact]
    public void GetNextFreeBibNumber_SomeNumbersUsed_SkipsUsedNumbersAndReturnsSmallestFreeNumber()
    {
        var service = new BibNumberValidationService();
        var category = CreateCategory(1, 5);
        var existingRegistrations = new List<Registration>
        {
            new() { BibNumber = 1 },
            new() { BibNumber = 2 },
            new() { BibNumber = 4 },
        };

        var nextFreeBibNumber = service.GetNextFreeBibNumber(category, existingRegistrations);

        Assert.Equal(3, nextFreeBibNumber);
    }

    [Fact]
    public void GetNextFreeBibNumber_RangeExhausted_ReturnsNull()
    {
        var service = new BibNumberValidationService();
        var category = CreateCategory(1, 3);
        var existingRegistrations = new List<Registration>
        {
            new() { BibNumber = 1 },
            new() { BibNumber = 2 },
            new() { BibNumber = 3 },
        };

        var nextFreeBibNumber = service.GetNextFreeBibNumber(category, existingRegistrations);

        Assert.Null(nextFreeBibNumber);
    }
}
