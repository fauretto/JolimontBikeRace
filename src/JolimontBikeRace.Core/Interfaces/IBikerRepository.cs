using JolimontBikeRace.Core.Models;

namespace JolimontBikeRace.Core.Interfaces;

/// <summary>
/// Defines the persistence operations available for <see cref="Biker"/> entities.
/// </summary>
public interface IBikerRepository
{
    /// <summary>
    /// Retrieves every biker stored in the database.
    /// </summary>
    /// <returns>A read-only list containing every known biker.</returns>
    Task<IReadOnlyList<Biker>> GetAllAsync();

    /// <summary>
    /// Retrieves a single biker by its unique identifier.
    /// </summary>
    /// <param name="identifier">The unique identifier of the biker to retrieve.</param>
    /// <returns>The matching biker, or null when no biker with that identifier exists.</returns>
    Task<Biker?> GetByIdentifierAsync(long identifier);

    /// <summary>
    /// Inserts a new biker into the database.
    /// </summary>
    /// <param name="biker">The biker to insert.</param>
    /// <returns>The unique identifier assigned to the newly inserted biker.</returns>
    Task<long> AddAsync(Biker biker);

    /// <summary>
    /// Updates the details of an already existing biker.
    /// </summary>
    /// <param name="biker">The biker holding the updated values, identified by its Identifier property.</param>
    Task UpdateAsync(Biker biker);

    /// <summary>
    /// Deletes a biker from the database.
    /// </summary>
    /// <param name="identifier">The unique identifier of the biker to delete.</param>
    Task DeleteAsync(long identifier);

    /// <summary>
    /// Deletes a biker together with every dependent record referencing it: its registrations
    /// (biker_race_category), its computed race results (standing) and its raw finish-line
    /// crossings (race_standings). The whole operation is performed as a single atomic
    /// transaction so a biker is never left partially deleted.
    /// </summary>
    /// <param name="identifier">The unique identifier of the biker to delete.</param>
    Task DeleteWithDependenciesAsync(long identifier);
}
