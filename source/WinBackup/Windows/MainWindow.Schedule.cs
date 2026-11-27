using System.Text;
using WinBackup.Engine;
using WinBackup.Models;

namespace WinBackup.Windows;

public partial class MainWindow
{
    partial void DescribeSchedule(Profile profile, StringBuilder text)
    {
        var schedule = profile.Schedule;
        if (schedule.Kind == ScheduleKind.Manual && !schedule.OnStartup) return;

        text.AppendLine();
        if (schedule.Kind != ScheduleKind.Manual)
        {
            text.AppendLine($"Schedule: {schedule.Kind}");
            var next = ScheduleCalculator.NextDue(schedule, DateTime.Now);
            text.AppendLine(!profile.Enabled ? "Next run: none while disabled"
                : next == null ? "Next run: none"
                : $"Next run: {next:dddd yyyy-MM-dd HH:mm}");
        }
        if (schedule.OnStartup) text.AppendLine("Runs when WinBackup starts.");
        text.AppendLine("Scheduled runs happen only while WinBackup is running.");
    }
}
