using System.Collections.ObjectModel;
using JolimontBikeRace.Core.Interfaces;
using JolimontBikeRace.Core.Models;

namespace JolimontBikeRace.Core.Services;

/// <summary>
/// Owns the single shared list of races used by every section of the application, so that
/// creating, deleting, duplicating or renaming a race in one section is immediately visible in
/// all the others.
/// </summary>
public class RaceCollectionService : IRaceCollectionService
{
    private readonly IRaceRepository _raceRepository;
    private readonly ILogService _logService;

    /// <summary>
    /// Initializes a new instance of the <see cref="RaceCollectionService"/> class.
    /// </summary>
    /// <param name="raceRepository">The repository used to reload the list of races from the database.</param>
    /// <param name="logService">The logging service used to record reload outcomes.</param>
    public RaceCollectionService(IRaceRepository raceRepository, ILogService logService)
    {
        _raceRepository = raceRepository;
        _logService = logService;
    }

    /// <summary>
    /// Gets the single shared list of races. Every view model exposes this same collection to its
    /// view, so changes made to it propagate to every race list and combo box at once.
    /// </summary>
    public ObservableCollection<Race> Races { get; } = new();

    /// <summary>
    /// Reloads the list of races from the database, replacing the contents of
    /// <see cref="Races"/> in place so that existing bindings stay attached.
    /// </summary>
    /// <remarks>
    /// This method must be called from the user interface thread, because it mutates a bound
    /// collection.
    /// </remarks>
    public async Task ReloadAsync()
    {
        try
        {
            var races = await _raceRepository.GetAllAsync();
            Races.Clear();
            foreach (var race in races)
            {
                Races.Add(race);
            }

            _logService.Information("RaceCollectionService -> ReloadAsync", $"loaded {Races.Count} races");
        }
        catch (Exception exception)
        {
            _logService.Error("RaceCollectionService -> ReloadAsync", "failed to reload the list of races", exception);
            throw;
        }
    }
}
