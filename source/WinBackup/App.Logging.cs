using WinBackup.Services;
using WinBackup.Windows;

namespace WinBackup;

public partial class App
{
    partial void InitLogging()
    {
        DispatcherUnhandledException += (_, e) =>
        {
            LogService.Error(e.Exception.ToString());
            e.Handled = true;
            Tray?.Notify("WinBackup", "An unexpected error occurred. See the log for details.", true);
        };
        LogService.Prune(Settings.Current.LogRetentionDays);
        LogService.Info($"Application started v{AboutWindow.VersionText}");
    }
}
