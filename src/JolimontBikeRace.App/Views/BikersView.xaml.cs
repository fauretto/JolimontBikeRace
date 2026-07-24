using System.Linq;
using System.Windows.Controls;
using System.Windows.Input;
using JolimontBikeRace.App.ViewModels;

namespace JolimontBikeRace.App.Views;

/// <summary>
/// Provides the code-behind for the Bikers view. Its only logic is a keyboard handler that lets
/// the user delete one or more selected bikers with the Delete key; every other behavior is
/// implemented through data binding and commands on the associated view model. The handler lives
/// in the view because it reads the data grid's SelectedItems collection, which is a user-interface
/// concept that is not exposed to the view model.
/// </summary>
public partial class BikersView : UserControl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BikersView"/> class.
    /// </summary>
    public BikersView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Deletes the bikers currently selected in the biker grid when the user presses the Delete
    /// key. The confirmation prompt and the actual deletion are handled by the view model.
    /// </summary>
    /// <param name="sender">The biker data grid that raised the event.</param>
    /// <param name="eventArguments">The key event arguments.</param>
    private void BikerDataGrid_PreviewKeyDown(object sender, KeyEventArgs eventArguments)
    {
        if (eventArguments.Key != Key.Delete)
        {
            return;
        }

        if (DataContext is not BikersViewModel viewModel)
        {
            return;
        }

        var selectedRows = BikerDataGrid.SelectedItems.OfType<BikerRow>().ToList();
        if (selectedRows.Count == 0)
        {
            return;
        }

        eventArguments.Handled = true;
        _ = viewModel.DeleteSelectedBikersAsync(selectedRows);
    }
}
