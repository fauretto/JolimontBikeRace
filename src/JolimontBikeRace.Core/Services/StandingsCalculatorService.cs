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

        for (var rankIndex = 0; rankIndex < orderedRiders.Count; rankIndex++)
        {
            var rider = orderedRiders[rankIndex];
            var position = rankIndex + 1;

            var raceTime = race.StartTicks > 0
                ? TickFormattingHelper.FormatElapsedTime(rider.LastCrossingTicks - race.StartTicks)
                : string.Empty;

            string gap;
            if (leader is null || position == 1)
            {
                // The race leader never has a gap to display.
                gap = string.Empty;
            }
            else if (rider.CompletedLaps < leader.CompletedLaps)
            {
                var lapDeficit = leader.CompletedLaps - rider.CompletedLaps;
                gap = lapDeficit == 1 ? "+1 lap" : $"+{lapDeficit} laps";
            }
            else
            {
                gap = TickFormattingHelper.FormatGap(rider.LastCrossingTicks - leader.LastCrossingTicks);
            }

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
}
