using JolimontBikeRace.Core.Interfaces;
using JolimontBikeRace.Core.Models;

namespace JolimontBikeRace.Core.Services;

/// <summary>
/// Validates bib numbers against a category's reserved range and against the bib numbers already
/// used by other registrations of the same race, and suggests the next free bib number available
/// within a category.
/// </summary>
public class BibNumberValidationService : IBibNumberValidationService
{
    /// <summary>
    /// Determines whether a bib number falls within the inclusive range reserved for a category.
    /// </summary>
    public bool IsWithinCategoryRange(int bibNumber, Category category)
    {
        return bibNumber >= category.MinimumBibNumber && bibNumber <= category.MaximumBibNumber;
    }

    /// <summary>
    /// Determines whether a bib number is not already used by another registration of the same
    /// race.
    /// </summary>
    public bool IsAvailable(int bibNumber, IReadOnlyList<Registration> existingRegistrationsForRace)
    {
        return existingRegistrationsForRace.All(registration => registration.BibNumber != bibNumber);
    }

    /// <summary>
    /// Finds the smallest bib number within a category's reserved range that is not yet used by
    /// any registration of the race.
    /// </summary>
    public int? GetNextFreeBibNumber(Category category, IReadOnlyList<Registration> existingRegistrationsForRace)
    {
        // Build the set of bib numbers already used within the race, so that membership checks
        // while scanning the category's range remain fast even for a large number of riders.
        var usedBibNumbers = new HashSet<int>(
            existingRegistrationsForRace
                .Where(registration => registration.BibNumber.HasValue)
                .Select(registration => registration.BibNumber!.Value));

        for (var candidateBibNumber = category.MinimumBibNumber; candidateBibNumber <= category.MaximumBibNumber; candidateBibNumber++)
        {
            if (!usedBibNumbers.Contains(candidateBibNumber))
            {
                return candidateBibNumber;
            }
        }

        // Every bib number in the category's range is already used.
        return null;
    }
}
