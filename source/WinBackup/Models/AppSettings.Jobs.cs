namespace WinBackup.Models;

public partial class AppSettings
{
    public bool ShowNotifications { get; set; } = true;
    public bool NotifyOnlyProblems { get; set; }
}
