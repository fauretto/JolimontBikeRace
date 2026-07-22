namespace JolimontBikeRace.Core.Models;

/// <summary>
/// Represents a registered biker, that is, a person who may take part in one or several races.
/// This class mirrors the "biker" table of the PostgreSQL schema.
/// </summary>
public class Biker
{
    /// <summary>
    /// Gets or sets the unique database identifier of the biker.
    /// </summary>
    public long Identifier { get; set; }

    /// <summary>
    /// Gets or sets the first name of the biker.
    /// </summary>
    public string? FirstName { get; set; }

    /// <summary>
    /// Gets or sets the last name of the biker.
    /// </summary>
    public string? LastName { get; set; }

    /// <summary>
    /// Gets or sets the year in which the biker was born.
    /// </summary>
    public int? YearOfBirth { get; set; }

    /// <summary>
    /// Gets or sets the postal address of the biker.
    /// </summary>
    public string? Address { get; set; }

    /// <summary>
    /// Gets or sets the electronic mail address of the biker.
    /// </summary>
    public string? ElectronicMailAddress { get; set; }

    /// <summary>
    /// Gets or sets the fixed telephone number of the biker.
    /// </summary>
    public string? Telephone { get; set; }

    /// <summary>
    /// Gets or sets the mobile telephone number of the biker. This property maps to the
    /// "natel" column of the database, which is the Swiss French term for a mobile telephone.
    /// </summary>
    public string? MobileTelephone { get; set; }

    /// <summary>
    /// Gets the full name of the biker, computed by concatenating the first name and the last
    /// name separated by a single space. This property is not stored in the database.
    /// </summary>
    public string FullName => $"{FirstName} {LastName}".Trim();
}
