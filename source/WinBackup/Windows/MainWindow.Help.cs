using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using WinBackup.Managers;

namespace WinBackup.Windows;

public partial class MainWindow
{
    protected override void OnKeyDown(System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.F1)
        {
            OpenHelp();
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }

    private void Help_Click(object sender, RoutedEventArgs e) => OpenHelp();

    private void OpenHelp()
    {
        var path = Path.Combine(AppPaths.Docs, "WinBackup-Help.html");
        if (!File.Exists(path))
        {
            Warn($"The help file was not found:\n{path}");
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Win32Exception ex)
        {
            Warn("The help file could not be opened: " + ex.Message);
        }
    }
}
