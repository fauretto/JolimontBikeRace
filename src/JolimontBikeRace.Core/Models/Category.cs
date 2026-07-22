namespace JolimontBikeRace.Core.Models;

/// <summary>
/// Represents a rider category, for example an age or gender group, together with the range of
/// bib numbers reserved for that category. This class mirrors the "category" table of the
/// PostgreSQL schema.
/// </summary>
public class Category
{
    /// <summary>
    /// Gets or sets the unique database identifier of the category.
    /// </summary>
    public long Identifier { get; set; }

    /// <summary>
    /// Gets or sets the name of the category.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the smallest bib number that may be assigned to a rider of this category.
    /// </summary>
    public int MinimumBibNumber { get; set; }

    /// <summary>
    /// Gets or sets the largest bib number that may be assigned to a rider of this category.
    /// </summary>
    public int MaximumBibNumber { get; set; }
}
