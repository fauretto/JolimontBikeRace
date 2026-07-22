using CommunityToolkit.Mvvm.ComponentModel;
using JolimontBikeRace.Core.Interfaces;

namespace JolimontBikeRace.App.ViewModels;

/// <summary>
/// Serves as the common base class for every view model of the application, providing access to
/// the shared logging service and a display title.
/// </summary>
public abstract class ViewModelBase : ObservableObject
{
    private string _title = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="ViewModelBase"/> class.
    /// </summary>
    /// <param name="logService">The logging service that derived view models use to record their activity.</param>
    protected ViewModelBase(ILogService logService)
    {
        LogService = logService;
    }

    /// <summary>
    /// Gets the logging service used by this view model to record informational messages,
    /// warnings and errors.
    /// </summary>
    protected ILogService LogService { get; }

    /// <summary>
    /// Gets or sets the display title of this view model, typically shown as a section heading in
    /// the user interface.
    /// </summary>
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }
}
