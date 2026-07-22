using JolimontBikeRace.Core.Models;

namespace JolimontBikeRace.Core.Interfaces;

/// <summary>
/// Defines the persistence operations available for <see cref="StandingEntry"/> entities, that
/// is, the computed final classification of a race.
/// </summary>
public interface IStandingRepository
{
    /// <summary>
    /// Retrieves the computed final classification stored for a given race.
    /// </summary>
    /// <param name="raceIdentifier">The unique identifier of the race to retrieve the classification for.</param>
    /// <returns>A read-only list containing every standing entry stored for the race.</returns>
    Task<IReadOnlyList<StandingEntry>> GetForRaceAsync(long raceIdentifier);

    /// <summary>
    /// Replaces the computed final classification stored for a given race with a new set of
    /// entries, as a single atomic operation. This is a critical section because it commits the
    /// official results of the race to the database.
    /// </summary>
    /// <param name="raceIdentifier">The unique identifier of the race whose classification must be replaced.</param>
    /// <param name="entries">The full, final classification to store for the race.</param>
    Task ReplaceForRaceAsync(long raceIdentifier, IReadOnlyList<StandingEntry> entries);
}
