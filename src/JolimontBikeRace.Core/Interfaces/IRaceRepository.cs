using JolimontBikeRace.Core.Models;

namespace JolimontBikeRace.Core.Interfaces;

/// <summary>
/// Defines the persistence operations available for <see cref="Race"/> entities.
/// </summary>
public interface IRaceRepository
{
    /// <summary>
    /// Retrieves every race stored in the database.
    /// </summary>
    /// <returns>A read-only list containing every known race.</returns>
    Task<IReadOnlyList<Race>> GetAllAsync();

    /// <summary>
    /// Retrieves a single race by its unique identifier.
    /// </summary>
    /// <param name="identifier">The unique identifier of the race to retrieve.</param>
    /// <returns>The matching race, or null when no race with that identifier exists.</returns>
    Task<Race?> GetByIdentifierAsync(long identifier);

    /// <summary>
    /// Inserts a new race into the database.
    /// </summary>
    /// <param name="race">The race to insert.</param>
    /// <returns>The unique identifier assigned to the newly inserted race.</returns>
    Task<long> AddAsync(Race race);

    /// <summary>
    /// Updates the details of an already existing race.
    /// </summary>
    /// <param name="race">The race holding the updated values, identified by its Identifier property.</param>
    Task UpdateAsync(Race race);

    /// <summary>
    /// Deletes a race from the database.
    /// </summary>
    /// <param name="identifier">The unique identifier of the race to delete.</param>
    Task DeleteAsync(long identifier);

    /// <summary>
    /// Records the start instant of a race. This is a critical operation because it marks the
    /// exact moment from which every rider's elapsed race time will be computed.
    /// </summary>
    /// <param name="raceIdentifier">The unique identifier of the race that is being started.</param>
    /// <param name="startTicks">The start instant, expressed as .NET ticks (see <see cref="System.DateTime.Ticks"/>).</param>
    Task UpdateStartTicksAsync(long raceIdentifier, long startTicks);
}
