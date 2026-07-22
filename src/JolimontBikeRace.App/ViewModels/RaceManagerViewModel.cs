using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JolimontBikeRace.Core.Interfaces;
using JolimontBikeRace.Core.Models;

namespace JolimontBikeRace.App.ViewModels;

/// <summary>
/// Supports the Race Manager section of the application, allowing the user to create, duplicate
/// and delete races, to maintain the master list of categories, and to choose which categories
/// are offered within each race.
/// </summary>
public class RaceManagerViewModel : ViewModelBase
{
    private readonly IRaceRepository _raceRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IRaceCategoryLinkRepository _raceCategoryLinkRepository;

    private Race? _selectedRace;
    private string _selectedRaceName = string.Empty;
    private Category? _selectedCategory;
    private string _raceFilterText = string.Empty;
    private string _statusMessage = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="RaceManagerViewModel"/> class.
    /// </summary>
    /// <param name="raceRepository">The repository used to load, create, duplicate and delete races.</param>
    /// <param name="categoryRepository">The repository used to load, create, update and delete categories.</param>
    /// <param name="raceCategoryLinkRepository">The repository used to link and unlink categories to races.</param>
    /// <param name="logService">The logging service used to record every creation, deletion and link change.</param>
    public RaceManagerViewModel(
        IRaceRepository raceRepository,
        ICategoryRepository categoryRepository,
        IRaceCategoryLinkRepository raceCategoryLinkRepository,
        ILogService logService)
        : base(logService)
    {
        _raceRepository = raceRepository;
        _categoryRepository = categoryRepository;
        _raceCategoryLinkRepository = raceCategoryLinkRepository;

        Title = "Race Manager";
        Races = new ObservableCollection<Race>();
        Categories = new ObservableCollection<Category>();
        LinkedCategories = new ObservableCollection<CategoryLinkRow>();

        AddRaceCommand = new AsyncRelayCommand(AddRaceAsync);
        DeleteRaceCommand = new AsyncRelayCommand(DeleteRaceAsync, () => SelectedRace is not null);
        DuplicateRaceCommand = new AsyncRelayCommand(DuplicateRaceAsync, () => SelectedRace is not null);
        SaveRaceNameCommand = new AsyncRelayCommand(SaveRaceNameAsync, () => SelectedRace is not null);
        AddCategoryCommand = new AsyncRelayCommand(AddCategoryAsync);
        DeleteCategoryCommand = new AsyncRelayCommand(DeleteCategoryAsync, () => SelectedCategory is not null);
        SaveCategoryCommand = new AsyncRelayCommand(SaveCategoryAsync, () => SelectedCategory is not null);

        _ = InitializeAsync();
    }

    /// <summary>
    /// Gets the list of races known to the application.
    /// </summary>
    public ObservableCollection<Race> Races { get; }

    /// <summary>
    /// Gets the master list of categories known to the application.
    /// </summary>
    public ObservableCollection<Category> Categories { get; }

    /// <summary>
    /// Gets the list of checkbox rows representing every known category and whether it is
    /// currently linked to the selected race.
    /// </summary>
    public ObservableCollection<CategoryLinkRow> LinkedCategories { get; }

    /// <summary>
    /// Gets or sets the text used to filter the list of races by name.
    /// </summary>
    public string RaceFilterText
    {
        get => _raceFilterText;
        set => SetProperty(ref _raceFilterText, value);
    }

    /// <summary>
    /// Gets or sets the race that is currently selected in the races list.
    /// </summary>
    public Race? SelectedRace
    {
        get => _selectedRace;
        set
        {
            if (SetProperty(ref _selectedRace, value))
            {
                SelectedRaceName = value?.Name ?? string.Empty;
                DeleteRaceCommand.NotifyCanExecuteChanged();
                DuplicateRaceCommand.NotifyCanExecuteChanged();
                SaveRaceNameCommand.NotifyCanExecuteChanged();
                _ = LoadLinkedCategoriesAsync();
            }
        }
    }

