using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JolimontBikeRace.Core.Helpers;
using JolimontBikeRace.Core.Interfaces;
using JolimontBikeRace.Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Win32;

namespace JolimontBikeRace.App.ViewModels;

/// <summary>
/// Supports the Chrono section of the application, the keyboard-first timing screen used while a
/// race is in progress to record finish-line crossings as they happen.
/// </summary>
public class ChronoViewModel : ViewModelBase
{
    private readonly IRaceRepository _raceRepository;
    private readonly ICrossingRepository _crossingRepository;
    private readonly IRegistrationRepository _registrationRepository;
    private readonly IBikerRepository _bikerRepository;
    private readonly IRaceStandingsJournalService _journalService;
    private readonly IRaceCollectionService _raceCollectionService;
    private readonly string _journalFolderPath;

    private readonly DispatcherTimer _clockTimer;

    // These dictionaries hold the context of the currently selected race: the bib number and
    // biker identifier resolved from every registration, and the running lap count computed as
    // crossings are recorded, keyed by biker identifier.
    private Dictionary<int, Registration> _registrationsByBibNumber = new();
    private Dictionary<long, Biker> _bikersByIdentifier = new();
    private Dictionary<long, int> _lapCountByBikerIdentifier = new();
    private Dictionary<long, int> _bibNumberByBikerIdentifier = new();

    private long _nextSequenceIndex = 1;

    private Race? _selectedRace;
    private bool _isRaceRunning;
    private string _elapsedTime = "0:00:00";
    private string _currentTimeOfDay = string.Empty;
    private string _bibNumberInputText = string.Empty;
    private string _autosaveStatusText = string.Empty;
    private DateTime? _lastSaveTime;

    /// <summary>
    /// Initializes a new instance of the <see cref="ChronoViewModel"/> class.
    /// </summary>
    /// <param name="raceRepository">The repository used to record race start instants.</param>
    /// <param name="crossingRepository">The repository used to record, update and delete crossings.</param>
    /// <param name="registrationRepository">The repository used to load the registrations of the selected race.</param>
    /// <param name="bikerRepository">The repository used to resolve biker full names.</param>
    /// <param name="journalService">The service used to read and write the XML race standings journal.</param>
    /// <param name="raceCollectionService">The service that owns the single shared list of races.</param>
    /// <param name="configuration">The application configuration, used to resolve the journal folder path.</param>
    /// <param name="logService">The logging service used to record every timing operation.</param>
    public ChronoViewModel(
        IRaceRepository raceRepository,
        ICrossingRepository crossingRepository,
        IRegistrationRepository registrationRepository,
        IBikerRepository bikerRepository,
        IRaceStandingsJournalService journalService,
        IRaceCollectionService raceCollectionService,
        IConfiguration configuration,
        ILogService logService)
        : base(logService)
    {
        _raceRepository = raceRepository;
        _crossingRepository = crossingRepository;
        _registrationRepository = registrationRepository;
        _bikerRepository = bikerRepository;
        _journalService = journalService;
        _raceCollectionService = raceCollectionService;
        _journalFolderPath = configuration["Journal:FolderPath"] ?? "Journal";

        Title = "Chrono";
        Crossings = new ObservableCollection<CrossingRow>();

        StartRaceCommand = new AsyncRelayCommand(StartRaceAsync, () => SelectedRace is not null);
        RecordCrossingCommand = new AsyncRelayCommand(() => RecordCrossingAsync(), () => SelectedRace is not null && SelectedRace.HasStarted);
        RecordUnassignedCrossingCommand = new AsyncRelayCommand(() => RecordCrossingAsync(forceUnassigned: true), () => SelectedRace is not null && SelectedRace.HasStarted);
        UndoLastCrossingCommand = new AsyncRelayCommand(UndoLastCrossingAsync, () => Crossings.Count > 0);
        AssignBibNumberCommand = new AsyncRelayCommand<CrossingRow?>(AssignBibNumberAsync);
        ResetRaceCommand = new AsyncRelayCommand(ResetRaceAsync, () => SelectedRace is not null);
        ResetRaceStandingsCommand = new AsyncRelayCommand(ResetRaceStandingsAsync, () => SelectedRace is not null);
        LoadJournalCommand = new AsyncRelayCommand(LoadJournalAsync, () => SelectedRace is not null);
        ForceDatabaseSynchronizationCommand = new AsyncRelayCommand(ForceDatabaseSynchronizationAsync, () => SelectedRace is not null);

        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _clockTimer.Tick += (_, _) => OnClockTick();
        _clockTimer.Start();
    }

