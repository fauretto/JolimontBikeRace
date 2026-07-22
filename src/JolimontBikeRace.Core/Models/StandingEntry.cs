namespace JolimontBikeRace.Core.Models;

/// <summary>
/// Represents a single row of the computed final classification of a race, that is, the
/// resolved position, elapsed race time and gap to the leader for one biker. This class mirrors
/// the "standing" table of the PostgreSQL schema, and additionally carries a few properties
/// that are useful for display purposes but are not persisted to that table.
/// </summary>
public class StandingEntry
{
    /// <summary>
    /// Gets or sets the unique database identifier of the standing entry.
    /// </summary>
    public long Identifier { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the biker that this standing entry describes.
    /// </summary>
    public long BikerIdentifier { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the race that this standing entry belongs to.
    /// </summary>
    public long RaceIdentifier { get; set; }

    /// <summary>
    /// Gets or sets the final position of the biker in the race, where one is the winner. This
    /// property maps to the "raceposition" column of the database.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// Gets or sets the instant of the biker's last recorded crossing, expressed as .NET ticks
    /// (see <see cref="System.DateTime.Ticks"/>).
    /// </summary>
    public long Ticks { get; set; }

    /// <summary>
    /// Gets or sets the formatted elapsed race time of the biker, for example "1:23:45".
    /// </summary>
    public string? RaceTime { get; set; }

    /// <summary>
    /// Gets or sets the formatted gap between the biker and the race leader, for example
    /// "+1:02" or "+1 lap".
    /// </summary>
    public string? Gap { get; set; }

    /// <summary>
    /// Gets or sets the number of laps completed by the biker. This property is not persisted
    /// to the database, it is a display helper computed while building the standings.
    /// </summary>
    public int CompletedLaps { get; set; }

    /// <summary>
    /// Gets or sets the full name of the biker. This property is not persisted to the database,
    /// it is a display helper computed while building the standings.
    /// </summary>
    public string? BikerFullName { get; set; }

    /// <summary>
    /// Gets or sets the bib number of the biker for this race. This property is not persisted
    /// to the database, it is a display helper computed while building the standings.
    /// </summary>
    public int? BibNumber { get; set; }

    /// <summary>
    /// Gets or sets the name of the category that the biker was registered in for this race.
    /// This property is not persisted to the database, it is a display helper computed while
    /// building the standings.
    /// </summary>
    public string? CategoryName { get; set; }
}
