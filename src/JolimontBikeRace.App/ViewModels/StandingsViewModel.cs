using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JolimontBikeRace.Core.Interfaces;
using JolimontBikeRace.Core.Models;
using Microsoft.Win32;

namespace JolimontBikeRace.App.ViewModels;

/// <summary>
/// Supports the Standings section of the application, computing and displaying the final
/// classification of a race, per category, and allowing it to be saved to the database, exported
/// as a comma separated values file, or printed.
/// </summary>
public class StandingsViewModel : ViewModelBase
{
    private readonly ICrossingRepository _crossingRepository;
    private readonly IRegistrationRepository _registrationRepository;
    private readonly IBikerRepository _bikerRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IRaceCategoryLinkRepository _raceCategoryLinkRepository;
    private readonly IStandingsCalculatorService _standingsCalculatorService;
    private readonly IStandingRepository _standingRepository;
    private readonly IRaceCollectionService _raceCollectionService;
    private readonly IBrandingProvider _brandingProvider;

    private IReadOnlyList<StandingEntry> _lastComputedStandings = new List<StandingEntry>();

    private Race? _selectedRace;
    private CategoryStandings? _selectedCategoryStandings;
    private string _statusMessage = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="StandingsViewModel"/> class.
    /// </summary>
    /// <param name="crossingRepository">The repository used to load the crossings of the selected race.</param>
    /// <param name="registrationRepository">The repository used to load the registrations of the selected race.</param>
    /// <param name="bikerRepository">The repository used to resolve biker full names.</param>
    /// <param name="categoryRepository">The repository used to load every known category.</param>
    /// <param name="raceCategoryLinkRepository">The repository used to load the categories linked to the selected race.</param>
    /// <param name="standingsCalculatorService">The service used to compute the final classification.</param>
    /// <param name="standingRepository">The repository used to persist the official results.</param>
    /// <param name="raceCollectionService">The service that owns the single shared list of races.</param>
    /// <param name="brandingProvider">The provider of the customer-configurable name shown across the application.</param>
    /// <param name="logService">The logging service used to record every standings operation.</param>
    public StandingsViewModel(
        ICrossingRepository crossingRepository,
        IRegistrationRepository registrationRepository,
        IBikerRepository bikerRepository,
        ICategoryRepository categoryRepository,
        IRaceCategoryLinkRepository raceCategoryLinkRepository,
        IStandingsCalculatorService standingsCalculatorService,
        IStandingRepository standingRepository,
        IRaceCollectionService raceCollectionService,
        IBrandingProvider brandingProvider,
        ILogService logService)
        : base(logService)
    {
        _crossingRepository = crossingRepository;
        _registrationRepository = registrationRepository;
        _bikerRepository = bikerRepository;
        _categoryRepository = categoryRepository;
        _raceCategoryLinkRepository = raceCategoryLinkRepository;
        _standingsCalculatorService = standingsCalculatorService;
        _standingRepository = standingRepository;
        _raceCollectionService = raceCollectionService;
        _brandingProvider = brandingProvider;

        Title = "Standings";
        CategoryCheckboxes = new ObservableCollection<CategoryCheckboxRow>();
        CategoryStandingsList = new ObservableCollection<CategoryStandings>();

        ComputeStandingsCommand = new AsyncRelayCommand(ComputeStandingsAsync, () => SelectedRace is not null);
        RefreshCommand = new AsyncRelayCommand(ComputeStandingsAsync, () => SelectedRace is not null);
        SaveToDatabaseCommand = new AsyncRelayCommand(SaveToDatabaseAsync, () => SelectedRace is not null && _lastComputedStandings.Count > 0);
        ExportCommaSeparatedValuesCommand = new RelayCommand(ExportCommaSeparatedValues, () => _lastComputedStandings.Count > 0);
        PrintCommand = new RelayCommand(Print, () => CategoryStandingsList.Count > 0);
    }

    /// <summary>
    /// Gets the single shared list of races owned by <see cref="IRaceCollectionService"/>.
    /// </summary>
    public ObservableCollection<Race> Races => _raceCollectionService.Races;

    /// <summary>
    /// Gets the checkbox rows representing every category linked to the selected race, letting the
    /// user choose which categories are shown as separate tabs.
    /// </summary>
    public ObservableCollection<CategoryCheckboxRow> CategoryCheckboxes { get; }

    /// <summary>
    /// Gets the computed standings, grouped per category, one entry per tab.
    /// </summary>
    public ObservableCollection<CategoryStandings> CategoryStandingsList { get; }

