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

    /// <summary>
    /// Produces a category-local classification from an already-ranked subset of standing entries
    /// that all belong to the same category, renumbering positions from one and recomputing gaps
    /// relative to the category leader. New instances are returned, leaving the input untouched.
    /// </summary>
    /// <param name="categoryEntries">The standing entries of a single category, in overall finishing order.</param>
    /// <param name="raceStartTicks">The race start instant, in ticks, used to compute each entry's whole-second classification time.</param>
    /// <returns>A category-local ranked classification, one new entry per input entry.</returns>
    IReadOnlyList<StandingEntry> RankWithinCategory(IReadOnlyList<StandingEntry> categoryEntries, long raceStartTicks);
}
