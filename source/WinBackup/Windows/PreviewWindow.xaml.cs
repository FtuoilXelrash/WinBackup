using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using WinBackup.Engine;
using WinBackup.Managers;
using WinBackup.Models;
using WinBackup.Services;

namespace WinBackup.Windows;

public partial class PreviewWindow : Window
{
    private Profile _profile;
    private RunControl? _control;
    private JobPlan? _plan;

    public PreviewWindow(Profile profile)
    {
        InitializeComponent();
        WindowPlacement.Track(this, "Preview");
        _profile = JsonStore.Clone(profile);
        Loaded += (_, _) => Build();
        Closed += (_, _) => _control?.Cancel();
    }

    private bool LoadProfile()
    {
        var saved = App.Instance.Profiles.Find(_profile.Id);
        if (saved != null)
        {
            _profile = JsonStore.Clone(saved);
            Title = $"WinBackup — Preview — {_profile.Name}";
            return true;
        }

        _plan = null;
        ItemGrid.ItemsSource = null;
        WarningBox.Visibility = Visibility.Collapsed;
        BusyPanel.Visibility = Visibility.Collapsed;
        RunButton.IsEnabled = false;
        SummaryText.Text = "This profile no longer exists.";
        TotalsText.Visibility = Visibility.Collapsed;
        UpToDateText.Visibility = Visibility.Collapsed;
        return false;
    }

    private async void Build()
    {
        if (!LoadProfile()) return;
        var control = _control = new RunControl();
        _plan = null;
        ItemGrid.ItemsSource = null;
        BusyPanel.Visibility = Visibility.Visible;
        WarningBox.Visibility = Visibility.Collapsed;
        RunButton.IsEnabled = RefreshButton.IsEnabled = false;
        SummaryText.Text = "";
        TotalsText.Visibility = Visibility.Collapsed;
        UpToDateText.Visibility = Visibility.Collapsed;

        var throttle = Stopwatch.StartNew();
        try
        {
            var plan = await Task.Run(() => Planner.Build(_profile, control, path =>
            {
                if (throttle.ElapsedMilliseconds < 150) return;
                throttle.Restart();
                Dispatcher.BeginInvoke(() => BusyPath.Text = path);
            }));
            Show(plan);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            LogService.Error(ex.ToString());
            SummaryText.Text = "The preview failed: " + ex.Message;
        }
        finally
        {
            control.Dispose();
            if (_control == control) _control = null;
            BusyPanel.Visibility = Visibility.Collapsed;
            RefreshButton.IsEnabled = true;
        }
    }

    private void Show(JobPlan plan)
    {
        _plan = plan;
        var items = plan.Pairs.SelectMany(p => p.Items).ToList();
        ItemGrid.ItemsSource = new ListCollectionView(items);
        ApplyFilter();

        var warnings = new List<string>(plan.Failures);
        foreach (var pair in plan.Pairs)
        {
            if (pair.Failure != null) warnings.Add($"{pair.SourceRoot} → {pair.DestinationRoot}: {pair.Failure}");
            if (pair.BlockReason != null) warnings.Add($"SAFEGUARD {pair.SourceRoot} → {pair.DestinationRoot}: {pair.BlockReason}");
            warnings.AddRange(pair.Problems.Select(p => $"{p.Path}: {p.Message}"));
        }
        WarningBox.Text = string.Join(Environment.NewLine, warnings.Take(500));
        WarningBox.Visibility = warnings.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        RunButton.IsEnabled = plan.Failures.Count == 0 && items.Count > 0;
        UpdateSummary();
        ShowTotals(plan);
        UpToDateText.Visibility = IsUpToDate(plan) ? Visibility.Visible : Visibility.Collapsed;
    }

    private static bool IsUpToDate(JobPlan plan) =>
        plan.Profile.Mode != CopyMode.Backup
        && plan.Failures.Count == 0
        && plan.Pairs.Count > 0
        && plan.Pairs.All(p => p.Failure == null && p.Items.Count == 0 && p.Problems.All(x => !x.IsError));

