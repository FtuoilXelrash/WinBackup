using System.Text;
using System.Windows;
using System.Windows.Input;
using WinBackup.Engine;
using WinBackup.Models;

namespace WinBackup.Windows;

public partial class MainWindow
{
    partial void InitJobPanel()
    {
        var jobs = App.Instance.Jobs;
        Action<JobProgress> progress = p => Dispatcher.BeginInvoke(() => ShowProgress(p));
        Action state = () => Dispatcher.BeginInvoke(ShowJobState);
        Action<RunReport> completed = report => Dispatcher.BeginInvoke(() =>
        {
            StatusLabel.Text = $"{report.ProfileName} — {report.Summary}";
            ShowDetails();
        });

        jobs.Progress += progress;
        jobs.StateChanged += state;
        jobs.Completed += completed;
        Closed += (_, _) =>
        {
            jobs.Progress -= progress;
            jobs.StateChanged -= state;
            jobs.Completed -= completed;
        };

        InputBindings.Add(new KeyBinding(new RelayCommand(RunSelected), Key.F5, ModifierKeys.None));
        ShowJobState();
    }

    partial void DescribeMore(Profile profile, StringBuilder text)
    {
        if (App.Instance.LastRuns.TryGetValue(profile.Id, out var report))
        {
            text.AppendLine();
            text.AppendLine($"Last run: {report.Finished:yyyy-MM-dd HH:mm}");
            text.AppendLine(report.Summary);
        }
        DescribeSchedule(profile, text);
    }

    partial void DescribeSchedule(Profile profile, StringBuilder text);

    private void ShowProgress(JobProgress progress)
    {
        if (!App.Instance.Jobs.IsBusy) return;
        var scanning = progress.BytesTotal == 0 && progress.FilesTotal == 0;
        JobProgressBar.IsIndeterminate = scanning && !App.Instance.Jobs.IsPaused;
        if (!scanning)
            JobProgressBar.Value = progress.BytesTotal > 0
                ? Math.Min(100, progress.BytesDone * 100.0 / progress.BytesTotal)
                : progress.FilesDone * 100.0 / Math.Max(1, progress.FilesTotal);

        JobTitle.Text = scanning
            ? $"{progress.Profile} — {progress.Phase}"
            : $"{progress.Profile} — {progress.Phase}   {progress.FilesDone} of {progress.FilesTotal} files, " +
              $"{Size(progress.BytesDone)} of {Size(progress.BytesTotal)}";
        JobPath.Text = progress.CurrentPath;
    }

    private static string Size(long bytes) => bytes switch
    {
        >= 1L << 30 => $"{bytes / (double)(1L << 30):N2} GB",
        >= 1L << 20 => $"{bytes / (double)(1L << 20):N1} MB",
        _ => $"{bytes / 1024.0:N0} KB"
    };

    private void ShowJobState()
    {
        var jobs = App.Instance.Jobs;
        JobPanel.Visibility = jobs.IsBusy ? Visibility.Visible : Visibility.Collapsed;
        PauseButton.Content = jobs.IsPaused ? "Resume" : "Pause";
        if (!jobs.IsBusy)
        {
            JobProgressBar.Value = 0;
            JobProgressBar.IsIndeterminate = false;
            return;
        }
        if (jobs.IsPaused) JobProgressBar.IsIndeterminate = false;
        StatusLabel.Text = jobs.IsPaused ? "Paused" : jobs.Waiting > 0 ? $"Running — {jobs.Waiting} waiting" : "Running";
    }

    private List<Profile> SelectedProfiles()
    {
        if (SelectedNode?.Profile is { } profile) return new List<Profile> { profile };
        return SelectedNode?.Children.Select(c => c.Profile!).Where(p => p.Enabled).ToList() ?? new List<Profile>();
    }

    private void Queue(List<Profile> profiles, string empty)
    {
        if (profiles.Count == 0)
        {
            StatusLabel.Text = empty;
            return;
        }
        foreach (var item in profiles)
            if (!App.Instance.Start(item, "Manual"))
                StatusLabel.Text = $"{item.Name} is already running or waiting.";
    }

    private void UpdateActions()
    {
        var node = SelectedNode;
        PreviewButton.IsEnabled = PreviewMenuItem.IsEnabled = PreviewContextItem.IsEnabled = node?.Profile != null;
        StartButton.IsEnabled = StartMenuItem.IsEnabled = StartContextItem.IsEnabled = node != null;
    }

    private void RunSelected() => Queue(SelectedProfiles(), "Select a profile or a folder with enabled profiles.");

    private void Run_Click(object sender, RoutedEventArgs e) => RunSelected();

    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedNode?.Profile is { } profile)
            new PreviewWindow(profile).Show();
        else
            StatusLabel.Text = "Select a profile to preview.";
    }

    private void RunAll_Click(object sender, RoutedEventArgs e) =>
        Queue(App.Instance.Profiles.Profiles.Where(p => p.Enabled).ToList(), "There are no enabled profiles.");

    private void Pause_Click(object sender, RoutedEventArgs e) => App.Instance.Jobs.TogglePause();

    private void CancelJobs_Click(object sender, RoutedEventArgs e) => App.Instance.Jobs.CancelAll();
}

public sealed class RelayCommand : ICommand
{
    private readonly Action _action;

    public RelayCommand(Action action) => _action = action;

    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => _action();
}
