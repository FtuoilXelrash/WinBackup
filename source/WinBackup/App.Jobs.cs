using System.Windows;
using WinBackup.Managers;
using WinBackup.Models;
using WinBackup.Services;

namespace WinBackup;

public partial class App
{
    public JobRunner Jobs { get; } = new();
    public Dictionary<string, RunReport> LastRuns { get; } = new();

    private bool _lastRunFailed;

    partial void InitJobs()
    {
        Jobs.StateChanged += () => Dispatcher.BeginInvoke(UpdateTrayState);
        ReportStore.Prune(Settings.Current.LogRetentionDays);
        Jobs.Completed += ReportStore.Save;
        Jobs.Completed += report => Dispatcher.BeginInvoke(() => JobFinished(report));
        SessionEnding += (_, _) => StopJobs();
    }

    public bool Start(Profile profile, string trigger) =>
        Jobs.Enqueue(new JobRequest(JsonStore.Clone(profile), trigger));

    private void JobFinished(RunReport report)
    {
        LastRuns[report.ProfileId] = report;
        _lastRunFailed = report.Outcome == RunOutcome.Errors;
        UpdateTrayState();

        var problem = report.Outcome != RunOutcome.Success;
        if (Settings.Current.ShowNotifications && (problem || !Settings.Current.NotifyOnlyProblems))
            Tray?.Notify($"WinBackup — {report.ProfileName}", report.Summary, problem);
    }

    private void UpdateTrayState()
    {
        if (IsExiting) return;
        if (Jobs.IsPaused) Tray?.SetState(TrayState.Paused, $"WinBackup — paused: {Jobs.CurrentName}");
        else if (Jobs.IsBusy) Tray?.SetState(TrayState.Running, $"WinBackup — running: {Jobs.CurrentName}");
        else if (_lastRunFailed) Tray?.SetState(TrayState.Error, "WinBackup — the last run had errors");
        else Tray?.SetState(TrayState.Idle, "WinBackup");
    }

    partial void ConfirmBusyExit(ref bool allowed)
    {
        if (!Jobs.IsBusy) return;
        allowed = MessageBox.Show("A job is still running. Cancel it and exit?", "WinBackup",
            MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }

    private void StopJobs()
    {
        Jobs.CancelAll();
        Jobs.WaitForIdle(TimeSpan.FromSeconds(15));
    }

    partial void CloseServices()
    {
        StopJobs();
        LogService.Info("Application closed");
    }
}
