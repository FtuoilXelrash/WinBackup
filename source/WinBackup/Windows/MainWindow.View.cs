namespace WinBackup.Windows;

public partial class MainWindow
{
    partial void InitViewMenu()
    {
        AddViewItem("_Settings...", () => App.Instance.ShowTool(() => new SettingsWindow()));
        AddViewItem("_Reports...", () => App.Instance.ShowTool(() => new ReportsWindow()));
        AddViewItem("_Log Viewer...", () => App.Instance.ShowTool(() => new LogViewerWindow()));
    }
}
