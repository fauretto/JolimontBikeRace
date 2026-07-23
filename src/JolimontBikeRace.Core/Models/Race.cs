using System.ComponentModel;

namespace JolimontBikeRace.Core.Models;

/// <summary>
/// Represents a single race event, for example "Jolimont Bike Race 2016 - Adultes". This class
/// mirrors the "race" table of the PostgreSQL schema. It raises change notifications so that user
/// interface elements bound to a race update when it is renamed or started.
/// </summary>
public class Race : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private long _startTicks;

    /// <summary>
    /// Occurs when the value of a property of this race changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Gets or sets the unique database identifier of the race.
    /// </summary>
    public long Identifier { get; set; }

    /// <summary>
    /// Gets or sets the name of the race. Defaults to an empty string so that the property is
    /// never null, which simplifies binding it directly to text boxes in the user interface.
    /// </summary>
    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                RaisePropertyChanged(nameof(Name));
            }
        }
    }

    /// <summary>
    /// Gets or sets the start instant of the race, expressed as .NET ticks (see
    /// <see cref="System.DateTime.Ticks"/>). A value of zero means that the race has not started
    /// yet. This property maps to the "racetick" column of the database.
    /// </summary>
    public long StartTicks
    {
        get => _startTicks;
        set
        {
            if (_startTicks != value)
            {
                _startTicks = value;
                RaisePropertyChanged(nameof(StartTicks));
                RaisePropertyChanged(nameof(HasStarted));
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the race has already been started, that is, whether
    /// <see cref="StartTicks"/> holds a strictly positive value.
    /// </summary>
    public bool HasStarted => StartTicks > 0;

    /// <summary>
    /// Raises the <see cref="PropertyChanged"/> event for the property with the given name.
    /// </summary>
    /// <param name="propertyName">The name of the property whose value changed.</param>
    private void RaisePropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
