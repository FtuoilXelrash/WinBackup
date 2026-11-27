using WinBackup.Services;

namespace WinBackup;

public partial class App
{
    private SchedulerService? _scheduler;

    partial void InitScheduler()
    {
        _scheduler = new SchedulerService(() => Profiles.Profiles, Start);
        Profiles.Changed += () => Dispatcher.BeginInvoke(() => _scheduler.Tick(DateTime.Now));
        Dispatcher.BeginInvoke(_scheduler.Start);
        Exit += (_, _) => _scheduler.Stop();
    }
}
