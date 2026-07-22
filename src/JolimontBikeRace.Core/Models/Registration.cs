namespace JolimontBikeRace.Core.Models;

/// <summary>
/// Represents the registration of a biker into a race, within an optional category, together
/// with the bib number that has been assigned to the biker for that race. This class mirrors
/// the "biker_race_category" table of the PostgreSQL schema.
/// </summary>
public class Registration
{
    /// <summary>
    /// Gets or sets the unique database identifier of the registration.
    /// </summary>
    public long Identifier { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the biker who is registered.
    /// </summary>
    public long BikerIdentifier { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the race that the biker is registered for.
    /// </summary>
    public long RaceIdentifier { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the category that the biker is registered in, or null if
    /// no category has been assigned.
    /// </summary>
    public long? CategoryIdentifier { get; set; }

    /// <summary>
    /// Gets or sets the bib number assigned to the biker for this race, or null if no bib
    /// number has been assigned yet.
    /// </summary>
    public int? BibNumber { get; set; }
}
