using System.Windows.Controls;

namespace JolimontBikeRace.App.Views;

/// <summary>
/// Provides the code-behind for the Race Manager view. It contains no logic beyond component
/// initialization: every behavior is implemented through data binding and commands on the
/// associated view model.
/// </summary>
public partial class RaceManagerView : UserControl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RaceManagerView"/> class.
    /// </summary>
    public RaceManagerView()
    {
        InitializeComponent();
    }
}
