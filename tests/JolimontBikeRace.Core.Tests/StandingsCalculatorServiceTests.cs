using JolimontBikeRace.Core.Models;
using JolimontBikeRace.Core.Services;

namespace JolimontBikeRace.Core.Tests;

/// <summary>
/// Contains unit tests for <see cref="StandingsCalculatorService"/>.
/// </summary>
public class StandingsCalculatorServiceTests
{
    private static readonly List<Registration> EmptyRegistrations = new();
    private static readonly List<Biker> EmptyBikers = new();
    private static readonly List<Category> EmptyCategories = new();

    [Fact]
    public void ComputeStandings_RidersWithSameLapCount_OrdersByCompletedTimeAscending()
    {
        var service = new StandingsCalculatorService();
        const long raceStartTicks = 100;
        var race = new Race { Identifier = 1, Name = "Test Race", StartTicks = raceStartTicks };

        var crossings = new List<Crossing>
        {
            new() { BikerIdentifier = 1, RaceIdentifier = 1, SequenceIndex = 1, Ticks = raceStartTicks + TimeSpan.FromMinutes(15).Ticks },
            new() { BikerIdentifier = 1, RaceIdentifier = 1, SequenceIndex = 2, Ticks = raceStartTicks + TimeSpan.FromMinutes(30).Ticks },
            new() { BikerIdentifier = 2, RaceIdentifier = 1, SequenceIndex = 3, Ticks = raceStartTicks + TimeSpan.FromMinutes(17).Ticks },
            new() { BikerIdentifier = 2, RaceIdentifier = 1, SequenceIndex = 4, Ticks = raceStartTicks + TimeSpan.FromMinutes(35).Ticks },
        };

        var standings = service.ComputeStandings(race, crossings, EmptyRegistrations, EmptyBikers, EmptyCategories);

        Assert.Equal(2, standings.Count);
        Assert.Equal(1, standings[0].BikerIdentifier);
        Assert.Equal(1, standings[0].Position);
        Assert.Equal(2, standings[1].BikerIdentifier);
        Assert.Equal(2, standings[1].Position);
    }

    [Fact]
    public void ComputeStandings_RidersWithDifferentLapCounts_RanksMoreLapsAheadOfFasterFewerLaps()
    {
        var service = new StandingsCalculatorService();
        const long raceStartTicks = 100;
        var race = new Race { Identifier = 1, Name = "Test Race", StartTicks = raceStartTicks };

        var crossings = new List<Crossing>
        {
            // Rider 1 completes two laps, finishing relatively late.
            new() { BikerIdentifier = 1, RaceIdentifier = 1, SequenceIndex = 1, Ticks = raceStartTicks + TimeSpan.FromMinutes(20).Ticks },
            new() { BikerIdentifier = 1, RaceIdentifier = 1, SequenceIndex = 2, Ticks = raceStartTicks + TimeSpan.FromMinutes(40).Ticks },
            // Rider 2 completes a single lap, but faster than rider 1's first lap.
            new() { BikerIdentifier = 2, RaceIdentifier = 1, SequenceIndex = 3, Ticks = raceStartTicks + TimeSpan.FromMinutes(10).Ticks },
        };

        var standings = service.ComputeStandings(race, crossings, EmptyRegistrations, EmptyBikers, EmptyCategories);

        Assert.Equal(1, standings[0].BikerIdentifier);
        Assert.Equal(2, standings[0].CompletedLaps);
        Assert.Equal(2, standings[1].BikerIdentifier);
        Assert.Equal(1, standings[1].CompletedLaps);
        Assert.Equal("+1 lap", standings[1].Gap);
    }

