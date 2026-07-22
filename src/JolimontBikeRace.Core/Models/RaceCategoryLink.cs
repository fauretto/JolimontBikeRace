namespace JolimontBikeRace.Core.Models;

/// <summary>
/// Represents the association between a race and a category, indicating that the category is
/// offered within that race. This class mirrors the "race_category" table of the PostgreSQL
/// schema.
/// </summary>
public class RaceCategoryLink
{
    /// <summary>
    /// Gets or sets the unique database identifier of the link.
    /// </summary>
    public long Identifier { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the race that the category is linked to.
    /// </summary>
    public long RaceIdentifier { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the category that is linked to the race.
    /// </summary>
    public long CategoryIdentifier { get; set; }
}