    /// <summary>
    /// Gets the single shared list of races owned by <see cref="IRaceCollectionService"/>.
    /// </summary>
    public ObservableCollection<Race> Races => _raceCollectionService.Races;

    /// <summary>
    /// Gets the list of recorded crossings, shown newest first.
    /// </summary>
    public ObservableCollection<CrossingRow> Crossings { get; }

    /// <summary>
    /// Gets or sets the race that the timing screen currently operates on.
    /// </summary>
    public Race? SelectedRace
    {
        get => _selectedRace;
        set
        {
            if (SetProperty(ref _selectedRace, value))
            {
                StartRaceCommand.NotifyCanExecuteChanged();
                RecordCrossingCommand.NotifyCanExecuteChanged();
                RecordUnassignedCrossingCommand.NotifyCanExecuteChanged();
                ResetRaceCommand.NotifyCanExecuteChanged();
                ResetRaceStandingsCommand.NotifyCanExecuteChanged();
                LoadJournalCommand.NotifyCanExecuteChanged();
                ForceDatabaseSynchronizationCommand.NotifyCanExecuteChanged();
                _ = LoadRaceContextAsync();
            }
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the currently selected race has been started.
    /// </summary>
    public bool IsRaceRunning
    {
        get => _isRaceRunning;
        set => SetProperty(ref _isRaceRunning, value);
    }

    /// <summary>
    /// Gets or sets the formatted elapsed race time, refreshed every 200 milliseconds while the
    /// race is running.
    /// </summary>
    public string ElapsedTime
    {
        get => _elapsedTime;
        set => SetProperty(ref _elapsedTime, value);
    }

    /// <summary>
    /// Gets or sets the formatted current time of day, refreshed every 200 milliseconds.
    /// </summary>
    public string CurrentTimeOfDay
    {
        get => _currentTimeOfDay;
        set => SetProperty(ref _currentTimeOfDay, value);
    }

    /// <summary>
    /// Gets or sets the text currently typed into the prominent bib-number input box.
    /// </summary>
    public string BibNumberInputText
    {
        get => _bibNumberInputText;
        set => SetProperty(ref _bibNumberInputText, value);
    }

    /// <summary>
    /// Gets or sets a short status text describing the outcome of the last autosave or database
    /// synchronization.
    /// </summary>
    public string AutosaveStatusText
    {
        get => _autosaveStatusText;
        set => SetProperty(ref _autosaveStatusText, value);
    }

    /// <summary>
    /// Gets or sets the instant of the last successful autosave to the XML journal.
    /// </summary>
    public DateTime? LastSaveTime
    {
        get => _lastSaveTime;
        set => SetProperty(ref _lastSaveTime, value);
    }

    /// <summary>
    /// Gets the command that starts the currently selected race, recording the current instant as
    /// its official start time.
    /// </summary>
    public AsyncRelayCommand StartRaceCommand { get; }

    /// <summary>
    /// Gets the command that records a crossing for the bib number currently typed into the input
    /// box.
    /// </summary>
    public AsyncRelayCommand RecordCrossingCommand { get; }

    /// <summary>
    /// Gets the command that records a crossing without an associated bib number.
    /// </summary>
    public AsyncRelayCommand RecordUnassignedCrossingCommand { get; }

    /// <summary>
    /// Gets the command that deletes the most recently recorded crossing.
    /// </summary>
    public AsyncRelayCommand UndoLastCrossingCommand { get; }

    /// <summary>
    /// Gets the command that assigns a bib number to a crossing that was recorded without one.
    /// </summary>
    public AsyncRelayCommand<CrossingRow?> AssignBibNumberCommand { get; }

    /// <summary>
    /// Gets the command that fully resets the selected race, clearing both its start time and
    /// every recorded crossing.
    /// </summary>
    public AsyncRelayCommand ResetRaceCommand { get; }

    /// <summary>
    /// Gets the command that clears every recorded crossing of the selected race while leaving
    /// its start time untouched.
    /// </summary>
    public AsyncRelayCommand ResetRaceStandingsCommand { get; }

    /// <summary>
    /// Gets the command that reloads the crossings of the selected race from a previously saved
    /// XML journal file.
    /// </summary>
    public AsyncRelayCommand LoadJournalCommand { get; }

    /// <summary>
    /// Gets the command that explicitly commits every crossing currently held in memory to the
    /// database.
    /// </summary>
    public AsyncRelayCommand ForceDatabaseSynchronizationCommand { get; }

    private void OnClockTick()
    {
        CurrentTimeOfDay = TickFormattingHelper.FormatTimeOfDay(DateTime.Now.Ticks);

        if (SelectedRace is { HasStarted: true })
        {
            ElapsedTime = TickFormattingHelper.FormatElapsedTime(DateTime.Now.Ticks - SelectedRace.StartTicks);
        }
    }

    /// <summary>
    /// Loads the registrations, bikers and existing crossings of the currently selected race.
    /// When the race registrations contain duplicated bib numbers, this method tolerates the
    /// duplication by keeping only the first registration of each duplicated bib number for
    /// timing purposes, and warns the user that the duplicated registrations must be corrected.
    /// </summary>
    private async Task LoadRaceContextAsync()
    {
        Crossings.Clear();
        UndoLastCrossingCommand.NotifyCanExecuteChanged();
        _lapCountByBikerIdentifier = new Dictionary<long, int>();
        _bibNumberByBikerIdentifier = new Dictionary<long, int>();
        _registrationsByBibNumber = new Dictionary<int, Registration>();
        _bikersByIdentifier = new Dictionary<long, Biker>();
        _nextSequenceIndex = 1;

        if (SelectedRace is null)
        {
            IsRaceRunning = false;
            return;
        }

        IsRaceRunning = SelectedRace.HasStarted;

        try
        {
            var registrations = await _registrationRepository.GetForRaceAsync(SelectedRace.Identifier);
            var registrationsWithBibNumber = registrations
                .Where(registration => registration.BibNumber.HasValue)
                .ToList();

            _registrationsByBibNumber = registrationsWithBibNumber
                .GroupBy(registration => registration.BibNumber!.Value)
                .ToDictionary(group => group.Key, group => group.First());
            _bibNumberByBikerIdentifier = registrationsWithBibNumber
                .GroupBy(registration => registration.BikerIdentifier)
                .ToDictionary(group => group.Key, group => group.First().BibNumber!.Value);

            var duplicatedBibNumbers = registrationsWithBibNumber
                .GroupBy(registration => registration.BibNumber!.Value)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .OrderBy(bibNumber => bibNumber)
                .ToList();
            if (duplicatedBibNumbers.Count > 0)
            {
                LogService.Warning("ChronoViewModel -> LoadRaceContextAsync", $"race {SelectedRace.Identifier} contains duplicated bib numbers: {string.Join(", ", duplicatedBibNumbers)}");
                MessageBox.Show(
                    $"The registrations of race \"{SelectedRace.Name}\" contain duplicated bib numbers: "
                    + $"{string.Join(", ", duplicatedBibNumbers)}. Only the first registration of each "
                    + "duplicated bib number will be used for timing. Please correct the registrations.",
                    "Duplicated Bib Numbers",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }

            var bikers = await _bikerRepository.GetAllAsync();
            _bikersByIdentifier = bikers.ToDictionary(biker => biker.Identifier);

            var existingCrossings = await _crossingRepository.GetForRaceAsync(SelectedRace.Identifier);
            LoadCrossingsIntoUi(existingCrossings);
        }
        catch (Exception exception)
        {
            LogService.Error("ChronoViewModel -> LoadRaceContextAsync", $"failed to load timing context for race {SelectedRace.Identifier}", exception);
        }
    }

    private void LoadCrossingsIntoUi(IReadOnlyList<Crossing> crossings)
    {
        Crossings.Clear();
        _lapCountByBikerIdentifier = new Dictionary<long, int>();

        // Crossings are processed in chronological order (ascending sequence index) so that lap
        // counts accumulate correctly, then the resulting rows are inserted newest first.
        var orderedCrossings = crossings.OrderBy(crossing => crossing.SequenceIndex).ToList();
        foreach (var crossing in orderedCrossings)
        {
            var row = BuildRowForCrossing(crossing);
            Crossings.Insert(0, row);
        }

        _nextSequenceIndex = orderedCrossings.Count == 0 ? 1 : orderedCrossings.Max(crossing => crossing.SequenceIndex) + 1;

        UndoLastCrossingCommand.NotifyCanExecuteChanged();
    }

    private CrossingRow BuildRowForCrossing(Crossing crossing)
    {
        var isAssigned = crossing.BikerIdentifier != 0;
        int lapNumber = 0;
        string? bikerFullName = null;

        if (isAssigned)
        {
            _lapCountByBikerIdentifier.TryGetValue(crossing.BikerIdentifier, out var previousLapCount);
            lapNumber = previousLapCount + 1;
            _lapCountByBikerIdentifier[crossing.BikerIdentifier] = lapNumber;

            _bikersByIdentifier.TryGetValue(crossing.BikerIdentifier, out var biker);
            bikerFullName = biker?.FullName;
        }

        var raceTimeText = SelectedRace is { HasStarted: true }
            ? TickFormattingHelper.FormatElapsedTime(crossing.Ticks - SelectedRace.StartTicks)
            : string.Empty;

        _bibNumberByBikerIdentifier.TryGetValue(crossing.BikerIdentifier, out var bibNumber);

        return new CrossingRow(crossing)
        {
            TimeOfDayText = TickFormattingHelper.FormatTimeOfDay(crossing.Ticks),
            RaceTimeText = raceTimeText,
            BibNumberText = isAssigned ? bibNumber.ToString() : string.Empty,
            BikerFullName = bikerFullName,
            LapNumber = lapNumber,
            IsBibNumberAssigned = isAssigned,
        };
    }

    private async Task StartRaceAsync()
    {
        if (SelectedRace is null)
        {
            return;
        }

        if (Crossings.Count > 0)
        {
            var confirmationResult = MessageBox.Show(
                "This race already has recorded crossings. Starting it again will reset the elapsed time reference for every rider. Continue?",
                "Confirm Race Start",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (confirmationResult != MessageBoxResult.Yes)
            {
                return;
            }
        }

        try
        {
            var startTicks = DateTime.Now.Ticks;
            await _raceRepository.UpdateStartTicksAsync(SelectedRace.Identifier, startTicks);
            SelectedRace.StartTicks = startTicks;
            IsRaceRunning = true;

            var journalFilePath = GetStartDateTimeFilePath(SelectedRace);
            _journalService.WriteStartDateTime(journalFilePath, SelectedRace);

            // This is the critical section that records the official start instant of the race.
            LogService.Information("ChronoViewModel -> StartRaceAsync", $"race {SelectedRace.Identifier} started at tick {startTicks}");
        }
        catch (Exception exception)
        {
            LogService.Error("ChronoViewModel -> StartRaceAsync", $"failed to start race {SelectedRace.Identifier}", exception);
        }
    }

    private async Task RecordCrossingAsync(bool forceUnassigned = false)
    {
        if (SelectedRace is null)
        {
            return;
        }

        // The instant is captured first, before any parsing or database access, so that the
        // recorded time reflects the exact moment of the physical crossing as closely as
        // possible.
        var capturedTicks = DateTime.Now.Ticks;

        long bikerIdentifier = 0;
        var bibNumberText = forceUnassigned ? string.Empty : BibNumberInputText.Trim();

        if (!forceUnassigned && bibNumberText.Length > 0)
        {
            if (!int.TryParse(bibNumberText, out var bibNumber))
            {
                LogService.Warning("ChronoViewModel -> RecordCrossingAsync", $"ignored non-numeric bib number input '{bibNumberText}'");
                return;
            }

            if (_registrationsByBibNumber.TryGetValue(bibNumber, out var registration))
            {
                bikerIdentifier = registration.BikerIdentifier;
            }
            else
            {
                LogService.Warning("ChronoViewModel -> RecordCrossingAsync", $"bib number {bibNumber} is not registered for race {SelectedRace.Identifier}; crossing recorded as unassigned");
            }
        }

        try
        {
            var crossing = new Crossing
            {
                BikerIdentifier = bikerIdentifier,
                RaceIdentifier = SelectedRace.Identifier,
                SequenceIndex = _nextSequenceIndex,
                Ticks = capturedTicks,
            };

            var newIdentifier = await _crossingRepository.AddAsync(crossing);
            crossing.Identifier = newIdentifier;
            _nextSequenceIndex++;

            var row = BuildRowForCrossing(crossing);
            Crossings.Insert(0, row);
            BibNumberInputText = string.Empty;

            await AutosaveJournalAsync();

            LogService.Information(
                "ChronoViewModel -> RecordCrossingAsync",
                $"crossing recorded for race {SelectedRace.Identifier}, biker {bikerIdentifier}, tick {capturedTicks}");
        }
        catch (Exception exception)
        {
            LogService.Error("ChronoViewModel -> RecordCrossingAsync", $"failed to record crossing for race {SelectedRace.Identifier}", exception);
        }

        UndoLastCrossingCommand.NotifyCanExecuteChanged();
    }

    private async Task UndoLastCrossingAsync()
    {
        if (Crossings.Count == 0)
        {
            return;
        }

        var lastRow = Crossings[0];

        var confirmationResult = MessageBox.Show(
            "Delete the most recently recorded crossing? This cannot be undone.",
            "Confirm Undo",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirmationResult != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await _crossingRepository.DeleteAsync(lastRow.Crossing.Identifier);
            Crossings.RemoveAt(0);

            if (lastRow.IsBibNumberAssigned && _lapCountByBikerIdentifier.TryGetValue(lastRow.Crossing.BikerIdentifier, out var lapCount))
            {
                _lapCountByBikerIdentifier[lastRow.Crossing.BikerIdentifier] = Math.Max(0, lapCount - 1);
            }

            await AutosaveJournalAsync();

            LogService.Warning("ChronoViewModel -> UndoLastCrossingAsync", $"removed crossing {lastRow.Crossing.Identifier} from race {SelectedRace?.Identifier}");
        }
        catch (Exception exception)
        {
            LogService.Error("ChronoViewModel -> UndoLastCrossingAsync", "failed to undo the last crossing", exception);
        }

        UndoLastCrossingCommand.NotifyCanExecuteChanged();
    }

    private async Task AssignBibNumberAsync(CrossingRow? row)
    {
        if (row is null || SelectedRace is null)
        {
            return;
        }

        if (!int.TryParse(row.PendingBibNumberText, out var bibNumber))
        {
            LogService.Warning("ChronoViewModel -> AssignBibNumberAsync", $"ignored non-numeric bib number input '{row.PendingBibNumberText}' while assigning a crossing");
            return;
        }

        if (!_registrationsByBibNumber.TryGetValue(bibNumber, out var registration))
        {
            LogService.Warning("ChronoViewModel -> AssignBibNumberAsync", $"bib number {bibNumber} is not registered for race {SelectedRace.Identifier}");
            return;
        }

        try
        {
            row.Crossing.BikerIdentifier = registration.BikerIdentifier;
            await _crossingRepository.UpdateAsync(row.Crossing);

            _lapCountByBikerIdentifier.TryGetValue(registration.BikerIdentifier, out var previousLapCount);
            row.LapNumber = previousLapCount + 1;
            _lapCountByBikerIdentifier[registration.BikerIdentifier] = row.LapNumber;

            _bikersByIdentifier.TryGetValue(registration.BikerIdentifier, out var biker);
            row.BikerFullName = biker?.FullName;
            row.BibNumberText = bibNumber.ToString();
            row.IsBibNumberAssigned = true;

            await AutosaveJournalAsync();

            LogService.Information("ChronoViewModel -> AssignBibNumberAsync", $"assigned bib number {bibNumber} to crossing {row.Crossing.Identifier}");
        }
        catch (Exception exception)
        {
            LogService.Error("ChronoViewModel -> AssignBibNumberAsync", $"failed to assign bib number {bibNumber} to crossing {row.Crossing.Identifier}", exception);
        }
    }

    private async Task ResetRaceAsync()
    {
        if (SelectedRace is null)
        {
            return;
        }

        var confirmationResult = MessageBox.Show(
            $"This will permanently delete every recorded crossing AND clear the start time of race \"{SelectedRace.Name}\". The race will need to be started again from scratch. Continue?",
            "Confirm Full Race Reset",
            MessageBoxButton.YesNo,
            MessageBoxImage.Stop);
        if (confirmationResult != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await _crossingRepository.DeleteAllForRaceAsync(SelectedRace.Identifier);
            await _raceRepository.UpdateStartTicksAsync(SelectedRace.Identifier, 0);
            SelectedRace.StartTicks = 0;
            IsRaceRunning = false;
            Crossings.Clear();
            UndoLastCrossingCommand.NotifyCanExecuteChanged();
            _lapCountByBikerIdentifier.Clear();
            _nextSequenceIndex = 1;
            ElapsedTime = "0:00:00";

            LogService.Warning("ChronoViewModel -> ResetRaceAsync", $"race {SelectedRace.Identifier} fully reset: start time and all crossings cleared");
        }
        catch (Exception exception)
        {
            LogService.Error("ChronoViewModel -> ResetRaceAsync", $"failed to fully reset race {SelectedRace.Identifier}", exception);
        }
    }

    private async Task ResetRaceStandingsAsync()
    {
        if (SelectedRace is null)
        {
            return;
        }

        var confirmationResult = MessageBox.Show(
            $"This will permanently delete every recorded crossing of race \"{SelectedRace.Name}\", but the start time will be kept. Continue?",
            "Confirm Crossings Reset",
            MessageBoxButton.YesNo,
            MessageBoxImage.Stop);
        if (confirmationResult != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await _crossingRepository.DeleteAllForRaceAsync(SelectedRace.Identifier);
            Crossings.Clear();
            UndoLastCrossingCommand.NotifyCanExecuteChanged();
            _lapCountByBikerIdentifier.Clear();
            _nextSequenceIndex = 1;

            LogService.Warning("ChronoViewModel -> ResetRaceStandingsAsync", $"all crossings cleared for race {SelectedRace.Identifier}, start time kept");
        }
        catch (Exception exception)
        {
            LogService.Error("ChronoViewModel -> ResetRaceStandingsAsync", $"failed to clear crossings for race {SelectedRace.Identifier}", exception);
        }
    }

    /// <summary>
    /// Reloads the crossings of the selected race from a previously saved XML journal file, asking
    /// for confirmation before permanently replacing every recorded crossing of the selected race,
    /// and warning when the journal file belongs to a different race than the one currently
    /// selected. Because the replacement deletes and re-inserts every row, the crossings are
    /// reloaded from the database afterwards so that the displayed rows carry their new database
    /// identifiers.
    /// </summary>
    private async Task LoadJournalAsync()
    {
        if (SelectedRace is null)
        {
            return;
        }

        var openFileDialog = new OpenFileDialog
        {
            Filter = "Race Standings Journal (*.xml)|*.xml",
            InitialDirectory = Directory.Exists(_journalFolderPath) ? Path.GetFullPath(_journalFolderPath) : Environment.CurrentDirectory,
        };

        if (openFileDialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var (crossings, startRaceTicks) = _journalService.LoadJournal(openFileDialog.FileName);

            var journalRaceIdentifiers = crossings
                .Select(crossing => crossing.RaceIdentifier)
                .Where(raceIdentifier => raceIdentifier > 0)
                .Distinct()
                .ToList();
            var belongsToAnotherRace = journalRaceIdentifiers.Count > 0
                && !journalRaceIdentifiers.Contains(SelectedRace.Identifier);
            if (belongsToAnotherRace)
            {
                LogService.Warning("ChronoViewModel -> LoadJournalAsync", $"journal file {openFileDialog.FileName} belongs to race {string.Join(", ", journalRaceIdentifiers)} but race {SelectedRace.Identifier} is selected");
            }

            var confirmationText = belongsToAnotherRace
                ? $"The selected journal file belongs to a different race (race identifier "
                  + $"{string.Join(", ", journalRaceIdentifiers)}), not to \"{SelectedRace.Name}\". "
                  + $"Loading it will permanently replace every recorded crossing of \"{SelectedRace.Name}\" "
                  + $"with the {crossings.Count} crossings of the journal file. Continue?"
                : $"This will permanently replace every recorded crossing of race \"{SelectedRace.Name}\" "
                  + $"in the database with the {crossings.Count} crossings of the selected journal file. Continue?";

            var confirmationResult = MessageBox.Show(
                confirmationText,
                "Confirm Journal Load",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (confirmationResult != MessageBoxResult.Yes)
            {
                return;
            }

            await _crossingRepository.ReplaceAllForRaceAsync(SelectedRace.Identifier, crossings);

            if (startRaceTicks > 0)
            {
                await _raceRepository.UpdateStartTicksAsync(SelectedRace.Identifier, startRaceTicks);
                SelectedRace.StartTicks = startRaceTicks;
            }

            var reloadedCrossings = await _crossingRepository.GetForRaceAsync(SelectedRace.Identifier);
            LoadCrossingsIntoUi(reloadedCrossings);
            IsRaceRunning = SelectedRace.HasStarted;

            AutosaveStatusText = $"Journal loaded ({crossings.Count} crossings).";

            LogService.Information("ChronoViewModel -> LoadJournalAsync", $"loaded {crossings.Count} crossings from journal {openFileDialog.FileName} into race {SelectedRace.Identifier}");
        }
        catch (Exception exception)
        {
            LogService.Error("ChronoViewModel -> LoadJournalAsync", $"failed to load journal {openFileDialog.FileName}", exception);
        }
    }

    /// <summary>
    /// Explicitly commits every crossing currently held in memory to the database by replacing
    /// every crossing of the selected race with the crossings currently shown in the grid. Because
    /// the replacement deletes and re-inserts every row, the crossings are reloaded from the
    /// database afterwards so that the displayed rows carry their new database identifiers.
    /// </summary>
    private async Task ForceDatabaseSynchronizationAsync()
    {
        if (SelectedRace is null)
        {
            return;
        }

        try
        {
            var crossings = Crossings.Select(row => row.Crossing).OrderBy(crossing => crossing.SequenceIndex).ToList();
            await _crossingRepository.ReplaceAllForRaceAsync(SelectedRace.Identifier, crossings);

            var reloadedCrossings = await _crossingRepository.GetForRaceAsync(SelectedRace.Identifier);
            LoadCrossingsIntoUi(reloadedCrossings);

            LastSaveTime = DateTime.Now;
            AutosaveStatusText = $"Database synchronized at {LastSaveTime:HH:mm:ss}.";

            LogService.Information("ChronoViewModel -> ForceDatabaseSynchronizationAsync", $"force-synchronized {crossings.Count} crossings for race {SelectedRace.Identifier}");
        }
        catch (Exception exception)
        {
            LogService.Error("ChronoViewModel -> ForceDatabaseSynchronizationAsync", $"failed to force-synchronize race {SelectedRace.Identifier}", exception);
        }
    }

    private async Task AutosaveJournalAsync()
    {
        if (SelectedRace is null)
        {
            return;
        }

        try
        {
            var journalFilePath = GetJournalFilePath(SelectedRace);
            var crossings = Crossings.Select(row => row.Crossing).OrderBy(crossing => crossing.SequenceIndex).ToList();
            _journalService.WriteJournal(journalFilePath, SelectedRace, crossings, _bibNumberByBikerIdentifier);

            LastSaveTime = DateTime.Now;
            AutosaveStatusText = $"Autosaved at {LastSaveTime:HH:mm:ss}.";
        }
        catch (Exception exception)
        {
            LogService.Error("ChronoViewModel -> AutosaveJournalAsync", $"failed to autosave journal for race {SelectedRace.Identifier}", exception);
        }
    }

    private string GetJournalFilePath(Race race)
    {
        Directory.CreateDirectory(_journalFolderPath);
        var sanitizedRaceName = SanitizeFileName(race.Name);
        return Path.Combine(_journalFolderPath, $"{sanitizedRaceName}_RaceStandings.xml");
    }

    private string GetStartDateTimeFilePath(Race race)
    {
        Directory.CreateDirectory(_journalFolderPath);
        var sanitizedRaceName = SanitizeFileName(race.Name);
        return Path.Combine(_journalFolderPath, $"StartRaceDateTime{sanitizedRaceName}.xml");
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        var sanitizedCharacters = fileName.Select(character => invalidCharacters.Contains(character) ? '_' : character);
        return new string(sanitizedCharacters.ToArray());
    }
}

/// <summary>
/// Represents a single row of the crossings data grid shown on the Chrono screen, wrapping the
/// underlying <see cref="Models.Crossing"/> entity together with display and editing helpers.
/// </summary>
public class CrossingRow : ObservableObject
{
    private string _timeOfDayText = string.Empty;
    private string _raceTimeText = string.Empty;
    private string _bibNumberText = string.Empty;
    private string? _bikerFullName;
    private int _lapNumber;
    private bool _isBibNumberAssigned;
    private string _pendingBibNumberText = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="CrossingRow"/> class.
    /// </summary>
    /// <param name="crossing">The underlying crossing entity that this row represents.</param>
    public CrossingRow(Crossing crossing)
    {
        Crossing = crossing;
    }

    /// <summary>
    /// Gets the underlying crossing entity represented by this row.
    /// </summary>
    public Crossing Crossing { get; }

    /// <summary>
    /// Gets the sequence index of the crossing, used as the position column of the grid.
    /// </summary>
    public long SequenceIndex => Crossing.SequenceIndex;

    /// <summary>
    /// Gets or sets the formatted time of day at which the crossing was captured.
    /// </summary>
    public string TimeOfDayText
    {
        get => _timeOfDayText;
        set => SetProperty(ref _timeOfDayText, value);
    }

    /// <summary>
    /// Gets or sets the formatted elapsed race time of the crossing.
    /// </summary>
    public string RaceTimeText
    {
        get => _raceTimeText;
        set => SetProperty(ref _raceTimeText, value);
    }

    /// <summary>
    /// Gets or sets the bib number resolved for this crossing, as displayed text.
    /// </summary>
    public string BibNumberText
    {
        get => _bibNumberText;
        set => SetProperty(ref _bibNumberText, value);
    }

    /// <summary>
    /// Gets or sets the full name of the biker resolved for this crossing, or null when the
    /// crossing is still unassigned.
    /// </summary>
    public string? BikerFullName
    {
        get => _bikerFullName;
        set => SetProperty(ref _bikerFullName, value);
    }

    /// <summary>
    /// Gets or sets the lap number that this crossing represents for its biker.
    /// </summary>
    public int LapNumber
    {
        get => _lapNumber;
        set => SetProperty(ref _lapNumber, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether this crossing has been assigned to a known biker.
    /// </summary>
    public bool IsBibNumberAssigned
    {
        get => _isBibNumberAssigned;
        set => SetProperty(ref _isBibNumberAssigned, value);
    }

    /// <summary>
    /// Gets or sets the bib number text typed into the inline "assign bib" cell, used only while
    /// the crossing is still unassigned.
    /// </summary>
    public string PendingBibNumberText
    {
        get => _pendingBibNumberText;
        set => SetProperty(ref _pendingBibNumberText, value);
    }
}
