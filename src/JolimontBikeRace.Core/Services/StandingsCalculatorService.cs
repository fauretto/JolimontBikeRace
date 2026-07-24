using JolimontBikeRace.Core.Helpers;
using JolimontBikeRace.Core.Interfaces;
using JolimontBikeRace.Core.Models;

namespace JolimontBikeRace.Core.Services;

/// <summary>
/// Computes the final classification of a race from its raw finish-line crossings, applying the
/// ranking rules described below.
/// </summary>
public class StandingsCalculatorService : IStandingsCalculatorService
{
    /// <summary>
    /// Computes the final classification of a race from its raw crossings.
    /// </summary>
    /// <remarks>
    /// The ranking rules applied by this method are the following. First, every crossing whose
    /// BikerIdentifier equals zero is ignored, because such a crossing has not yet been assigned
    /// to a rider and therefore cannot contribute to anyone's classification. Second, the
    /// remaining crossings are grouped by biker identifier: the number of crossings recorded for
    /// a biker is that biker's number of completed laps, and the largest tick value among those
    /// crossings is the instant of the biker's last recorded passage. Third, riders are ordered
    /// primarily by their number of completed laps, in descending order, so that a rider who
    /// completed more laps always ranks ahead of a rider who completed fewer, regardless of how
    /// much time each of them took. Riders who completed the same number of laps are then ordered
    /// by their last crossing instant, in ascending order, so that the rider who reached that lap
    /// count first ranks ahead. The one-based rank obtained from this ordering becomes the
    /// rider's final position. Fourth, the elapsed race time of a rider is the difference between
    /// that rider's last crossing instant and the race's start instant, formatted as "h:mm:ss";
    /// when the race has not recorded a start instant yet, the elapsed race time is left empty
    /// because it would be meaningless. Fifth, the gap of the race leader, that is, the rider in
    /// first position, is always left empty. The gap of any other rider is expressed as
    /// "+N laps" (using the singular "+1 lap" when applicable) whenever that rider completed
    /// fewer laps than the leader, because a lap deficit cannot meaningfully be expressed as a
    /// time difference; otherwise, when the rider completed the same number of laps as the
    /// leader, the gap is the formatted time difference between that rider's last crossing
    /// instant and the leader's last crossing instant.
    /// </remarks>
    public IReadOnlyList<StandingEntry> ComputeStandings(
        Race race,
        IReadOnlyList<Crossing> crossings,
        IReadOnlyList<Registration> registrations,
        IReadOnlyList<Biker> bikers,
        IReadOnlyList<Category> categories)
    {
        // Crossings with a biker identifier of zero represent bib numbers that were never
        // resolved to an actual rider, so they cannot be attributed to anyone's classification.
        var assignedCrossings = crossings.Where(crossing => crossing.BikerIdentifier != 0).ToList();

        // Group the assigned crossings by rider, computing the completed lap count (the number
        // of crossings) and the instant of the last recorded passage (the maximum tick).
        var groupedByBiker = assignedCrossings
            .GroupBy(crossing => crossing.BikerIdentifier)
            .Select(group => new
            {
                BikerIdentifier = group.Key,
                CompletedLaps = group.Count(),
                LastCrossingTicks = group.Max(crossing => crossing.Ticks)
            })
            .ToList();

        // Order riders by completed laps descending, then by last crossing instant ascending, so
        // that the rider who reached the highest lap count first is ranked first overall.
        var orderedRiders = groupedByBiker
            .OrderByDescending(rider => rider.CompletedLaps)
            .ThenBy(rider => rider.LastCrossingTicks)
            .ToList();

        var registrationByBikerIdentifier = registrations
            .GroupBy(registration => registration.BikerIdentifier)
            .ToDictionary(group => group.Key, group => group.First());
        var bikerByIdentifier = bikers.ToDictionary(biker => biker.Identifier);
        var categoryByIdentifier = categories.ToDictionary(category => category.Identifier);

        var standingEntries = new List<StandingEntry>();

        // The leader is the rider in first position after ordering, used as the reference point
        // for every other rider's gap.
        var leader = orderedRiders.FirstOrDefault();
        var leaderClassificationSeconds = leader is null ? 0 : ClassificationSeconds(leader.LastCrossingTicks, race.StartTicks);

        for (var rankIndex = 0; rankIndex < orderedRiders.Count; rankIndex++)
        {
            var rider = orderedRiders[rankIndex];
            var position = rankIndex + 1;

            var raceTime = race.StartTicks > 0
                ? TickFormattingHelper.FormatElapsedTime(rider.LastCrossingTicks - race.StartTicks)
                : string.Empty;

            var gap = leader is null
                ? string.Empty
                : ComputeGap(position, rider.CompletedLaps, ClassificationSeconds(rider.LastCrossingTicks, race.StartTicks), leader.CompletedLaps, leaderClassificationSeconds);

            registrationByBikerIdentifier.TryGetValue(rider.BikerIdentifier, out var registration);
            bikerByIdentifier.TryGetValue(rider.BikerIdentifier, out var biker);
            Category? category = null;
            if (registration?.CategoryIdentifier is long categoryIdentifier)
            {
                categoryByIdentifier.TryGetValue(categoryIdentifier, out category);
            }

            standingEntries.Add(new StandingEntry
            {
                BikerIdentifier = rider.BikerIdentifier,
                RaceIdentifier = race.Identifier,
                Position = position,
                Ticks = rider.LastCrossingTicks,
                RaceTime = raceTime,
                Gap = gap,
                CompletedLaps = rider.CompletedLaps,
                BikerFullName = biker?.FullName,
                BibNumber = registration?.BibNumber,
                CategoryName = category?.Name
            });
        }

        return standingEntries;
    }