    [Fact]
    public void ComputeStandings_RidersWithMultipleLapDeficit_ReturnsPluralLapsGap()
    {
        var service = new StandingsCalculatorService();
        const long raceStartTicks = 100;
        var race = new Race { Identifier = 1, Name = "Test Race", StartTicks = raceStartTicks };

        var crossings = new List<Crossing>
        {
            new() { BikerIdentifier = 1, RaceIdentifier = 1, SequenceIndex = 1, Ticks = raceStartTicks + TimeSpan.FromMinutes(10).Ticks },
            new() { BikerIdentifier = 1, RaceIdentifier = 1, SequenceIndex = 2, Ticks = raceStartTicks + TimeSpan.FromMinutes(20).Ticks },
            new() { BikerIdentifier = 1, RaceIdentifier = 1, SequenceIndex = 3, Ticks = raceStartTicks + TimeSpan.FromMinutes(30).Ticks },
            new() { BikerIdentifier = 2, RaceIdentifier = 1, SequenceIndex = 4, Ticks = raceStartTicks + TimeSpan.FromMinutes(5).Ticks },
        };

        var standings = service.ComputeStandings(race, crossings, EmptyRegistrations, EmptyBikers, EmptyCategories);

        Assert.Equal("+2 laps", standings[1].Gap);
    }

    [Fact]
    public void ComputeStandings_SameLapCount_FormatsGapAsTimeDifference()
    {
        var service = new StandingsCalculatorService();
        const long raceStartTicks = 100;
        var race = new Race { Identifier = 1, Name = "Test Race", StartTicks = raceStartTicks };

        var crossings = new List<Crossing>
        {
            new() { BikerIdentifier = 1, RaceIdentifier = 1, SequenceIndex = 1, Ticks = raceStartTicks + TimeSpan.FromMinutes(30).Ticks },
            new() { BikerIdentifier = 2, RaceIdentifier = 1, SequenceIndex = 2, Ticks = raceStartTicks + TimeSpan.FromMinutes(31).Ticks + TimeSpan.FromSeconds(2).Ticks },
        };

        var standings = service.ComputeStandings(race, crossings, EmptyRegistrations, EmptyBikers, EmptyCategories);

        Assert.Equal(string.Empty, standings[0].Gap);
        Assert.Equal("+1:02", standings[1].Gap);
    }

    [Fact]
    public void ComputeStandings_CrossingsWithUnassignedBikerIdentifier_IgnoresThoseCrossings()
    {
        var service = new StandingsCalculatorService();
        const long raceStartTicks = 100;
        var race = new Race { Identifier = 1, Name = "Test Race", StartTicks = raceStartTicks };

        var crossings = new List<Crossing>
        {
            new() { BikerIdentifier = 0, RaceIdentifier = 1, SequenceIndex = 1, Ticks = raceStartTicks + TimeSpan.FromMinutes(5).Ticks },
            new() { BikerIdentifier = 1, RaceIdentifier = 1, SequenceIndex = 2, Ticks = raceStartTicks + TimeSpan.FromMinutes(10).Ticks },
        };

        var standings = service.ComputeStandings(race, crossings, EmptyRegistrations, EmptyBikers, EmptyCategories);

        Assert.Single(standings);
        Assert.Equal(1, standings[0].BikerIdentifier);
    }

    [Fact]
    public void ComputeStandings_EmptyCrossingList_ReturnsEmptyStandings()
    {
        var service = new StandingsCalculatorService();
        var race = new Race { Identifier = 1, Name = "Test Race", StartTicks = 100 };

        var standings = service.ComputeStandings(race, new List<Crossing>(), EmptyRegistrations, EmptyBikers, EmptyCategories);

        Assert.Empty(standings);
    }

    [Fact]
    public void ComputeStandings_RaceNotYetStarted_LeavesRaceTimeEmpty()
    {
        var service = new StandingsCalculatorService();
        var race = new Race { Identifier = 1, Name = "Test Race", StartTicks = 0 };

        var crossings = new List<Crossing>
        {
            new() { BikerIdentifier = 1, RaceIdentifier = 1, SequenceIndex = 1, Ticks = TimeSpan.FromMinutes(10).Ticks },
        };

        var standings = service.ComputeStandings(race, crossings, EmptyRegistrations, EmptyBikers, EmptyCategories);

        Assert.Equal(string.Empty, standings[0].RaceTime);
    }

