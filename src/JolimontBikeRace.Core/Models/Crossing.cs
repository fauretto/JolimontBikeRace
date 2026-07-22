namespace JolimontBikeRace.Core.Models;

/// <summary>
/// Represents a single finish-line crossing captured during the timing of a race. This class
/// mirrors the "race_standings" table of the PostgreSQL schema, which stores the raw, unordered
/// timing ticks as they are recorded, before any final classification is computed.
/// </summary>
public class Crossing
{
    /// <summary>
    /// Gets or sets the unique database identifier of the crossing.
    /// </summary>
    public long Identifier { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the biker who crossed the finish line. A value of zero
    /// means that the bib number could not be resolved at the time of capture, that is, the
    /// crossing has not been assigned to a biker yet.
    /// </summary>
    public long BikerIdentifier { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the race during which the crossing was captured.
    /// </summary>
    public long RaceIdentifier { get; set; }

    /// <summary>
    /// Gets or sets the sequence index of the crossing, that is, the order in which it was
    /// captured relative to the other crossings of the same race. This property maps to the
    /// "tickindex" column of the database.
    /// </summary>
    public long SequenceIndex { get; set; }

    /// <summary>
    /// Gets or sets the instant at which the crossing was captured, expressed as .NET ticks
    /// (see <see cref="System.DateTime.Ticks"/>). This property maps to the "tick" column of
    /// the database.
    /// </summary>
    public long Ticks { get; set; }
}
