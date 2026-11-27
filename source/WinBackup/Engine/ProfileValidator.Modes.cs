using WinBackup.Models;

namespace WinBackup.Engine;

public static partial class ProfileValidator
{
    static partial void RequireSubfolders(Profile profile, ref bool required)
    {
        if (profile.Mode == CopyMode.Backup && Used(profile.Sources).Count > 1)
            required = true;
    }

    static partial void ValidateMore(Profile profile, List<string> sources, List<string> destinations, List<string> errors)
    {
        if (profile.Mode == CopyMode.Backup && Planner.BackupTitle(profile, DateTime.Now).Length == 0)
            errors.Add("Enter a name for the backup folder or ZIP file.");

        if (profile.Mode == CopyMode.Sync && destinations.Count > 1)
            errors.Add("Sync works with one destination folder.");

        var schedule = profile.Schedule;
        if (schedule.Kind != ScheduleKind.Manual && !ScheduleCalculator.TryTime(schedule.Time, out _))
            errors.Add("Enter the schedule time as hours and minutes, for example 18:30.");
        if (schedule.Kind == ScheduleKind.Weekly && !(schedule.Monday || schedule.Tuesday || schedule.Wednesday
                || schedule.Thursday || schedule.Friday || schedule.Saturday || schedule.Sunday))
            errors.Add("Select at least one day for the weekly schedule.");
    }
}