    /// <summary>
    /// Produces a category-local classification from an already-ranked subset of standing entries
    /// that all belong to the same category. The subset is expected to be in overall finishing
    /// order (as produced by <see cref="ComputeStandings"/> and filtered by category); this method
    /// renumbers the positions from one and recomputes every gap relative to the category leader,
    /// that is, the first entry of the subset. New <see cref="StandingEntry"/> instances are
    /// returned so that the original overall entries, and any results already persisted, are left
    /// unchanged.
    /// </summary>
    /// <param name="categoryEntries">The standing entries of a single category, in overall finishing order.</param>
    /// <param name="raceStartTicks">The race start instant, in ticks, used to compute each entry's whole-second classification time.</param>
    /// <returns>A category-local ranked classification, one new entry per input entry.</returns>
    public IReadOnlyList<StandingEntry> RankWithinCategory(IReadOnlyList<StandingEntry> categoryEntries, long raceStartTicks)
    {
        var leader = categoryEntries.FirstOrDefault();
        var leaderClassificationSeconds = leader is null ? 0 : ClassificationSeconds(leader.Ticks, raceStartTicks);
        var rankedEntries = new List<StandingEntry>();

        for (var index = 0; index < categoryEntries.Count; index++)
        {
            var entry = categoryEntries[index];
            var position = index + 1;
            var gap = leader is null
                ? string.Empty
                : ComputeGap(position, entry.CompletedLaps, ClassificationSeconds(entry.Ticks, raceStartTicks), leader.CompletedLaps, leaderClassificationSeconds);

            rankedEntries.Add(new StandingEntry
            {
                Identifier = entry.Identifier,
                BikerIdentifier = entry.BikerIdentifier,
                RaceIdentifier = entry.RaceIdentifier,
                Position = position,
                Ticks = entry.Ticks,
                RaceTime = entry.RaceTime,
                Gap = gap,
                CompletedLaps = entry.CompletedLaps,
                BikerFullName = entry.BikerFullName,
                BibNumber = entry.BibNumber,
                CategoryName = entry.CategoryName,
            });
        }

        return rankedEntries;
    }

    /// <summary>
    /// Computes the formatted gap of a rider to the classification leader, applying the same
    /// rules used by <see cref="ComputeStandings"/>: the leader never has a gap; a rider who
    /// completed fewer laps than the leader is reported as a lap deficit, because that cannot
    /// meaningfully be expressed as a time difference; otherwise the gap is the formatted time
    /// difference between the rider's last crossing instant and the leader's.
    /// </summary>
    /// <param name="position">The one-based position of the rider in the classification.</param>
    /// <param name="completedLaps">The number of laps completed by the rider.</param>
    /// <param name="classificationSeconds">The rider's classification time, in whole seconds.</param>
    /// <param name="leaderCompletedLaps">The number of laps completed by the classification leader.</param>
    /// <param name="leaderClassificationSeconds">The classification leader's classification time, in whole seconds.</param>
    /// <returns>The formatted gap, or an empty string when the rider is the leader.</returns>
    private static string ComputeGap(int position, int completedLaps, long classificationSeconds, int leaderCompletedLaps, long leaderClassificationSeconds)
    {
        // The leader, always in first position, never has a gap to display.
        if (position == 1)
        {
            return string.Empty;
        }

        // A lap deficit cannot meaningfully be expressed as a time difference, so it is reported
        // as a number of laps instead.
        if (completedLaps < leaderCompletedLaps)
        {
            var lapDeficit = leaderCompletedLaps - completedLaps;
            return lapDeficit == 1 ? "+1 lap" : $"+{lapDeficit} laps";
        }

        // The gap is the difference of the two whole-second race times, so it always agrees with
        // the values shown in the race-time column.
        return TickFormattingHelper.FormatGap((classificationSeconds - leaderClassificationSeconds) * TimeSpan.TicksPerSecond);
    }

    /// <summary>
    /// Computes a rider's classification time in whole seconds, that is, the elapsed time between
    /// the race start and the rider's last recorded crossing, floored to whole seconds. This is
    /// exactly the value shown in the race-time column, so gaps derived from it are always
    /// consistent with the displayed race times. A negative elapsed time, which should never occur,
    /// is clamped to zero.
    /// </summary>
    /// <param name="lastCrossingTicks">The instant of the rider's last recorded crossing, in ticks.</param>
    /// <param name="raceStartTicks">The instant at which the race started, in ticks.</param>
    /// <returns>The rider's classification time, expressed in whole seconds.</returns>
    private static long ClassificationSeconds(long lastCrossingTicks, long raceStartTicks)
    {
        var elapsedTicks = Math.Max(0, lastCrossingTicks - raceStartTicks);
        return elapsedTicks / TimeSpan.TicksPerSecond;
    }
}
