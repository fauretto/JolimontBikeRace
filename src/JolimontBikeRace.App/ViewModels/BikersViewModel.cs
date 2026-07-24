using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JolimontBikeRace.Core.Interfaces;
using JolimontBikeRace.Core.Models;

namespace JolimontBikeRace.App.ViewModels;

/// <summary>
/// Supports the Bikers section of the application, allowing the user to search, edit and delete
/// bikers, and to register a biker into a race and category with an automatically suggested bib
/// number. The biker grid also displays the bib number assigned to each biker within the
/// currently selected registration race.
/// </summary>
public class BikersViewModel : ViewModelBase
{
    private readonly IBikerRepository _bikerRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IRaceCategoryLinkRepository _raceCategoryLinkRepository;
    private readonly IRegistrationRepository _registrationRepository;
    private readonly IBibNumberValidationService _bibNumberValidationService;
    private readonly IRaceCollectionService _raceCollectionService;

    private readonly ICollectionView _bikersView;
    private readonly ICollectionView _registeredBikersView;

    // Maps a biker identifier to the bib number assigned to that biker for the currently selected
    // registration race, used both by the search filter and by the bib-number validation logic.
    private Dictionary<long, int> _bibNumberByBikerIdentifierForSelectedRace = new();
    private IReadOnlyList<Registration> _registrationsForSelectedRace = new List<Registration>();

    // Maps a category identifier to its name, across all races, used to resolve human-readable
    // category names in the registered bikers list and in the already-registered indicator.
    private Dictionary<long, string> _categoryNameByIdentifier = new();

