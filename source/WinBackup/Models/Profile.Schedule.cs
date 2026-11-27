namespace WinBackup.Models;

public enum ScheduleKind
{
    Manual,
    Once,
    Interval,
    Daily,
    Weekly,
    Monthly
}

public class ScheduleSettings
{
    public ScheduleKind Kind { get; set; } = ScheduleKind.Manual;
    public DateTime Date { get; set; } = DateTime.Today;
    public string Time { get; set; } = "12:00";
    public int IntervalMinutes { get; set; } = 60;
    public bool Monday { get; set; } = true;
    public bool Tuesday { get; set; }
    public bool Wednesday { get; set; }
    public bool Thursday { get; set; }
    public bool Friday { get; set; }
    public bool Saturday { get; set; }
    public bool Sunday { get; set; }
    public int DayOfMonth { get; set; } = 1;
    public bool RunMissed { get; set; }
    public bool OnStartup { get; set; }
}

public partial class Profile
{
    public ScheduleSettings Schedule { get; set; } = new();
}
