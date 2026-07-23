using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using JolimontBikeRace.Core.Interfaces;
using JolimontBikeRace.Core.Models;

namespace JolimontBikeRace.App.ViewModels;

/// <summary>
/// Acts as the top-level view model of the application: it hosts navigation between the four
/// section view models and owns the state that is shared across every section, namely the list
/// of races, the currently active race, the database connection indicator and the status bar
/// text.
/// </summary>
public class ShellViewModel : ViewModelBase
{
    private readonly IDatabaseConnectionService _databaseConnectionService;
    private readonly IRaceCollectionService _raceCollectionService;

    private ViewModelBase _currentViewModel;
    private Race? _selectedRace;
    private bool _isDatabaseConnected;
    private string _statusMessage = string.Empty;
    private DateTime? _lastSaveTime;

    /// <summary>
    /// Initializes a new instance of the <see cref="ShellViewModel"/> class.
    /// </summary>
    /// <param name="databaseConnectionService">The service used to verify database connectivity.</param>
    /// <param name="raceCollectionService">The service that owns the single shared list of races.</param>
    /// <param name="raceManagerViewModel">The Race Manager section view model.</param>
    /// <param name="bikersViewModel">The Bikers section view model.</param>
    /// <param name="chronoViewModel">The Chrono section view model.</param>
    /// <param name="standingsViewModel">The Standings section view model.</param>
    /// <param name="logService">The logging service used to record navigation and startup events.</param>
    public ShellViewModel(
        IDatabaseConnectionService databaseConnectionService,
        IRaceCollectionService raceCollectionService,
        RaceManagerViewModel raceManagerViewModel,
        BikersViewModel bikersViewModel,
        ChronoViewModel chronoViewModel,
        StandingsViewModel standingsViewModel,
        ILogService logService)
        : base(logService)
    {
        _databaseConnectionService = databaseConnectionService;
        _raceCollectionService = raceCollectionService;

        RaceManagerViewModel = raceManagerViewModel;
        BikersViewModel = bikersViewModel;
        ChronoViewModel = chronoViewModel;
        StandingsViewModel = standingsViewModel;

        Title = "Jolimont Bike Race";

        _currentViewModel = raceManagerViewModel;

        ShowRaceManagerCommand = new RelayCommand(() => CurrentViewModel = RaceManagerViewModel);
        ShowBikersCommand = new RelayCommand(() => CurrentViewModel = BikersViewModel);
        ShowChronoCommand = new RelayCommand(() => CurrentViewModel = ChronoViewModel);
        ShowStandingsCommand = new RelayCommand(() => CurrentViewModel = StandingsViewModel);
        ExitCommand = new RelayCommand(() => Application.Current.Shutdown());
    }

    /// <summary>
    /// Gets the Race Manager section view model.
    /// </summary>
    public RaceManagerViewModel RaceManagerViewModel { get; }

    /// <summary>
    /// Gets the Bikers section view model.
    /// </summary>
    public BikersViewModel BikersViewModel { get; }

    /// <summary>
    /// Gets the Chrono section view model.
    /// </summary>
    public ChronoViewModel ChronoViewModel { get; }

    /// <summary>
    /// Gets the Standings section view model.
    /// </summary>
    public StandingsViewModel StandingsViewModel { get; }

    /// <summary>
    /// Gets or sets the view model of the section that is currently displayed in the main
    /// content area of the shell window.
    /// </summary>
    public ViewModelBase CurrentViewModel
    {
        get => _currentViewModel;
        set => SetProperty(ref _currentViewModel, value);
    }

    /// <summary>
    /// Gets the single shared list of races owned by <see cref="IRaceCollectionService"/>.
    /// </summary>
    public ObservableCollection<Race> Races => _raceCollectionService.Races;

    /// <summary>
    /// Gets or sets the race that is currently active, shared by every section through the top
    /// header combo box.
    /// </summary>
    public Race? SelectedRace
    {
        get => _selectedRace;
        set
        {
            if (SetProperty(ref _selectedRace, value))
            {
                RaceManagerViewModel.SelectedRace = value;
                BikersViewModel.SelectedRegistrationRace = value;
                ChronoViewModel.SelectedRace = value;
                StandingsViewModel.SelectedRace = value;
            }
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the application is currently connected to the
    /// database, shown as a green or red indicator in the status bar.
    /// </summary>
    public bool IsDatabaseConnected
    {
        get => _isDatabaseConnected;
        set => SetProperty(ref _isDatabaseConnected, value);
    }

    /// <summary>
    /// Gets or sets the current status message shown in the status bar.
    /// </summary>
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>
    /// Gets or sets the instant of the last successful autosave or database save, shown in the
    /// status bar.
    /// </summary>
    public DateTime? LastSaveTime
    {
        get => _lastSaveTime;
        set => SetProperty(ref _lastSaveTime, value);
    }

    /// <summary>
    /// Gets the command that navigates to the Race Manager section.
    /// </summary>
    public RelayCommand ShowRaceManagerCommand { get; }

    /// <summary>
    /// Gets the command that navigates to the Bikers section.
    /// </summary>
    public RelayCommand ShowBikersCommand { get; }

    /// <summary>
    /// Gets the command that navigates to the Chrono section.
    /// </summary>
    public RelayCommand ShowChronoCommand { get; }

    /// <summary>
    /// Gets the command that navigates to the Standings section.
    /// </summary>
    public RelayCommand ShowStandingsCommand { get; }

    /// <summary>
    /// Gets the command that shuts down the application.
    /// </summary>
    public RelayCommand ExitCommand { get; }

    /// <summary>
    /// Loads the list of races and verifies database connectivity. This method is intended to be
    /// called once at application startup, without being awaited by the caller, so that a slow or
    /// unreachable database never delays the display of the main window.
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            IsDatabaseConnected = await _databaseConnectionService.TestConnectionAsync();
            StatusMessage = IsDatabaseConnected ? "Connected to database." : "Database unreachable.";

            await _raceCollectionService.ReloadAsync();

            SelectedRace ??= Races.FirstOrDefault();

            LogService.Information("ShellViewModel -> InitializeAsync", $"loaded {Races.Count} races, database connected: {IsDatabaseConnected}");
        }
        catch (Exception exception)
        {
            LogService.Error("ShellViewModel -> InitializeAsync", "failed to initialize the shell view model", exception);
            StatusMessage = "Failed to load initial data.";
        }
    }
}
