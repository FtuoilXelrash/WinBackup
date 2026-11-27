using WinBackup.Windows;

namespace WinBackup;

public partial class App
{
    public void ShowMiniWindow() => ShowTool(() => new MiniStatusWindow());

    partial void MainWindowHidden()
    {
        if (Settings.Current.ShowMiniWhenClosedToTray) ShowMiniWindow();
    }
}
