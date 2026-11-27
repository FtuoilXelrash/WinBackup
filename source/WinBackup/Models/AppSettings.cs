namespace WinBackup.Models;

public enum AppTheme
{
    Light,
    Dark
}

public partial class AppSettings
{
    public AppTheme Theme { get; set; } = AppTheme.Light;
    public bool CloseToTray { get; set; } = true;
    public bool ConfirmExit { get; set; } = true;
    public bool StartMinimized { get; set; }
    public WindowOptionSet Windows { get; set; } = new();
    public Dictionary<string, WindowPosition> Positions { get; set; } = new();
}

public class WindowOption
{
    public bool AlwaysOnTop { get; set; }
    public bool ShowInTaskbar { get; set; } = true;
}

public partial class WindowOptionSet
{
    public WindowOption Main { get; set; } = new();
    public WindowOption Settings { get; set; } = new() { ShowInTaskbar = false };
}

public class WindowPosition
{
    public double Left { get; set; }
    public double Top { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public bool Maximized { get; set; }
}
