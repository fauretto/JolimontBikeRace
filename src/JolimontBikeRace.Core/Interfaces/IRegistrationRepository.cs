using JolimontBikeRace.Core.Models;

namespace JolimontBikeRace.Core.Interfaces;

/// <summary>
/// Defines the persistence operations available for <see cref="Registration"/> entities, that
/// is, the entries linking bikers to races with an assigned bib number.
/// </summary>
public interface IRegistrationRepository
{
    /// <summary>
    /// Retrieves every registration for a given race.
    /// </summary>
    /// <param name="raceIdentifier">The unique identifier of the race to retrieve the registrations for.</param>
    /// <returns>A read-only list containing every registration for the race.</returns>
    Task<IReadOnlyList<Registration>> GetForRaceAsync(long raceIdentifier);

    /// <summary>
    /// Retrieves every registration of a given biker, across every race.
    /// </summary>
    /// <param name="bikerIdentifier">The unique identifier of the biker to retrieve the registrations for.</param>
    /// <returns>A read-only list containing every registration of the biker.</returns>
    Task<IReadOnlyList<Registration>> GetForBikerAsync(long bikerIdentifier);

    /// <summary>
    /// Retrieves the registration matching a given race and bib number, if one exists.
    /// </summary>
    /// <param name="raceIdentifier">The unique identifier of the race.</param>
    /// <param name="bibNumber">The bib number to look for.</param>
    /// <returns>The matching registration, or null when no registration uses that bib number in that race.</returns>
    Task<Registration?> GetByRaceAndBibNumberAsync(long raceIdentifier, int bibNumber);

    /// <summary>
    /// Inserts a new registration into the database.
    /// </summary>
    /// <param name="registration">The registration to insert.</param>
    /// <returns>The unique identifier assigned to the newly inserted registration.</returns>
    Task<long> AddAsync(Registration registration);

    /// <summary>
    /// Deletes a registration from the database.
    /// </summary>
    /// <param name="identifier">The unique identifier of the registration to delete.</param>
    Task DeleteAsync(long identifier);
}