    [Fact]
    public void ComputeStandings_RaceStarted_ComputesRaceTimeRelativeToStartTicks()
    {
        var service = new StandingsCalculatorService();
        const long raceStartTicks = 500;
        var race = new Race { Identifier = 1, Name = "Test Race", StartTicks = raceStartTicks };

        var crossings = new List<Crossing>
        {
            new() { BikerIdentifier = 1, RaceIdentifier = 1, SequenceIndex = 1, Ticks = raceStartTicks + TimeSpan.FromMinutes(45).Ticks },
        };

        var standings = service.ComputeStandings(race, crossings, EmptyRegistrations, EmptyBikers, EmptyCategories);

        Assert.Equal("0:45:00", standings[0].RaceTime);
    }

    [Fact]
    public void RankWithinCategory_RenumbersPositionsFromOne()
    {
        var service = new StandingsCalculatorService();
        const long baseTicks = 1000;

        var categoryEntries = new List<StandingEntry>
        {
            new() { BikerIdentifier = 1, Position = 3, CompletedLaps = 4, Ticks = baseTicks + TimeSpan.FromMinutes(30).Ticks },
            new() { BikerIdentifier = 2, Position = 5, CompletedLaps = 4, Ticks = baseTicks + TimeSpan.FromMinutes(31).Ticks },
            new() { BikerIdentifier = 3, Position = 8, CompletedLaps = 4, Ticks = baseTicks + TimeSpan.FromMinutes(32).Ticks },
        };

        var rankedEntries = service.RankWithinCategory(categoryEntries, 0L);

        Assert.Equal(3, rankedEntries.Count);
        Assert.Equal(1, rankedEntries[0].Position);
        Assert.Equal(1, rankedEntries[0].BikerIdentifier);
        Assert.Equal(2, rankedEntries[1].Position);
        Assert.Equal(2, rankedEntries[1].BikerIdentifier);
        Assert.Equal(3, rankedEntries[2].Position);
        Assert.Equal(3, rankedEntries[2].BikerIdentifier);
    }

    [Fact]
    public void RankWithinCategory_RecomputesGapRelativeToCategoryLeader()
    {
        var service = new StandingsCalculatorService();
        const long baseTicks = 1000;
        var leaderTicks = baseTicks + TimeSpan.FromMinutes(30).Ticks;
        var secondTicks = leaderTicks + TimeSpan.FromSeconds(5).Ticks;

        var categoryEntries = new List<StandingEntry>
        {
            new() { BikerIdentifier = 1, Position = 3, CompletedLaps = 4, Ticks = leaderTicks },
            new() { BikerIdentifier = 2, Position = 5, CompletedLaps = 4, Ticks = secondTicks },
        };

        var rankedEntries = service.RankWithinCategory(categoryEntries, 0L);

        Assert.Equal(string.Empty, rankedEntries[0].Gap);
        Assert.Equal("+0:05", rankedEntries[1].Gap);
    }

    [Fact]
    public void ComputeStandings_GapMatchesDifferenceOfWholeSecondRaceTimes()
    {
        var service = new StandingsCalculatorService();
        const long raceStartTicks = 100;
        var race = new Race { Identifier = 1, Name = "Test Race", StartTicks = raceStartTicks };

        // Two riders, same lap count. The leader's elapsed time is 44.9 s and the follower's is
        // 50.1 s, so the displayed race times floor to 0:00:44 and 0:00:50 and the gap must read
        // "+0:06" (50 - 44), not the "+0:05" that flooring the raw 5.2 s difference would give.
        var crossings = new List<Crossing>
        {
            new() { BikerIdentifier = 1, RaceIdentifier = 1, SequenceIndex = 1, Ticks = raceStartTicks + (long)(44.9 * TimeSpan.TicksPerSecond) },
            new() { BikerIdentifier = 2, RaceIdentifier = 1, SequenceIndex = 2, Ticks = raceStartTicks + (long)(50.1 * TimeSpan.TicksPerSecond) },
        };

        var standings = service.ComputeStandings(race, crossings, EmptyRegistrations, EmptyBikers, EmptyCategories);

        Assert.Equal("0:00:44", standings[0].RaceTime);
        Assert.Equal("0:00:50", standings[1].RaceTime);
        Assert.Equal(string.Empty, standings[0].Gap);
        Assert.Equal("+0:06", standings[1].Gap);
    }
}
