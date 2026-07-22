using JolimontBikeRace.Core.Models;

namespace JolimontBikeRace.Core.Interfaces;

/// <summary>
/// Defines the persistence operations available for <see cref="Crossing"/> entities, that is,
/// the raw finish-line crossings captured during the timing of a race.
/// </summary>
public interface ICrossingRepository
{
    /// <summary>
    /// Retrieves every crossing captured for a given race, ordered by sequence index.
    /// </summary>
    /// <param name="raceIdentifier">The unique identifier of the race to retrieve the crossings for.</param>
    /// <returns>A read-only list containing every crossing for the race.</returns>
    Task<IReadOnlyList<Crossing>> GetForRaceAsync(long raceIdentifier);

    /// <summary>
    /// Inserts a new crossing into the database.
    /// </summary>
    /// <param name="crossing">The crossing to insert.</param>
    /// <returns>The unique identifier assigned to the newly inserted crossing.</returns>
    Task<long> AddAsync(Crossing crossing);

    /// <summary>
    /// Updates the details of an already existing crossing, for example to assign a bib number
    /// that was unknown at capture time.
    /// </summary>
    /// <param name="crossing">The crossing holding the updated values, identified by its Identifier property.</param>
    Task UpdateAsync(Crossing crossing);

    /// <summary>
    /// Deletes a single crossing from the database.
    /// </summary>
    /// <param name="identifier">The unique identifier of the crossing to delete.</param>
    Task DeleteAsync(long identifier);

    /// <summary>
    /// Deletes every crossing recorded for a given race. This operation is typically used when
    /// resetting a race before it starts again.
    /// </summary>
    /// <param name="raceIdentifier">The unique identifier of the race whose crossings must be deleted.</param>
    Task DeleteAllForRaceAsync(long raceIdentifier);

    /// <summary>
    /// Replaces every crossing recorded for a given race with a new set of crossings, as a
    /// single atomic operation. This is a critical section because it commits the timing data
    /// captured for the whole race to the database.
    /// </summary>
    /// <param name="raceIdentifier">The unique identifier of the race whose crossings must be replaced.</param>
    /// <param name="crossings">The full, final list of crossings to store for the race.</param>
    Task ReplaceAllForRaceAsync(long raceIdentifier, IReadOnlyList<Crossing> crossings);
}
