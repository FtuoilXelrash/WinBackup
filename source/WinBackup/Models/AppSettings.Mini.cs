namespace WinBackup.Models;

public partial class AppSettings
{
    public bool ShowMiniOnStart { get; set; }
    public bool ShowMiniWhenClosedToTray { get; set; }
}

public partial class WindowOptionSet
{
    public WindowOption Mini { get; set; } = new() { AlwaysOnTop = true, ShowInTaskbar = false };
}
