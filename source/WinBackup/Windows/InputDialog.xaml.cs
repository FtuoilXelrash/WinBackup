using System.Windows;

namespace WinBackup.Windows;

public partial class InputDialog : Window
{
    private InputDialog()
    {
        InitializeComponent();
    }

    public static string? Ask(Window owner, string title, string prompt, string value = "")
    {
        var dialog = new InputDialog { Owner = owner, Title = title, Topmost = owner.Topmost };
        dialog.Prompt.Text = prompt;
        dialog.Input.Text = value;
        dialog.Input.SelectAll();
        dialog.Input.Focus();
        if (dialog.ShowDialog() != true) return null;
        var result = dialog.Input.Text.Trim();
        return result.Length == 0 ? null : result;
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
