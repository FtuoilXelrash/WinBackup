using System.Globalization;
using WinBackup.Models;

namespace WinBackup.Engine;

public static class ScheduleCalculator
{
    public static bool TryTime(string text, out TimeSpan time) =>
        TimeSpan.TryParseExact(text.Trim(), new[] { @"h\:mm", @"hh\:mm" }, CultureInfo.InvariantCulture, out time);

    private static bool Matches(ScheduleSettings schedule, DateTime day) => schedule.Kind switch
    {
        ScheduleKind.Daily => true,
        ScheduleKind.Weekly => day.DayOfWeek switch
        {
            DayOfWeek.Monday => schedule.Monday,
            DayOfWeek.Tuesday => schedule.Tuesday,
            DayOfWeek.Wednesday => schedule.Wednesday,
            DayOfWeek.Thursday => schedule.Thursday,
            DayOfWeek.Friday => schedule.Friday,
            DayOfWeek.Saturday => schedule.Saturday,
            _ => schedule.Sunday
        },
        ScheduleKind.Monthly => day.Day == Math.Clamp(schedule.DayOfMonth, 1, DateTime.DaysInMonth(day.Year, day.Month)),
        _ => false
    };

    public static DateTime? LastDue(ScheduleSettings schedule, DateTime now)
    {
        if (schedule.Kind == ScheduleKind.Manual || !TryTime(schedule.Time, out var time)) return null;
        var start = schedule.Date.Date + time;
        if (now < start) return null;

        switch (schedule.Kind)
        {
            case ScheduleKind.Once:
                return start;
            case ScheduleKind.Interval:
                var step = TimeSpan.FromMinutes(Math.Max(1, schedule.IntervalMinutes)).Ticks;
                return start.AddTicks((now - start).Ticks / step * step);
            default:
                for (var day = now.Date; day >= start.Date && day >= now.Date.AddDays(-63); day = day.AddDays(-1))
                    if (Matches(schedule, day) && day + time <= now)
                        return day + time;
                return null;
        }
    }

    public static DateTime? NextDue(ScheduleSettings schedule, DateTime now)
    {
        if (schedule.Kind == ScheduleKind.Manual || !TryTime(schedule.Time, out var time)) return null;
        var start = schedule.Date.Date + time;

        switch (schedule.Kind)
        {
            case ScheduleKind.Once:
                return start > now ? start : null;
            case ScheduleKind.Interval:
                return LastDue(schedule, now)?.AddMinutes(Math.Max(1, schedule.IntervalMinutes)) ?? start;
            default:
                var from = now.Date > start.Date ? now.Date : start.Date;
                for (var day = from; day <= from.AddDays(63); day = day.AddDays(1))
                    if (Matches(schedule, day) && day + time > now)
                        return day + time;
                return null;
        }
    }
}
