namespace WinBackup.Models;

public partial class AppSettings
{
    public int LogRetentionDays { get; set; } = 30;
}

public partial class WindowOptionSet
{
    public WindowOption LogViewer { get; set; } = new();
}