    private string _searchText = string.Empty;
    private BikerRow? _selectedBikerRow;
    private Race? _selectedRegistrationRace;
    private Category? _selectedRegistrationCategory;
    private string _bibNumberText = string.Empty;
    private string _statusMessage = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="BikersViewModel"/> class.
    /// </summary>
    /// <param name="bikerRepository">The repository used to load, create, update and delete bikers.</param>
    /// <param name="categoryRepository">The repository used to load the categories available for registration.</param>
    /// <param name="raceCategoryLinkRepository">The repository used to load the categories linked to the selected race.</param>
    /// <param name="registrationRepository">The repository used to load and create registrations.</param>
    /// <param name="bibNumberValidationService">The service used to validate and suggest bib numbers.</param>
    /// <param name="raceCollectionService">The service that owns the single shared list of races.</param>
    /// <param name="logService">The logging service used to record every biker and registration operation.</param>
    public BikersViewModel(
        IBikerRepository bikerRepository,
        ICategoryRepository categoryRepository,
        IRaceCategoryLinkRepository raceCategoryLinkRepository,
        IRegistrationRepository registrationRepository,
        IBibNumberValidationService bibNumberValidationService,
        IRaceCollectionService raceCollectionService,
        ILogService logService)
        : base(logService)
    {
        _bikerRepository = bikerRepository;
        _categoryRepository = categoryRepository;
        _raceCategoryLinkRepository = raceCategoryLinkRepository;
        _registrationRepository = registrationRepository;
        _bibNumberValidationService = bibNumberValidationService;
        _raceCollectionService = raceCollectionService;

        Title = "Bikers";
        BikerRows = new ObservableCollection<BikerRow>();
        RegistrationCategories = new ObservableCollection<Category>();
        RegisteredBikers = new ObservableCollection<RegisteredBikerRow>();

        _bikersView = CollectionViewSource.GetDefaultView(BikerRows);
        _bikersView.Filter = FilterBiker;

        _registeredBikersView = CollectionViewSource.GetDefaultView(RegisteredBikers);
        _registeredBikersView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(RegisteredBikerRow.CategoryName)));

        NewBikerCommand = new RelayCommand(NewBiker);
        ClearBikerDetailsCommand = new RelayCommand(ClearBikerDetails);
        SaveBikerCommand = new AsyncRelayCommand(SaveBikerAsync, () => SelectedBikerRow is not null);
        DeleteBikerCommand = new AsyncRelayCommand(DeleteBikerAsync, () => SelectedBikerRow is not null);
        RegisterCommand = new AsyncRelayCommand(RegisterAsync, () => SelectedBikerRow is not null && SelectedRegistrationRace is not null && SelectedRegistrationCategory is not null);

        _ = InitializeAsync();
    }

    /// <summary>
    /// Gets the full list of bikers loaded from the database, each wrapped in a <see cref="BikerRow"/>
    /// that additionally carries the bib number assigned within the currently selected registration
    /// race.
    /// </summary>
    public ObservableCollection<BikerRow> BikerRows { get; }

    /// <summary>
    /// Gets the filtered view of <see cref="BikerRows"/> that the biker data grid is bound to.
    /// </summary>
    public ICollectionView BikersView => _bikersView;

    /// <summary>
    /// Gets the single shared list of races owned by <see cref="IRaceCollectionService"/>.
    /// </summary>
    public ObservableCollection<Race> Races => _raceCollectionService.Races;

    /// <summary>
    /// Gets the list of categories offered within the currently selected registration race.
    /// </summary>
    public ObservableCollection<Category> RegistrationCategories { get; }

    /// <summary>
    /// Gets the list of bikers registered for the currently selected registration race, with
    /// category names already resolved for display.
    /// </summary>
    public ObservableCollection<RegisteredBikerRow> RegisteredBikers { get; }

    /// <summary>
    /// Gets the view of <see cref="RegisteredBikers"/> that the registered bikers list is bound
    /// to, grouped by category name.
    /// </summary>
    public ICollectionView RegisteredBikersView => _registeredBikersView;

    /// <summary>
    /// Gets or sets the text used to filter the biker list by first name, last name, electronic
    /// mail address or bib number within the selected registration race.
    /// </summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                _bikersView.Refresh();
            }
        }
    }

    /// <summary>
    /// Gets or sets the row currently selected in the biker data grid. Setting this property
    /// refreshes the biker detail editor, the registration commands, the already-registered
    /// indicator, and the registration hint.
    /// </summary>
    public BikerRow? SelectedBikerRow
    {
        get => _selectedBikerRow;
        set
        {
            if (SetProperty(ref _selectedBikerRow, value))
            {
                OnPropertyChanged(nameof(SelectedBiker));
                SaveBikerCommand.NotifyCanExecuteChanged();
                DeleteBikerCommand.NotifyCanExecuteChanged();
                RegisterCommand.NotifyCanExecuteChanged();
                OnPropertyChanged(nameof(AlreadyRegisteredText));
                OnPropertyChanged(nameof(RegistrationHint));
                SynchronizeRegistrationPanelWithSelection();
            }
        }
    }

    /// <summary>
    /// Gets the biker currently selected in the biker data grid, or null if no row is selected.
    /// This is a read-only projection of <see cref="SelectedBikerRow"/>, kept for convenient
    /// binding from the biker detail editor.
    /// </summary>
    public Biker? SelectedBiker => SelectedBikerRow?.Biker;

    /// <summary>
    /// Gets or sets the race that the registration panel currently operates on.
    /// </summary>
    public Race? SelectedRegistrationRace
    {
        get => _selectedRegistrationRace;
        set
        {
            if (SetProperty(ref _selectedRegistrationRace, value))
            {
                RegisterCommand.NotifyCanExecuteChanged();
                OnPropertyChanged(nameof(RegistrationHint));
                OnPropertyChanged(nameof(AlreadyRegisteredText));
                _ = LoadRegistrationContextAsync();
            }
        }
    }

    /// <summary>
    /// Gets or sets the category that the registration panel currently operates on. Changing this
    /// property automatically suggests the next free bib number for the category.
    /// </summary>
    public Category? SelectedRegistrationCategory
    {
        get => _selectedRegistrationCategory;
        set
        {
            if (SetProperty(ref _selectedRegistrationCategory, value))
            {
                RegisterCommand.NotifyCanExecuteChanged();
                OnPropertyChanged(nameof(CategoryRangeText));
                OnPropertyChanged(nameof(RegistrationHint));

                if (value is not null)
                {
                    var nextFreeBibNumber = _bibNumberValidationService.GetNextFreeBibNumber(value, _registrationsForSelectedRace);
                    BibNumberText = nextFreeBibNumber?.ToString() ?? string.Empty;
                }
            }
        }
    }

    /// <summary>
    /// Gets a textual description of the bib number range reserved for the selected registration
    /// category, for display next to the category combo box.
    /// </summary>
    public string CategoryRangeText => SelectedRegistrationCategory is null
        ? string.Empty
        : $"Range: {SelectedRegistrationCategory.MinimumBibNumber}-{SelectedRegistrationCategory.MaximumBibNumber}";

    /// <summary>
    /// Gets or sets the bib number entered for the new registration.
    /// </summary>
    public string BibNumberText
    {
        get => _bibNumberText;
        set => SetProperty(ref _bibNumberText, value);
    }

    /// <summary>
    /// Gets or sets a short status message describing the outcome of the last operation.
    /// </summary>
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>
    /// Gets a message describing the existing registration of the selected biker within the
    /// selected registration race, or an empty string when the selected biker is not yet
    /// registered for that race, when no biker or race is selected, or when the selected biker
    /// has not been saved yet.
    /// </summary>
    public string AlreadyRegisteredText
    {
        get
        {
            var selectedBiker = SelectedBiker;
            if (selectedBiker is null || SelectedRegistrationRace is null || selectedBiker.Identifier == 0)
            {
                return string.Empty;
            }

            var existingRegistration = _registrationsForSelectedRace.FirstOrDefault(registration => registration.BikerIdentifier == selectedBiker.Identifier);
            if (existingRegistration is null)
            {
                return string.Empty;
            }

            return $"Already registered — bib {existingRegistration.BibNumber?.ToString() ?? "none"} ({ResolveCategoryName(existingRegistration.CategoryIdentifier)})";
        }
    }

    /// <summary>
    /// Gets a short explanation of what is still missing before the registration button becomes
    /// enabled, or an empty string once a biker, a race and a category have all been selected.
    /// </summary>
    public string RegistrationHint
    {
        get
        {
            if (SelectedBikerRow is null)
            {
                return "Select a biker to register.";
            }

            if (SelectedRegistrationRace is null)
            {
                return "Select a race.";
            }

            if (SelectedRegistrationCategory is null)
            {
                return "Select a category.";
            }

            return string.Empty;
        }
    }

    /// <summary>
    /// Gets the command that starts editing a brand new, unsaved biker.
    /// </summary>
    public RelayCommand NewBikerCommand { get; }

    /// <summary>
    /// Gets the command that clears the biker detail editor without deleting or modifying the
    /// selected biker.
    /// </summary>
    public RelayCommand ClearBikerDetailsCommand { get; }

    /// <summary>
    /// Gets the command that saves the currently edited biker.
    /// </summary>
    public AsyncRelayCommand SaveBikerCommand { get; }

    /// <summary>
    /// Gets the command that deletes the currently selected biker, after asking for confirmation.
    /// </summary>
    public AsyncRelayCommand DeleteBikerCommand { get; }

    /// <summary>
    /// Gets the command that registers the currently selected biker into the selected race and
    /// category, using the entered bib number.
    /// </summary>
    public AsyncRelayCommand RegisterCommand { get; }

    /// <summary>
    /// Loads the full biker list, then loads the category name cache used to resolve
    /// human-readable category names throughout the page. The category load runs in its own
    /// try/catch block so that a category-loading failure never blanks the biker list or its
    /// status message.
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            var bikers = await _bikerRepository.GetAllAsync();
            BikerRows.Clear();
            foreach (var biker in bikers)
            {
                BikerRows.Add(new BikerRow(biker));
            }
        }
        catch (Exception exception)
        {
            LogService.Error("BikersViewModel -> InitializeAsync", "failed to load bikers", exception);
            StatusMessage = "Failed to load bikers.";
        }

        try
        {
            var allCategories = await _categoryRepository.GetAllAsync();
            _categoryNameByIdentifier = allCategories.ToDictionary(category => category.Identifier, category => category.Name);
        }
        catch (Exception exception)
        {
            LogService.Error("BikersViewModel -> InitializeAsync", "failed to load categories", exception);
        }
    }

    /// <summary>
    /// Determines whether the given biker row matches <see cref="SearchText"/>, matching against
    /// the biker's first name, last name, electronic mail address and the bib number assigned
    /// within the selected registration race.
    /// </summary>
    /// <param name="item">The item offered by the collection view, expected to be a <see cref="BikerRow"/>.</param>
    /// <returns>True if the row should be displayed; otherwise, false.</returns>
    private bool FilterBiker(object item)
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return true;
        }

        if (item is not BikerRow row)
        {
            return false;
        }

        var searchTextLowered = SearchText.Trim().ToLowerInvariant();

        var matchesFirstName = row.Biker.FirstName?.ToLowerInvariant().Contains(searchTextLowered) ?? false;
        var matchesLastName = row.Biker.LastName?.ToLowerInvariant().Contains(searchTextLowered) ?? false;
        var matchesElectronicMailAddress = row.Biker.ElectronicMailAddress?.ToLowerInvariant().Contains(searchTextLowered) ?? false;

        var matchesBibNumber = row.BibNumber.HasValue && row.BibNumber.Value.ToString().Contains(searchTextLowered);

        return matchesFirstName || matchesLastName || matchesElectronicMailAddress || matchesBibNumber;
    }

    /// <summary>
    /// Loads the categories linked to the selected registration race and the registrations
    /// already made for that race, then projects the resulting bib numbers onto <see cref="BikerRows"/>.
    /// </summary>
    private async Task LoadRegistrationContextAsync()
    {
        RegistrationCategories.Clear();
        _bibNumberByBikerIdentifierForSelectedRace = new Dictionary<long, int>();
        _registrationsForSelectedRace = new List<Registration>();

        if (SelectedRegistrationRace is null)
        {
            foreach (var row in BikerRows)
            {
                row.BibNumber = null;
            }

            OnPropertyChanged(nameof(AlreadyRegisteredText));
            RebuildRegisteredBikers();
            _bikersView.Refresh();
            return;
        }

        try
        {
            var links = await _raceCategoryLinkRepository.GetForRaceAsync(SelectedRegistrationRace.Identifier);
            var allCategories = await _categoryRepository.GetAllAsync();
            var linkedCategoryIdentifiers = links.Select(link => link.CategoryIdentifier).ToHashSet();

            _categoryNameByIdentifier = allCategories.ToDictionary(category => category.Identifier, category => category.Name);

            foreach (var category in allCategories.Where(category => linkedCategoryIdentifiers.Contains(category.Identifier)))
            {
                RegistrationCategories.Add(category);
            }

            var registrations = await _registrationRepository.GetForRaceAsync(SelectedRegistrationRace.Identifier);
            _registrationsForSelectedRace = registrations;
            _bibNumberByBikerIdentifierForSelectedRace = registrations
                .Where(registration => registration.BibNumber.HasValue)
                .GroupBy(registration => registration.BikerIdentifier)
                .ToDictionary(group => group.Key, group => group.First().BibNumber!.Value);

            foreach (var row in BikerRows)
            {
                row.BibNumber = _bibNumberByBikerIdentifierForSelectedRace.TryGetValue(row.Biker.Identifier, out var bibNumber) ? bibNumber : null;
            }

            _bikersView.Refresh();
            OnPropertyChanged(nameof(AlreadyRegisteredText));

            SynchronizeRegistrationPanelWithSelection();
            RebuildRegisteredBikers();
        }
        catch (Exception exception)
        {
            LogService.Error("BikersViewModel -> LoadRegistrationContextAsync", $"failed to load registration context for race {SelectedRegistrationRace.Identifier}", exception);
            OnPropertyChanged(nameof(AlreadyRegisteredText));
            RebuildRegisteredBikers();
        }
    }

    /// <summary>
    /// Aligns the registration panel with the current biker and race selection: when the selected
    /// biker is already registered for the selected registration race, the registered category and
    /// bib number are displayed; otherwise the next free bib number is suggested for the selected
    /// category.
    /// </summary>
    private void SynchronizeRegistrationPanelWithSelection()
    {
        var selectedBiker = SelectedBiker;
        var existingRegistration = selectedBiker is null || SelectedRegistrationRace is null || selectedBiker.Identifier == 0
            ? null
            : _registrationsForSelectedRace.FirstOrDefault(registration => registration.BikerIdentifier == selectedBiker.Identifier);

        if (existingRegistration is not null)
        {
            SelectedRegistrationCategory = existingRegistration.CategoryIdentifier is null
                ? null
                : RegistrationCategories.FirstOrDefault(category => category.Identifier == existingRegistration.CategoryIdentifier.Value);

            // Assigning SelectedRegistrationCategory above triggers that property's setter, which
            // overwrites BibNumberText with a next-free-bib suggestion, so the registered bib
            // number must be assigned here, after the category, to take effect.
            BibNumberText = existingRegistration.BibNumber?.ToString() ?? string.Empty;
            return;
        }

        if (SelectedRegistrationCategory is not null)
        {
            var nextFreeBibNumber = _bibNumberValidationService.GetNextFreeBibNumber(SelectedRegistrationCategory, _registrationsForSelectedRace);
            BibNumberText = nextFreeBibNumber?.ToString() ?? string.Empty;
        }
    }

    /// <summary>
    /// Rebuilds the list of bikers registered for the currently selected registration race,
    /// grouped by category name and ordered by bib number within each category.
    /// </summary>
    private void RebuildRegisteredBikers()
    {
        RegisteredBikers.Clear();

        var bikerNameByIdentifier = BikerRows
            .GroupBy(row => row.Biker.Identifier)
            .ToDictionary(group => group.Key, group => group.First().Biker.FullName);

        var orderedRegistrations = _registrationsForSelectedRace
            .OrderBy(registration => ResolveCategoryName(registration.CategoryIdentifier))
            .ThenBy(registration => registration.BibNumber ?? int.MaxValue);

        foreach (var registration in orderedRegistrations)
        {
            var bikerFullName = bikerNameByIdentifier.TryGetValue(registration.BikerIdentifier, out var fullName)
                ? fullName
                : $"Biker {registration.BikerIdentifier}";

            RegisteredBikers.Add(new RegisteredBikerRow(ResolveCategoryName(registration.CategoryIdentifier), bikerFullName, registration.BibNumber));
        }
    }

    /// <summary>
    /// Resolves the display name of a category from its identifier, using the cached category
    /// names loaded across all races.
    /// </summary>
    /// <param name="categoryIdentifier">The identifier of the category to resolve, or null.</param>
    /// <returns>The name of the category, a generic fallback label, or "Unknown category" when the identifier is null.</returns>
    private string ResolveCategoryName(long? categoryIdentifier)
    {
        if (categoryIdentifier is null)
        {
            return "Unknown category";
        }

        return _categoryNameByIdentifier.TryGetValue(categoryIdentifier.Value, out var categoryName) ? categoryName : $"Category {categoryIdentifier.Value}";
    }

    /// <summary>
    /// Creates a brand new, unsaved biker, adds it to the biker list and selects it for editing.
    /// </summary>
    private void NewBiker()
    {
        var newBikerRow = new BikerRow(new Biker());
        BikerRows.Add(newBikerRow);
        SelectedBikerRow = newBikerRow;
    }

    /// <summary>
    /// Clears the biker detail editor by clearing the current selection. Because the detail text
    /// boxes are bound to the selected biker, clearing the selection empties every field. This
    /// operation is deliberately non-destructive: the selected biker remains in the list and in
    /// the database, and none of its values are modified.
    /// </summary>
    private void ClearBikerDetails()
    {
        SelectedBikerRow = null;
    }

    /// <summary>
    /// Saves the currently selected biker, creating it in the database if it is new, or updating
    /// it otherwise.
    /// </summary>
    private async Task SaveBikerAsync()
    {
        if (SelectedBiker is null)
        {
            return;
        }

        try
        {
            if (SelectedBiker.Identifier == 0)
            {
                var newIdentifier = await _bikerRepository.AddAsync(SelectedBiker);
                SelectedBiker.Identifier = newIdentifier;
                LogService.Information("BikersViewModel -> SaveBikerAsync", $"created biker {SelectedBiker.FullName} with identifier {SelectedBiker.Identifier}");
            }
            else
            {
                await _bikerRepository.UpdateAsync(SelectedBiker);
                LogService.Information("BikersViewModel -> SaveBikerAsync", $"updated biker {SelectedBiker.Identifier}");
            }

            StatusMessage = "Biker saved.";
        }
        catch (Exception exception)
        {
            LogService.Error("BikersViewModel -> SaveBikerAsync", $"failed to save biker {SelectedBiker.Identifier}", exception);
            StatusMessage = "Failed to save biker.";
        }
    }

    /// <summary>
    /// Deletes the currently selected biker after asking the user for confirmation.
    /// </summary>
    private async Task DeleteBikerAsync()
    {
        if (SelectedBikerRow is null)
        {
            return;
        }

        await DeleteBikerRowsAsync(new List<BikerRow> { SelectedBikerRow });
    }

    /// <summary>
    /// Deletes the given biker rows after asking the user for a single confirmation. This is the
    /// public entry point used by the biker grid when the user presses the Delete key on one or
    /// more selected rows.
    /// </summary>
    /// <param name="rows">The biker rows to delete.</param>
    public async Task DeleteSelectedBikersAsync(IReadOnlyList<BikerRow> rows)
    {
        await DeleteBikerRowsAsync(rows);
    }

    /// <summary>
    /// Deletes the given biker rows, together with every registration and race result of each
    /// biker, after asking the user for a single confirmation. Unsaved rows (identifier zero) are
    /// only removed from the list. The registration context is reloaded afterwards so that the bib
    /// numbers, the already-registered indicator and the registered bikers list reflect the
    /// removals.
    /// </summary>
    /// <param name="rows">The biker rows to delete.</param>
    private async Task DeleteBikerRowsAsync(IReadOnlyList<BikerRow> rows)
    {
        if (rows is null || rows.Count == 0)
        {
            return;
        }

        var rowsToDelete = rows.ToList();

        var confirmationMessage = rowsToDelete.Count == 1
            ? $"Delete biker \"{rowsToDelete[0].Biker.FullName}\"? All of this biker's registrations and race results will also be permanently deleted. This cannot be undone."
            : $"Delete {rowsToDelete.Count} selected bikers? All their registrations and race results will also be permanently deleted. This cannot be undone.";

        var confirmationResult = MessageBox.Show(
            confirmationMessage,
            "Confirm Deletion",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirmationResult != MessageBoxResult.Yes)
        {
            return;
        }

        var deletedCount = 0;
        foreach (var rowToDelete in rowsToDelete)
        {
            try
            {
                if (rowToDelete.Biker.Identifier != 0)
                {
                    await _bikerRepository.DeleteWithDependenciesAsync(rowToDelete.Biker.Identifier);
                    LogService.Information("BikersViewModel -> DeleteBikerRowsAsync", $"deleted biker {rowToDelete.Biker.Identifier} with its registrations and race results");
                }

                BikerRows.Remove(rowToDelete);
                deletedCount++;
            }
            catch (Exception exception)
            {
                LogService.Error("BikersViewModel -> DeleteBikerRowsAsync", $"failed to delete biker {rowToDelete.Biker.Identifier}", exception);
                StatusMessage = "Failed to delete biker.";
            }
        }

        if (deletedCount == 0)
        {
            return;
        }

        SelectedBikerRow = BikerRows.FirstOrDefault();
        StatusMessage = deletedCount == 1 ? "Deleted 1 biker." : $"Deleted {deletedCount} bikers.";

        if (SelectedRegistrationRace is not null)
        {
            await LoadRegistrationContextAsync();
        }
    }

    /// <summary>
    /// Registers the currently selected biker into the selected race and category, using the
    /// entered bib number, after validating that the biker has been saved, that the biker is not
    /// already registered for the race, and that the bib number is valid and available.
    /// </summary>
    private async Task RegisterAsync()
    {
        if (SelectedBikerRow is not { } bikerRow || SelectedRegistrationRace is not { } race || SelectedRegistrationCategory is not { } category)
        {
            return;
        }

        var biker = bikerRow.Biker;

        if (biker.Identifier == 0)
        {
            LogService.Warning("BikersViewModel -> RegisterAsync", "rejected registration attempt for an unsaved biker");
            StatusMessage = "Save the biker before registering.";
            return;
        }

        if (_registrationsForSelectedRace.Any(registration => registration.BikerIdentifier == biker.Identifier))
        {
            LogService.Warning("BikersViewModel -> RegisterAsync", $"rejected duplicate registration of biker {biker.Identifier} in race {race.Identifier}");
            StatusMessage = "This biker is already registered for this race.";
            return;
        }

        if (!int.TryParse(BibNumberText, out var bibNumber))
        {
            LogService.Warning("BikersViewModel -> RegisterAsync", $"rejected registration attempt with non-numeric bib number '{BibNumberText}'");
            StatusMessage = "The bib number must be a whole number.";
            return;
        }

        if (!_bibNumberValidationService.IsWithinCategoryRange(bibNumber, category))
        {
            LogService.Warning("BikersViewModel -> RegisterAsync", $"rejected bib number {bibNumber} outside range of category {category.Name}");
            StatusMessage = $"Bib number {bibNumber} is outside the category's allowed range.";
            return;
        }

        if (!_bibNumberValidationService.IsAvailable(bibNumber, _registrationsForSelectedRace))
        {
            LogService.Warning("BikersViewModel -> RegisterAsync", $"rejected bib number {bibNumber} already used in race {race.Identifier}");
            StatusMessage = $"Bib number {bibNumber} is already in use for this race.";
            return;
        }

        try
        {
            var registration = new Registration
            {
                BikerIdentifier = biker.Identifier,
                RaceIdentifier = race.Identifier,
                CategoryIdentifier = category.Identifier,
                BibNumber = bibNumber,
            };

            var newIdentifier = await _registrationRepository.AddAsync(registration);
            registration.Identifier = newIdentifier;

            _registrationsForSelectedRace = _registrationsForSelectedRace.Append(registration).ToList();
            _bibNumberByBikerIdentifierForSelectedRace[biker.Identifier] = bibNumber;
            bikerRow.BibNumber = bibNumber;
            _bikersView.Refresh();
            OnPropertyChanged(nameof(AlreadyRegisteredText));
            SynchronizeRegistrationPanelWithSelection();
            RebuildRegisteredBikers();

            StatusMessage = $"Biker registered with bib number {bibNumber}.";
            LogService.Information(
                "BikersViewModel -> RegisterAsync",
                $"biker {biker.Identifier} registered to race {race.Identifier} with bib number {bibNumber}");
        }
        catch (Exception exception)
        {
            LogService.Error("BikersViewModel -> RegisterAsync", $"failed to register biker {biker.Identifier} to race {race.Identifier}", exception);
            StatusMessage = "Failed to register biker.";
        }
    }
}

