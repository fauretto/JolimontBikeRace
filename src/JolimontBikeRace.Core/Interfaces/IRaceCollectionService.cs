using System.Collections.ObjectModel;
using JolimontBikeRace.Core.Models;

namespace JolimontBikeRace.Core.Interfaces;

/// <summary>
/// Owns the single shared list of races used by every section of the application, so that
/// creating, deleting, duplicating or renaming a race in one section is immediately visible in
/// all the others.
/// </summary>
public interface IRaceCollectionService
{
    /// <summary>
    /// Gets the single shared list of races. Every view model exposes this same collection to its
    /// view, so changes made to it propagate to every race list and combo box at once.
    /// </summary>
    ObservableCollection<Race> Races { get; }

    /// <summary>
    /// Reloads the list of races from the database, replacing the contents of
    /// <see cref="Races"/> in place so that existing bindings stay attached.
    /// </summary>
    Task ReloadAsync();
}