    /// <summary>
    /// Gets or sets the editable name of the currently selected race, bound to the name text box.
    /// </summary>
    public string SelectedRaceName
    {
        get => _selectedRaceName;
        set => SetProperty(ref _selectedRaceName, value);
    }

    /// <summary>
    /// Gets or sets the category that is currently selected in the categories master grid.
    /// </summary>
    public Category? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (SetProperty(ref _selectedCategory, value))
            {
                DeleteCategoryCommand.NotifyCanExecuteChanged();
                SaveCategoryCommand.NotifyCanExecuteChanged();
            }
        }
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
    /// Gets the command that creates a new, empty race.
    /// </summary>
    public AsyncRelayCommand AddRaceCommand { get; }

    /// <summary>
    /// Gets the command that deletes the currently selected race, after asking for confirmation.
    /// </summary>
    public AsyncRelayCommand DeleteRaceCommand { get; }

    /// <summary>
    /// Gets the command that duplicates the currently selected race, including its category
    /// links.
    /// </summary>
    public AsyncRelayCommand DuplicateRaceCommand { get; }

    /// <summary>
    /// Gets the command that saves the edited name of the currently selected race.
    /// </summary>
    public AsyncRelayCommand SaveRaceNameCommand { get; }

    /// <summary>
    /// Gets the command that creates a new category.
    /// </summary>
    public AsyncRelayCommand AddCategoryCommand { get; }

    /// <summary>
    /// Gets the command that deletes the currently selected category.
    /// </summary>
    public AsyncRelayCommand DeleteCategoryCommand { get; }

    /// <summary>
    /// Gets the command that saves changes made to the currently selected category.
    /// </summary>
    public AsyncRelayCommand SaveCategoryCommand { get; }

    /// <summary>
    /// Loads the list of races and the master list of categories from the database.
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            var races = await _raceRepository.GetAllAsync();
            Races.Clear();
            foreach (var race in races)
            {
                Races.Add(race);
            }

            var categories = await _categoryRepository.GetAllAsync();
            Categories.Clear();
            foreach (var category in categories)
            {
                Categories.Add(category);
            }

            SelectedRace ??= Races.FirstOrDefault();
        }
        catch (Exception exception)
        {
            LogService.Error("RaceManagerViewModel -> InitializeAsync", "failed to load races and categories", exception);
            StatusMessage = "Failed to load races and categories.";
        }
    }

    private async Task LoadLinkedCategoriesAsync()
    {
        LinkedCategories.Clear();

        if (SelectedRace is null)
        {
            return;
        }

        try
        {
            var links = await _raceCategoryLinkRepository.GetForRaceAsync(SelectedRace.Identifier);
            var linkedCategoryIdentifiers = links.Select(link => link.CategoryIdentifier).ToHashSet();

            foreach (var category in Categories)
            {
                var row = new CategoryLinkRow(category, linkedCategoryIdentifiers.Contains(category.Identifier), OnCategoryLinkToggledAsync);
                LinkedCategories.Add(row);
            }
        }
        catch (Exception exception)
        {
            LogService.Error("RaceManagerViewModel -> LoadLinkedCategoriesAsync", $"failed to load linked categories for race {SelectedRace.Identifier}", exception);
        }
    }

    private async Task OnCategoryLinkToggledAsync(CategoryLinkRow row, bool isLinked)
    {
        if (SelectedRace is null)
        {
            return;
        }

        try
        {
            if (isLinked)
            {
                await _raceCategoryLinkRepository.LinkAsync(SelectedRace.Identifier, row.Category.Identifier);
                LogService.Information("RaceManagerViewModel -> OnCategoryLinkToggledAsync", $"linked category {row.Category.Name} to race {SelectedRace.Name}");
            }
            else
            {
                await _raceCategoryLinkRepository.UnlinkAsync(SelectedRace.Identifier, row.Category.Identifier);
                LogService.Information("RaceManagerViewModel -> OnCategoryLinkToggledAsync", $"unlinked category {row.Category.Name} from race {SelectedRace.Name}");
            }
        }
        catch (Exception exception)
        {
            LogService.Error("RaceManagerViewModel -> OnCategoryLinkToggledAsync", $"failed to change link between race {SelectedRace.Identifier} and category {row.Category.Identifier}", exception);
            StatusMessage = "Failed to update category link.";
        }
    }

    private async Task AddRaceAsync()
    {
        try
        {
            var newRace = new Race { Name = "New Race" };
            var newIdentifier = await _raceRepository.AddAsync(newRace);
            newRace.Identifier = newIdentifier;
            Races.Add(newRace);
            SelectedRace = newRace;

            LogService.Information("RaceManagerViewModel -> AddRaceAsync", $"created race {newRace.Name} with identifier {newRace.Identifier}");
        }
        catch (Exception exception)
        {
            LogService.Error("RaceManagerViewModel -> AddRaceAsync", "failed to create a new race", exception);
            StatusMessage = "Failed to create a new race.";
        }
    }

    private async Task DeleteRaceAsync()
    {
        if (SelectedRace is null)
        {
            return;
        }

        var confirmationResult = MessageBox.Show(
            $"Delete race \"{SelectedRace.Name}\"? This cannot be undone.",
            "Confirm Deletion",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirmationResult != MessageBoxResult.Yes)
        {
            return;
        }

        var raceToDelete = SelectedRace;
        try
        {
            await _raceRepository.DeleteAsync(raceToDelete.Identifier);
            LogService.Information("RaceManagerViewModel -> DeleteRaceAsync", $"deleted race {raceToDelete.Identifier}");
            Races.Remove(raceToDelete);
            SelectedRace = Races.FirstOrDefault();
        }
        catch (Exception exception)
        {
            LogService.Error("RaceManagerViewModel -> DeleteRaceAsync", $"failed to delete race {raceToDelete.Identifier}", exception);
            StatusMessage = "Failed to delete race.";
        }
    }

    private async Task DuplicateRaceAsync()
    {
        if (SelectedRace is null)
        {
            return;
        }

        try
        {
            var duplicateRace = new Race { Name = $"{SelectedRace.Name} - Copy" };
            var newIdentifier = await _raceRepository.AddAsync(duplicateRace);
            duplicateRace.Identifier = newIdentifier;

            var existingLinks = await _raceCategoryLinkRepository.GetForRaceAsync(SelectedRace.Identifier);
            foreach (var link in existingLinks)
            {
                await _raceCategoryLinkRepository.LinkAsync(duplicateRace.Identifier, link.CategoryIdentifier);
            }

            Races.Add(duplicateRace);
            SelectedRace = duplicateRace;

            LogService.Information("RaceManagerViewModel -> DuplicateRaceAsync", $"duplicated race {SelectedRace.Identifier} into new race {duplicateRace.Identifier} with {existingLinks.Count} category links");
        }
        catch (Exception exception)
        {
            LogService.Error("RaceManagerViewModel -> DuplicateRaceAsync", $"failed to duplicate race {SelectedRace.Identifier}", exception);
            StatusMessage = "Failed to duplicate race.";
        }
    }

    private async Task SaveRaceNameAsync()
    {
        if (SelectedRace is null)
        {
            return;
        }

        try
        {
            SelectedRace.Name = SelectedRaceName;
            await _raceRepository.UpdateAsync(SelectedRace);
            StatusMessage = "Race saved.";
            LogService.Information("RaceManagerViewModel -> SaveRaceNameAsync", $"saved race {SelectedRace.Identifier} with name {SelectedRace.Name}");
        }
        catch (Exception exception)
        {
            LogService.Error("RaceManagerViewModel -> SaveRaceNameAsync", $"failed to save race {SelectedRace.Identifier}", exception);
            StatusMessage = "Failed to save race.";
        }
    }

    private async Task AddCategoryAsync()
    {
        try
        {
            var newCategory = new Category { Name = "New Category", MinimumBibNumber = 1, MaximumBibNumber = 99 };
            var newIdentifier = await _categoryRepository.AddAsync(newCategory);
            newCategory.Identifier = newIdentifier;
            Categories.Add(newCategory);
            SelectedCategory = newCategory;
            await LoadLinkedCategoriesAsync();

            LogService.Information("RaceManagerViewModel -> AddCategoryAsync", $"created category {newCategory.Name} with identifier {newCategory.Identifier}");
        }
        catch (Exception exception)
        {
            LogService.Error("RaceManagerViewModel -> AddCategoryAsync", "failed to create a new category", exception);
            StatusMessage = "Failed to create a new category.";
        }
    }

    private async Task DeleteCategoryAsync()
    {
        if (SelectedCategory is null)
        {
            return;
        }

        var confirmationResult = MessageBox.Show(
            $"Delete category \"{SelectedCategory.Name}\"? This cannot be undone.",
            "Confirm Deletion",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirmationResult != MessageBoxResult.Yes)
        {
            return;
        }

        var categoryToDelete = SelectedCategory;
        try
        {
            await _categoryRepository.DeleteAsync(categoryToDelete.Identifier);
            LogService.Information("RaceManagerViewModel -> DeleteCategoryAsync", $"deleted category {categoryToDelete.Identifier}");
            Categories.Remove(categoryToDelete);
            SelectedCategory = Categories.FirstOrDefault();
            await LoadLinkedCategoriesAsync();
        }
        catch (Exception exception)
        {
            LogService.Error("RaceManagerViewModel -> DeleteCategoryAsync", $"failed to delete category {categoryToDelete.Identifier}", exception);
            StatusMessage = "Failed to delete category.";
        }
    }

    private async Task SaveCategoryAsync()
    {
        if (SelectedCategory is null)
        {
            return;
        }

        try
        {
            await _categoryRepository.UpdateAsync(SelectedCategory);
            await LoadLinkedCategoriesAsync();
            StatusMessage = "Category saved.";
            LogService.Information("RaceManagerViewModel -> SaveCategoryAsync", $"saved category {SelectedCategory.Identifier}");
        }
        catch (Exception exception)
        {
            LogService.Error("RaceManagerViewModel -> SaveCategoryAsync", $"failed to save category {SelectedCategory.Identifier}", exception);
            StatusMessage = "Failed to save category.";
        }
    }
}

