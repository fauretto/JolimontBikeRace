using JolimontBikeRace.Core.Models;

namespace JolimontBikeRace.Core.Interfaces;

/// <summary>
/// Defines the persistence operations that manage the association between races and categories.
/// </summary>
public interface IRaceCategoryLinkRepository
{
    /// <summary>
    /// Retrieves every category link that is currently associated with a given race.
    /// </summary>
    /// <param name="raceIdentifier">The unique identifier of the race to retrieve the links for.</param>
    /// <returns>A read-only list containing every link associated with the race.</returns>
    Task<IReadOnlyList<RaceCategoryLink>> GetForRaceAsync(long raceIdentifier);

    /// <summary>
    /// Associates a category with a race. This operation is idempotent: calling it again for a
    /// pair that is already linked has no additional effect.
    /// </summary>
    /// <param name="raceIdentifier">The unique identifier of the race.</param>
    /// <param name="categoryIdentifier">The unique identifier of the category.</param>
    Task LinkAsync(long raceIdentifier, long categoryIdentifier);

    /// <summary>
    /// Removes the association between a category and a race.
    /// </summary>
    /// <param name="raceIdentifier">The unique identifier of the race.</param>
    /// <param name="categoryIdentifier">The unique identifier of the category.</param>
    Task UnlinkAsync(long raceIdentifier, long categoryIdentifier);
}