/// <summary>
/// Wraps a <see cref="Biker"/> for display in the biker data grid, additionally carrying the bib
/// number assigned to the biker within the currently selected registration race so that the grid
/// can be refreshed live, without reloading the underlying biker list.
/// </summary>
public class BikerRow : ObservableObject
{
    private int? _bibNumber;

    /// <summary>
    /// Initializes a new instance of the <see cref="BikerRow"/> class.
    /// </summary>
    /// <param name="biker">The underlying biker entity that this row represents.</param>
    public BikerRow(Biker biker)
    {
        Biker = biker;
    }

    /// <summary>
    /// Gets the underlying biker entity represented by this row.
    /// </summary>
    public Biker Biker { get; }

    /// <summary>
    /// Gets or sets the bib number assigned to the biker within the currently selected
    /// registration race, or null if the race is not selected or the biker is not registered
    /// for it.
    /// </summary>
    public int? BibNumber
    {
        get => _bibNumber;
        set => SetProperty(ref _bibNumber, value);
    }
}

/// <summary>
/// Represents one biker registered for the currently selected registration race, for display in
/// the registered bikers list, grouped by category.
/// </summary>
public class RegisteredBikerRow : ObservableObject
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RegisteredBikerRow"/> class.
    /// </summary>
    /// <param name="categoryName">The resolved display name of the category that the registration belongs to.</param>
    /// <param name="bikerFullName">The full name of the registered biker.</param>
    /// <param name="bibNumber">The bib number assigned for this registration, or null if none has been assigned.</param>
    public RegisteredBikerRow(string categoryName, string bikerFullName, int? bibNumber)
    {
        CategoryName = categoryName;
        BikerFullName = bikerFullName;
        BibNumber = bibNumber;
    }

    /// <summary>
    /// Gets the resolved display name of the category that the registration belongs to.
    /// </summary>
    public string CategoryName { get; }

    /// <summary>
    /// Gets the full name of the registered biker.
    /// </summary>
    public string BikerFullName { get; }

    /// <summary>
    /// Gets the bib number assigned for this registration, or null if none has been assigned.
    /// </summary>
    public int? BibNumber { get; }

    /// <summary>
    /// Gets the bib number as displayed text, or an empty string when no bib number has been
    /// assigned.
    /// </summary>
    public string BibNumberText => BibNumber?.ToString() ?? string.Empty;
}
