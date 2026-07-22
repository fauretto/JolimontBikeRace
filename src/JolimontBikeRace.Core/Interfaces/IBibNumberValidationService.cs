using JolimontBikeRace.Core.Models;

namespace JolimontBikeRace.Core.Interfaces;

/// <summary>
/// Defines the operations used to validate and suggest bib numbers when registering a biker into
/// a race and a category.
/// </summary>
public interface IBibNumberValidationService
{
    /// <summary>
    /// Determines whether a bib number falls within the inclusive range reserved for a category.
    /// </summary>
    /// <param name="bibNumber">The bib number to validate.</param>
    /// <param name="category">The category whose reserved range must be checked against.</param>
    /// <returns>True when the bib number lies within the category's minimum and maximum bounds, inclusive.</returns>
    bool IsWithinCategoryRange(int bibNumber, Category category);

    /// <summary>
    /// Determines whether a bib number is not already used by another registration of the same
    /// race.
    /// </summary>
    /// <param name="bibNumber">The bib number to validate.</param>
    /// <param name="existingRegistrationsForRace">The registrations already recorded for the race.</param>
    /// <returns>True when no existing registration for the race uses that bib number.</returns>
    bool IsAvailable(int bibNumber, IReadOnlyList<Registration> existingRegistrationsForRace);

    /// <summary>
    /// Finds the smallest bib number within a category's reserved range that is not yet used by
    /// any registration of the race.
    /// </summary>
    /// <param name="category">The category whose reserved range must be searched.</param>
    /// <param name="existingRegistrationsForRace">The registrations already recorded for the race.</param>
    /// <returns>The smallest free bib number, or null when the whole range is already used.</returns>
    int? GetNextFreeBibNumber(Category category, IReadOnlyList<Registration> existingRegistrationsForRace);
}
