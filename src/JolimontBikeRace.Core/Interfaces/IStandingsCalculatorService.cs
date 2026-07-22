using JolimontBikeRace.Core.Models;

namespace JolimontBikeRace.Core.Interfaces;

/// <summary>
/// Defines the operation that turns the raw finish-line crossings captured during a race into a
/// ranked final classification.
/// </summary>
public interface IStandingsCalculatorService
{
    /// <summary>
    /// Computes the final classification of a race from its raw crossings.
    /// </summary>
    /// <param name="race">The race that the classification is being computed for.</param>
    /// <param name="crossings">The raw finish-line crossings captured for the race.</param>
    /// <param name="registrations">The registrations of every biker for the race, used to resolve bib numbers and categories.</param>
    /// <param name="bikers">The full list of known bikers, used to resolve full names.</param>
    /// <param name="categories">The full list of known categories, used to resolve category names.</param>
    /// <returns>A ranked, read-only list of standing entries, one per biker who has at least one valid crossing.</returns>
    IReadOnlyList<StandingEntry> ComputeStandings(
        Race race,
        IReadOnlyList<Crossing> crossings,
        IReadOnlyList<Registration> registrations,
        IReadOnlyList<Biker> bikers,
        IReadOnlyList<Category> categories);
}