    /// <summary>
    /// Gets or sets the category standings tab currently selected on the screen. Export and print
    /// operate on this selection: the "Overall" tab exports and prints every tab (the scratch
    /// ranking plus one section per category), while a specific category tab exports and prints
    /// only that category.
    /// </summary>
    public CategoryStandings? SelectedCategoryStandings
    {
        get => _selectedCategoryStandings;
        set => SetProperty(ref _selectedCategoryStandings, value);
    }

    /// <summary>
    /// Gets or sets the race that the standings are computed for.
    /// </summary>
    public Race? SelectedRace
    {
        get => _selectedRace;
        set
        {
            if (SetProperty(ref _selectedRace, value))
            {
                ComputeStandingsCommand.NotifyCanExecuteChanged();
                RefreshCommand.NotifyCanExecuteChanged();
                SaveToDatabaseCommand.NotifyCanExecuteChanged();
                _ = LoadCategoryCheckboxesAsync();
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
    /// Gets the command that computes the standings of the selected race.
    /// </summary>
    public AsyncRelayCommand ComputeStandingsCommand { get; }

    /// <summary>
    /// Gets the command that recomputes the standings, simulating a live refresh of the display.
    /// </summary>
    public AsyncRelayCommand RefreshCommand { get; }

    /// <summary>
    /// Gets the command that saves the last computed standings as the official results of the
    /// race.
    /// </summary>
    public AsyncRelayCommand SaveToDatabaseCommand { get; }

    /// <summary>
    /// Gets the command that exports the last computed standings as a comma separated values
    /// file.
    /// </summary>
    public RelayCommand ExportCommaSeparatedValuesCommand { get; }

    /// <summary>
    /// Gets the command that prints the currently displayed standings.
    /// </summary>
    public RelayCommand PrintCommand { get; }

    private async Task LoadCategoryCheckboxesAsync()
    {
        CategoryCheckboxes.Clear();

        if (SelectedRace is null)
        {
            return;
        }

        try
        {
            var links = await _raceCategoryLinkRepository.GetForRaceAsync(SelectedRace.Identifier);
            var allCategories = await _categoryRepository.GetAllAsync();
            var linkedCategoryIdentifiers = links.Select(link => link.CategoryIdentifier).ToHashSet();

            foreach (var category in allCategories.Where(category => linkedCategoryIdentifiers.Contains(category.Identifier)))
            {
                CategoryCheckboxes.Add(new CategoryCheckboxRow(category, isSelected: true));
            }
        }
        catch (Exception exception)
        {
            LogService.Error("StandingsViewModel -> LoadCategoryCheckboxesAsync", $"failed to load categories for race {SelectedRace.Identifier}", exception);
        }
    }

    private async Task ComputeStandingsAsync()
    {
        if (SelectedRace is null)
        {
            return;
        }

        try
        {
            var crossings = await _crossingRepository.GetForRaceAsync(SelectedRace.Identifier);
            var registrations = await _registrationRepository.GetForRaceAsync(SelectedRace.Identifier);
            var bikers = await _bikerRepository.GetAllAsync();
            var categories = await _categoryRepository.GetAllAsync();

            _lastComputedStandings = _standingsCalculatorService.ComputeStandings(SelectedRace, crossings, registrations, bikers, categories);

            CategoryStandingsList.Clear();

            var overallTab = new CategoryStandings("Overall", isOverall: true);
            foreach (var entry in _lastComputedStandings)
            {
                overallTab.Entries.Add(entry);
            }
            CategoryStandingsList.Add(overallTab);

            var selectedCategoryNames = CategoryCheckboxes.Where(row => row.IsSelected).Select(row => row.Category.Name);
            foreach (var categoryName in selectedCategoryNames)
            {
                var categoryTab = new CategoryStandings(categoryName);
                var categoryEntries = _lastComputedStandings.Where(entry => entry.CategoryName == categoryName).ToList();
                foreach (var entry in _standingsCalculatorService.RankWithinCategory(categoryEntries, SelectedRace.StartTicks))
                {
                    categoryTab.Entries.Add(entry);
                }
                CategoryStandingsList.Add(categoryTab);
            }

            SelectedCategoryStandings = CategoryStandingsList.FirstOrDefault();

            SaveToDatabaseCommand.NotifyCanExecuteChanged();
            ExportCommaSeparatedValuesCommand.NotifyCanExecuteChanged();
            PrintCommand.NotifyCanExecuteChanged();

            StatusMessage = $"Computed standings for {_lastComputedStandings.Count} riders.";
            LogService.Information("StandingsViewModel -> ComputeStandingsAsync", $"computed standings for race {SelectedRace.Identifier}: {_lastComputedStandings.Count} riders");
        }
        catch (Exception exception)
        {
            LogService.Error("StandingsViewModel -> ComputeStandingsAsync", $"failed to compute standings for race {SelectedRace.Identifier}", exception);
            StatusMessage = "Failed to compute standings.";
        }
    }

    private async Task SaveToDatabaseAsync()
    {
        if (SelectedRace is null || _lastComputedStandings.Count == 0)
        {
            return;
        }

        try
        {
            await _standingRepository.ReplaceForRaceAsync(SelectedRace.Identifier, _lastComputedStandings);
            StatusMessage = "Official results saved.";
            LogService.Information("StandingsViewModel -> SaveToDatabaseAsync", $"official results saved for race {SelectedRace.Identifier}");
        }
        catch (Exception exception)
        {
            LogService.Error("StandingsViewModel -> SaveToDatabaseAsync", $"failed to save official results for race {SelectedRace.Identifier}", exception);
            StatusMessage = "Failed to save official results.";
        }
    }

    /// <summary>
    /// Returns the category standings tabs that Export and Print must act on, based on the tab
    /// currently selected on the screen. When the "Overall" tab is selected (or nothing is), every
    /// tab is returned — the scratch ranking followed by each category section; otherwise only the
    /// selected category tab is returned.
    /// </summary>
    /// <returns>The category standings tabs to export or print.</returns>
    private IReadOnlyList<CategoryStandings> GetStandingsToOutput()
    {
        var selected = SelectedCategoryStandings;
        if (selected is null || selected.IsOverall)
        {
            return CategoryStandingsList.ToList();
        }

        return new List<CategoryStandings> { selected };
    }

    private void ExportCommaSeparatedValues()
    {
        var sections = GetStandingsToOutput();
        if (sections.Count == 0)
        {
            return;
        }

        var selected = SelectedCategoryStandings;
        var fileNameCategorySuffix = selected is not null && !selected.IsOverall ? $"_{selected.CategoryName}" : string.Empty;

        var saveFileDialog = new SaveFileDialog
        {
            Filter = "Comma Separated Values (*.csv)|*.csv",
            FileName = $"{SelectedRace?.Name}{fileNameCategorySuffix}_Standings.csv",
        };

        if (saveFileDialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var builder = new StringBuilder();

            foreach (var section in sections)
            {
                // Each tab is written as its own section, titled with the category name, so the
                // export reflects exactly what the selected tab shows.
                builder.AppendLine(EscapeCommaSeparatedValue(section.CategoryName));
                builder.AppendLine("Position,BibNumber,Rider,Category,Laps,RaceTime,Gap");

                foreach (var entry in section.Entries)
                {
                    builder.AppendLine(string.Join(",",
                        entry.Position,
                        entry.BibNumber,
                        EscapeCommaSeparatedValue(entry.BikerFullName),
                        EscapeCommaSeparatedValue(entry.CategoryName),
                        entry.CompletedLaps,
                        EscapeCommaSeparatedValue(entry.RaceTime),
                        EscapeCommaSeparatedValue(entry.Gap)));
                }

                builder.AppendLine();
            }

            File.WriteAllText(saveFileDialog.FileName, builder.ToString());

            StatusMessage = $"Standings exported to {saveFileDialog.FileName}.";
            LogService.Information("StandingsViewModel -> ExportCommaSeparatedValues", $"exported standings for race {SelectedRace?.Identifier} to {saveFileDialog.FileName}");
        }
        catch (Exception exception)
        {
            LogService.Error("StandingsViewModel -> ExportCommaSeparatedValues", $"failed to export standings to {saveFileDialog.FileName}", exception);
            StatusMessage = "Failed to export standings.";
        }
    }

    private static string EscapeCommaSeparatedValue(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Contains(',') || value.Contains('"')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }

    private void Print()
    {
        try
        {
            var flowDocument = new FlowDocument
            {
                PagePadding = new Thickness(40),
                ColumnWidth = double.PositiveInfinity,
            };

            flowDocument.Blocks.Add(new Paragraph(new Run($"Standings - {SelectedRace?.Name}"))
            {
                FontSize = 20,
                FontWeight = FontWeights.Bold,
            });

            foreach (var categoryStandings in GetStandingsToOutput())
            {
                flowDocument.Blocks.Add(new Paragraph(new Run(categoryStandings.CategoryName))
                {
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 20, 0, 5),
                });

                var table = new Table();
                for (var columnIndex = 0; columnIndex < 6; columnIndex++)
                {
                    table.Columns.Add(new TableColumn());
                }

                var rowGroup = new TableRowGroup();
                rowGroup.Rows.Add(CreateHeaderRow());
                foreach (var entry in categoryStandings.Entries)
                {
                    rowGroup.Rows.Add(CreateEntryRow(entry));
                }
                table.RowGroups.Add(rowGroup);

                flowDocument.Blocks.Add(table);
            }

            var printDialog = new PrintDialog();
            if (printDialog.ShowDialog() == true)
            {
                printDialog.PrintDocument(((IDocumentPaginatorSource)flowDocument).DocumentPaginator, $"{_brandingProvider.RaceName} Standings");
                LogService.Information("StandingsViewModel -> Print", $"printed standings for race {SelectedRace?.Identifier}");
            }
        }
        catch (Exception exception)
        {
            LogService.Error("StandingsViewModel -> Print", "failed to print standings", exception);
        }
    }

    private static TableRow CreateHeaderRow()
    {
        var row = new TableRow { FontWeight = FontWeights.Bold };
        row.Cells.Add(new TableCell(new Paragraph(new Run("Position"))));
        row.Cells.Add(new TableCell(new Paragraph(new Run("Bib"))));
        row.Cells.Add(new TableCell(new Paragraph(new Run("Rider"))));
        row.Cells.Add(new TableCell(new Paragraph(new Run("Laps"))));
        row.Cells.Add(new TableCell(new Paragraph(new Run("Race Time"))));
        row.Cells.Add(new TableCell(new Paragraph(new Run("Gap"))));
        return row;
    }

    private static TableRow CreateEntryRow(StandingEntry entry)
    {
        var row = new TableRow();
        row.Cells.Add(new TableCell(new Paragraph(new Run(entry.Position.ToString()))));
        row.Cells.Add(new TableCell(new Paragraph(new Run(entry.BibNumber?.ToString() ?? string.Empty))));
        row.Cells.Add(new TableCell(new Paragraph(new Run(entry.BikerFullName ?? string.Empty))));
        row.Cells.Add(new TableCell(new Paragraph(new Run(entry.CompletedLaps.ToString()))));
        row.Cells.Add(new TableCell(new Paragraph(new Run(entry.RaceTime ?? string.Empty))));
        row.Cells.Add(new TableCell(new Paragraph(new Run(entry.Gap ?? string.Empty))));
        return row;
    }
}

/// <summary>
/// Represents a single checkbox row of the category selection list, pairing a category with a
/// flag indicating whether it should be shown as a separate tab in the standings display.
/// </summary>
public class CategoryCheckboxRow : ObservableObject
{
    private bool _isSelected;

    /// <summary>
    /// Initializes a new instance of the <see cref="CategoryCheckboxRow"/> class.
    /// </summary>
    /// <param name="category">The category represented by this row.</param>
    /// <param name="isSelected">A value indicating whether the category is initially selected.</param>
    public CategoryCheckboxRow(Category category, bool isSelected)
    {
        Category = category;
        _isSelected = isSelected;
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
    /// Gets or sets a value indicating whether this category should be shown as a separate tab.
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

/// <summary>
/// Represents the computed standings of a single category, shown as one tab of the standings
/// display.
/// </summary>
public class CategoryStandings
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CategoryStandings"/> class.
    /// </summary>
    /// <param name="categoryName">The name of the category, used as the tab header.</param>
    /// <param name="isOverall">A value indicating whether this tab is the combined overall ranking rather than a single category.</param>
    public CategoryStandings(string categoryName, bool isOverall = false)
    {
        CategoryName = categoryName;
        IsOverall = isOverall;
        Entries = new ObservableCollection<StandingEntry>();
    }

    /// <summary>
    /// Gets the name of the category, used as the tab header.
    /// </summary>
    public string CategoryName { get; }

    /// <summary>
    /// Gets a value indicating whether this tab is the combined overall (scratch) ranking rather
    /// than a single category.
    /// </summary>
    public bool IsOverall { get; }

    /// <summary>
    /// Gets the ranked standing entries belonging to this category.
    /// </summary>
    public ObservableCollection<StandingEntry> Entries { get; }
}
