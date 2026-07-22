namespace JolimontBikeRace.Core.Models;

/// <summary>
/// Represents a single race event, for example "Jolimont Bike Race 2016 - Adultes". This class
/// mirrors the "race" table of the PostgreSQL schema.
/// </summary>
public class Race
{
    /// <summary>
    /// Gets or sets the unique database identifier of the race.
    /// </summary>
    public long Identifier { get; set; }

    /// <summary>
    /// Gets or sets the name of the race. Defaults to an empty string so that the property is
    /// never null, which simplifies binding it directly to text boxes in the user interface.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the start instant of the race, expressed as .NET ticks (see
    /// <see cref="System.DateTime.Ticks"/>). A value of zero means that the race has not started
    /// yet. This property maps to the "racetick" column of the database.
    /// </summary>
    public long StartTicks { get; set; }

    /// <summary>
    /// Gets a value indicating whether the race has already been started, that is, whether
    /// <see cref="StartTicks"/> holds a strictly positive value.
    /// </summary>
    public bool HasStarted => StartTicks > 0;
}
