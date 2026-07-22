using System.Windows;
using JolimontBikeRace.App.ViewModels;

namespace JolimontBikeRace.App.Views;

/// <summary>
/// Provides the code-behind for the main application window. It contains no logic beyond
/// component initialization: navigation, status display and every other behavior are implemented
/// through data binding and commands on the <see cref="ShellViewModel"/>.
/// </summary>
public partial class ShellWindow : Window
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ShellWindow"/> class.
    /// </summary>
    /// <param name="shellViewModel">The view model that this window displays.</param>
    public ShellWindow(ShellViewModel shellViewModel)
    {
        InitializeComponent();
        DataContext = shellViewModel;
    }
}