    private void ShowTotals(JobPlan plan)
    {
        TotalsText.Visibility = plan.Pairs.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        var sources = Describe(plan.Pairs
            .Where(p => p.SourceTotals != null)
            .DistinctBy(p => p.SourceRoot, StringComparer.OrdinalIgnoreCase)
            .Select(p => p.SourceTotals!));
        var destinations = plan.Profile.Mode == CopyMode.Backup
            ? "not compared in Backup mode"
            : Describe(plan.Pairs
                .Where(p => p.DestinationTotals != null)
                .DistinctBy(p => p.DestinationRoot, StringComparer.OrdinalIgnoreCase)
                .Select(p => p.DestinationTotals!));
        var failed = plan.Pairs.Count(p => p.SourceTotals == null);

        TotalsText.Text = $"Source: {sources}\nDestination: {destinations}\nTime taken: {Duration(plan.Elapsed)}" +
                          (failed > 0 ? $"    NOT COMPARED: {failed} folder pair(s), see the warnings below" : "");
    }

    private static string Describe(IEnumerable<ScanTotals> totals)
    {
        var list = totals.ToList();
        return $"{list.Sum(t => (long)t.Files):N0} files, {list.Sum(t => (long)t.Folders):N0} folders, " +
               $"{Size(list.Sum(t => t.Bytes))}    Skipped by filters: {list.Sum(t => (long)t.SkippedFiles):N0} files, " +
               $"{list.Sum(t => (long)t.SkippedFolders):N0} folders";
    }

    private static string Size(long bytes) => bytes switch
    {
        >= 1L << 40 => $"{bytes / (double)(1L << 40):N2} TB",
        >= 1L << 30 => $"{bytes / (double)(1L << 30):N2} GB",
        >= 1L << 20 => $"{bytes / (double)(1L << 20):N1} MB",
        _ => $"{bytes / 1024.0:N0} KB"
    };

    private static string Duration(TimeSpan time) => time.TotalMinutes >= 1
        ? $"{(int)time.TotalMinutes} min {time.Seconds} sec"
        : $"{time.TotalSeconds:N1} sec";

    private void UpdateSummary()
    {
        if (_plan == null) return;
        var items = _plan.Pairs.SelectMany(p => p.Items).ToList();
        var copies = items.Count(i => i.Action is ItemAction.CopyToDestination or ItemAction.CopyToSource);
        var deletes = items.Count(i => i.Action is ItemAction.DeleteDestination or ItemAction.DeleteSource);
        SummaryText.Text = items.Count == 0
            ? "Nothing to do."
            : $"{copies:N0} to copy, {deletes:N0} to delete, {items.Count - copies - deletes:N0} skipped";
    }

    private void ApplyFilter()
    {
        if (ItemGrid?.ItemsSource is not ListCollectionView view) return;
        var choice = FilterCombo.SelectedIndex;
        view.Filter = o => o is PlanItem item && choice switch
        {
            1 => item.Action is ItemAction.CopyToDestination or ItemAction.CopyToSource,
            2 => item.Action is ItemAction.DeleteDestination or ItemAction.DeleteSource,
            3 => item.Action == ItemAction.None,
            _ => true
        };
    }

    private void Filter_Changed(object sender, SelectionChangedEventArgs e) => ApplyFilter();

    private void Menu_Opening(object sender, ContextMenuEventArgs e)
    {
        var selected = ItemGrid.SelectedItems.OfType<PlanItem>().ToList();
        foreach (var menu in ActionMenu.Items.OfType<MenuItem>())
        {
            var action = Enum.Parse<ItemAction>((string)menu.Tag);
            menu.IsEnabled = selected.Any(i => PlanRules.Allowed(_profile.Mode, i, action));
        }
    }

    private void SetAction_Click(object sender, RoutedEventArgs e)
    {
        var action = Enum.Parse<ItemAction>((string)((MenuItem)sender).Tag);
        foreach (var item in ItemGrid.SelectedItems.OfType<PlanItem>().ToList())
            if (PlanRules.Allowed(_profile.Mode, item, action))
                item.Action = action;
        UpdateSummary();
    }

    private void Run_Click(object sender, RoutedEventArgs e)
    {
        if (_plan == null) return;
        var blocked = _plan.Pairs.Where(p => p.BlockReason != null).Select(p => p.BlockReason!).ToList();
        if (blocked.Count > 0 && MessageBox.Show(this,
                $"The safeguard stopped this run:\n\n{string.Join("\n", blocked)}\n\nRun it anyway?",
                "WinBackup", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
            return;

        if (!App.Instance.Jobs.Enqueue(new JobRequest(_plan.Profile, "Preview", _plan, blocked.Count > 0)))
        {
            SummaryText.Text = "This profile is already running or waiting.";
            return;
        }
        Close();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => Build();

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
