using System.Windows.Controls;
using System.Windows.Input;
using JolimontBikeRace.App.ViewModels;

namespace JolimontBikeRace.App.Views;

/// <summary>
/// Provides the code-behind for the Chrono view. Almost every behavior of this view is
/// implemented through data binding and commands, with a single exception: forwarding the Enter
/// and Space keys typed into the bib-number box to the corresponding view model commands. This
/// requires a small amount of code-behind because WPF text boxes do not expose a built-in way to
/// bind individual key presses to commands without introducing an attached-behavior library, and
/// the amount of logic involved here is trivial enough that adding such a library would not be
/// justified.
/// </summary>
public partial class ChronoView : UserControl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ChronoView"/> class.
    /// </summary>
    public ChronoView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Forwards the Enter key to the RecordCrossingCommand and the Space key, when the box is
    /// empty, to the RecordUnassignedCrossingCommand. The event is marked as handled in both
    /// cases so that pressing Space in an empty box does not insert a literal space character.
    /// </summary>
    private void BibNumberTextBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not ChronoViewModel viewModel)
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            if (viewModel.RecordCrossingCommand.CanExecute(null))
            {
                viewModel.RecordCrossingCommand.Execute(null);
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Space && BibNumberTextBox.Text.Length == 0)
        {
            if (viewModel.RecordUnassignedCrossingCommand.CanExecute(null))
            {
                viewModel.RecordUnassignedCrossingCommand.Execute(null);
            }
            e.Handled = true;
        }
    }
}