/// <summary>
/// Represents a single checkbox row of the linked-categories list, pairing a category with a
/// flag indicating whether it is currently linked to the selected race. Toggling the flag
/// immediately triggers an asynchronous link or unlink operation, without requiring a separate
/// save action.
/// </summary>
public class CategoryLinkRow : ObservableObject
{
    private readonly Func<CategoryLinkRow, bool, Task> _onToggled;
    private bool _isLinked;

    /// <summary>
    /// Initializes a new instance of the <see cref="CategoryLinkRow"/> class.
    /// </summary>
    /// <param name="category">The category represented by this row.</param>
    /// <param name="isLinked">A value indicating whether the category is currently linked to the selected race.</param>
    /// <param name="onToggled">The callback invoked, with the new linked state, whenever the user toggles the checkbox.</param>
    public CategoryLinkRow(Category category, bool isLinked, Func<CategoryLinkRow, bool, Task> onToggled)
    {
        Category = category;
        _isLinked = isLinked;
        _onToggled = onToggled;
    }

    /// <summary>
    /// Gets the category represented by this row.
    /// </summary>
    public Category Category { get; }

    /// <summary>
    /// Gets the name of the category, exposed directly for convenient binding.
    /// </summary>
    public string CategoryName => Category.Name;

    /// <summary>
    /// Gets or sets a value indicating whether the category is linked to the selected race.
    /// Setting this property immediately triggers the asynchronous link or unlink operation.
    /// </summary>
    public bool IsLinked
    {
        get => _isLinked;
        set
        {
            if (SetProperty(ref _isLinked, value))
            {
                _ = _onToggled(this, value);
            }
        }
    }
}
