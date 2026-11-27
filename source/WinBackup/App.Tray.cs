using System.Windows;
using WinBackup.Managers;
using WinBackup.Windows;

namespace WinBackup;

public partial class App
{
    public TrayManager Tray { get; private set; } = null!;

    partial void InitTray()
    {
        Tray = new TrayManager();
        Tray.OpenRequested += ShowMainWindow;
        Tray.AddCommand("Open WinBackup", ShowMainWindow);
        Tray.AddCommand("Settings", () => ShowTool(() => new SettingsWindow()));
        Tray.AddCommand("Log Viewer", () => ShowTool(() => new LogViewerWindow()));
        Tray.AddSeparator();
        Tray.AddCommand("Exit", () =>
        {
            if (ConfirmExit()) RequestExit();
        });
    }

    partial void CheckTray(ref bool available) => available = Tray != null;

    public bool ConfirmExit() =>
        !Settings.Current.ConfirmExit ||
        MessageBox.Show("Exit WinBackup?", "WinBackup", MessageBoxButton.YesNo,
            MessageBoxImage.Question) == MessageBoxResult.Yes;
}
