using System.Text.Json;
using System.Windows.Threading;
using WinBackup.Engine;
using WinBackup.Managers;
using WinBackup.Models;

namespace WinBackup.Services;

public sealed class SchedulerService
{
    public sealed class Mark
    {
        public string Signature { get; set; } = "";
        public DateTime Handled { get; set; }
    }

    private static readonly TimeSpan Tolerance = TimeSpan.FromMinutes(5);
    private static string FilePath => Path.Combine(AppPaths.Config, "schedule.json");

    private readonly Func<IReadOnlyList<Profile>> _profiles;
    private readonly Func<Profile, string, bool> _start;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(15) };
    private Dictionary<string, Mark> _marks = new();

    public SchedulerService(Func<IReadOnlyList<Profile>> profiles, Func<Profile, string, bool> start)
    {
        _profiles = profiles;
        _start = start;
        _timer.Tick += (_, _) => Tick(DateTime.Now);
    }

    public void Start()
    {
        try
        {
            _marks = JsonStore.Load<Dictionary<string, Mark>>(FilePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogService.Error($"The schedule history could not be read: {ex.Message}");
        }

        Tick(DateTime.Now);
        foreach (var profile in _profiles().Where(p => p.Enabled && p.Schedule.OnStartup).ToList())
            _start(profile, "Application start");
        _timer.Start();
    }

    public void Stop() => _timer.Stop();

    public void Tick(DateTime now)
    {
        var changed = false;
        var active = _profiles().Where(p => p.Enabled && p.Schedule.Kind != ScheduleKind.Manual).ToList();

        foreach (var gone in _marks.Keys.Except(active.Select(p => p.Id)).ToList())
        {
            _marks.Remove(gone);
            changed = true;
        }

        foreach (var profile in active)
        {
            var signature = JsonSerializer.Serialize(profile.Schedule);
            if (!_marks.TryGetValue(profile.Id, out var mark) || mark.Signature != signature)
            {
                _marks[profile.Id] = new Mark { Signature = signature, Handled = now };
                changed = true;
                continue;
            }

            var due = ScheduleCalculator.LastDue(profile.Schedule, now);
            if (due == null || due <= mark.Handled) continue;

            mark.Handled = due.Value;
            changed = true;
            if (now - due > Tolerance && !profile.Schedule.RunMissed)
                LogService.Warning($"{profile.Name}: the run scheduled for {due:yyyy-MM-dd HH:mm} was missed.");
            else if (!_start(profile, "Schedule"))
                LogService.Warning($"{profile.Name}: scheduled run skipped because it is already running or waiting.");
        }

        if (!changed) return;
        try
        {
            JsonStore.Save(FilePath, _marks);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogService.Error($"The schedule history could not be saved: {ex.Message}");
        }
    }
}
