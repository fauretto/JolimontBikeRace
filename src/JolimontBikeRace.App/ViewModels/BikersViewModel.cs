using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.Input;
using JolimontBikeRace.Core.Interfaces;
using JolimontBikeRace.Core.Models;

namespace JolimontBikeRace.App.ViewModels;

/// <summary>
/// Supports the Bikers section of the application, allowing the user to search, edit and delete
/// bikers, and to register a biker into a race and category with an automatically suggested bib
/// number.
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

    // Maps a biker identifier to the bib number assigned to that biker for the currently selected
    // registration race, used both by the search filter and by the bib-number validation logic.
    private Dictionary<long, int> _bibNumberByBikerIdentifierForSelectedRace = new();
    private IReadOnlyList<Registration> _registrationsForSelectedRace = new List<Registration>();

    private string _searchText = string.Empty;
    private Biker? _selectedBiker;
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
        Bikers = new ObservableCollection<Biker>();
        RegistrationCategories = new ObservableCollection<Category>();
        RegistrationHistory = new ObservableCollection<Registration>();

        _bikersView = CollectionViewSource.GetDefaultView(Bikers);
        _bikersView.Filter = FilterBiker;

        NewBikerCommand = new RelayCommand(NewBiker);
        SaveBikerCommand = new AsyncRelayCommand(SaveBikerAsync, () => SelectedBiker is not null);
        DeleteBikerCommand = new AsyncRelayCommand(DeleteBikerAsync, () => SelectedBiker is not null);
        RegisterCommand = new AsyncRelayCommand(RegisterAsync, () => SelectedBiker is not null && SelectedRegistrationRace is not null && SelectedRegistrationCategory is not null);

        _ = InitializeAsync();
    }

    /// <summary>
    /// Gets the full list of bikers loaded from the database.
    /// </summary>
    public ObservableCollection<Biker> Bikers { get; }

    /// <summary>
    /// Gets the filtered view of <see cref="Bikers"/> that the biker data grid is bound to.
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
    /// Gets the registration history of the currently selected biker.
    /// </summary>
    public ObservableCollection<Registration> RegistrationHistory { get; }

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
    /// Gets or sets the biker currently selected in the biker data grid.
    /// </summary>
    public Biker? SelectedBiker
    {
        get => _selectedBiker;
        set
        {
            if (SetProperty(ref _selectedBiker, value))
            {
                SaveBikerCommand.NotifyCanExecuteChanged();
                DeleteBikerCommand.NotifyCanExecuteChanged();
                RegisterCommand.NotifyCanExecuteChanged();
                _ = LoadRegistrationHistoryAsync();
            }
        }
    }

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
    /// Gets the command that starts editing a brand new, unsaved biker.
    /// </summary>
    public RelayCommand NewBikerCommand { get; }

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
    /// Loads the full biker list.
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            var bikers = await _bikerRepository.GetAllAsync();
            Bikers.Clear();
            foreach (var biker in bikers)
            {
                Bikers.Add(biker);
            }
        }
        catch (Exception exception)
        {
            LogService.Error("BikersViewModel -> InitializeAsync", "failed to load bikers", exception);
            StatusMessage = "Failed to load bikers.";
        }
    }

    private bool FilterBiker(object item)
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            return true;
        }

        if (item is not Biker biker)
        {
            return false;
        }

        var searchTextLowered = SearchText.Trim().ToLowerInvariant();

        var matchesFirstName = biker.FirstName?.ToLowerInvariant().Contains(searchTextLowered) ?? false;
        var matchesLastName = biker.LastName?.ToLowerInvariant().Contains(searchTextLowered) ?? false;
        var matchesElectronicMailAddress = biker.ElectronicMailAddress?.ToLowerInvariant().Contains(searchTextLowered) ?? false;

        var matchesBibNumber = _bibNumberByBikerIdentifierForSelectedRace.TryGetValue(biker.Identifier, out var bibNumber)
            && bibNumber.ToString().Contains(searchTextLowered);

        return matchesFirstName || matchesLastName || matchesElectronicMailAddress || matchesBibNumber;
    }

    private async Task LoadRegistrationContextAsync()
    {
        RegistrationCategories.Clear();
        _bibNumberByBikerIdentifierForSelectedRace = new Dictionary<long, int>();
        _registrationsForSelectedRace = new List<Registration>();

        if (SelectedRegistrationRace is null)
        {
            _bikersView.Refresh();
            return;
        }

        try
        {
            var links = await _raceCategoryLinkRepository.GetForRaceAsync(SelectedRegistrationRace.Identifier);
            var allCategories = await _categoryRepository.GetAllAsync();
            var linkedCategoryIdentifiers = links.Select(link => link.CategoryIdentifier).ToHashSet();

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

            _bikersView.Refresh();
        }
        catch (Exception exception)
        {
            LogService.Error("BikersViewModel -> LoadRegistrationContextAsync", $"failed to load registration context for race {SelectedRegistrationRace.Identifier}", exception);
        }
    }

    private async Task LoadRegistrationHistoryAsync()
    {
        RegistrationHistory.Clear();

        if (SelectedBiker is null)
        {
            return;
        }

        try
        {
            var registrations = await _registrationRepository.GetForBikerAsync(SelectedBiker.Identifier);
            foreach (var registration in registrations)
            {
                RegistrationHistory.Add(registration);
            }
        }
        catch (Exception exception)
        {
            LogService.Error("BikersViewModel -> LoadRegistrationHistoryAsync", $"failed to load registration history for biker {SelectedBiker.Identifier}", exception);
        }
    }

    private void NewBiker()
    {
        var newBiker = new Biker();
        Bikers.Add(newBiker);
        SelectedBiker = newBiker;
    }

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

    private async Task DeleteBikerAsync()
    {
        if (SelectedBiker is null)
        {
            return;
        }

        var confirmationResult = MessageBox.Show(
            $"Delete biker \"{SelectedBiker.FullName}\"? This cannot be undone.",
            "Confirm Deletion",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirmationResult != MessageBoxResult.Yes)
        {
            return;
        }

        var bikerToDelete = SelectedBiker;
        try
        {
            if (bikerToDelete.Identifier != 0)
            {
                await _bikerRepository.DeleteAsync(bikerToDelete.Identifier);
                LogService.Information("BikersViewModel -> DeleteBikerAsync", $"deleted biker {bikerToDelete.Identifier}");
            }

            Bikers.Remove(bikerToDelete);
            SelectedBiker = Bikers.FirstOrDefault();
        }
        catch (Exception exception)
        {
            LogService.Error("BikersViewModel -> DeleteBikerAsync", $"failed to delete biker {bikerToDelete.Identifier}", exception);
            StatusMessage = "Failed to delete biker.";
        }
    }

    private async Task RegisterAsync()
    {
        if (SelectedBiker is null || SelectedRegistrationRace is null || SelectedRegistrationCategory is null)
        {
            return;
        }

        if (!int.TryParse(BibNumberText, out var bibNumber))
        {
            LogService.Warning("BikersViewModel -> RegisterAsync", $"rejected registration attempt with non-numeric bib number '{BibNumberText}'");
            StatusMessage = "The bib number must be a whole number.";
            return;
        }

        if (!_bibNumberValidationService.IsWithinCategoryRange(bibNumber, SelectedRegistrationCategory))
        {
            LogService.Warning("BikersViewModel -> RegisterAsync", $"rejected bib number {bibNumber} outside range of category {SelectedRegistrationCategory.Name}");
            StatusMessage = $"Bib number {bibNumber} is outside the category's allowed range.";
            return;
        }

        if (!_bibNumberValidationService.IsAvailable(bibNumber, _registrationsForSelectedRace))
        {
            LogService.Warning("BikersViewModel -> RegisterAsync", $"rejected bib number {bibNumber} already used in race {SelectedRegistrationRace.Identifier}");
            StatusMessage = $"Bib number {bibNumber} is already in use for this race.";
            return;
        }

        try
        {
            var registration = new Registration
            {
                BikerIdentifier = SelectedBiker.Identifier,
                RaceIdentifier = SelectedRegistrationRace.Identifier,
                CategoryIdentifier = SelectedRegistrationCategory.Identifier,
                BibNumber = bibNumber,
            };

            var newIdentifier = await _registrationRepository.AddAsync(registration);
            registration.Identifier = newIdentifier;

            RegistrationHistory.Add(registration);
            _registrationsForSelectedRace = _registrationsForSelectedRace.Append(registration).ToList();
            _bibNumberByBikerIdentifierForSelectedRace[SelectedBiker.Identifier] = bibNumber;
            _bikersView.Refresh();

            StatusMessage = $"Biker registered with bib number {bibNumber}.";
            LogService.Information(
                "BikersViewModel -> RegisterAsync",
                $"biker {SelectedBiker.Identifier} registered to race {SelectedRegistrationRace.Identifier} with bib number {bibNumber}");
        }
        catch (Exception exception)
        {
            LogService.Error("BikersViewModel -> RegisterAsync", $"failed to register biker {SelectedBiker.Identifier} to race {SelectedRegistrationRace.Identifier}", exception);
            StatusMessage = "Failed to register biker.";
        }
    }
}
