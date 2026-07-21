using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using TextBox = System.Windows.Controls.TextBox;
using Validation = System.Windows.Controls.Validation;

namespace PulseMeter.Slices.AccountUsage.UI;

public partial class AccountUsageSection : System.Windows.Controls.UserControl
{
    public AccountUsageSection()
    {
        InitializeComponent();
    }

    private void AutoSyncSecondsTextBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox)
        {
            return;
        }

        var binding = textBox.GetBindingExpression(TextBox.TextProperty);
        if (e.Key == Key.Enter)
        {
            binding?.UpdateSource();
            if (!Validation.GetHasError(textBox))
            {
                textBox.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            }

            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            binding?.UpdateTarget();
            textBox.SelectAll();
            e.Handled = true;
        }
    }
}
