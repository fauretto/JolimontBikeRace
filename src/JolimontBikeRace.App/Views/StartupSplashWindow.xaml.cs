using System.Windows;

namespace JolimontBikeRace.App.Views;

/// <summary>
/// Provides the code-behind for the startup splash window, a small borderless window shown while
/// the application verifies (and if necessary creates) its database, before the main window opens.
/// </summary>
public partial class StartupSplashWindow : Window
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StartupSplashWindow"/> class.
    /// </summary>
    /// <param name="raceName">The customer-configurable race name shown as the heading of the splash window.</param>
    public StartupSplashWindow(string raceName)
    {
        InitializeComponent();
        RaceNameText.Text = raceName;
    }
}
