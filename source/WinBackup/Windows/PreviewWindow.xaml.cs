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
    private readonly Profile _profile;
    private RunControl? _control;
    private JobPlan? _plan;

    public PreviewWindow(Profile profile)
    {
        InitializeComponent();
        WindowPlacement.Track(this, "Preview");
        _profile = JsonStore.Clone(profile);
        Title = $"WinBackup — Preview — {_profile.Name}";
        Loaded += (_, _) => Build();
        Closed += (_, _) => _control?.Cancel();
    }

    private async void Build()
    {
        var control = _control = new RunControl();
        _plan = null;
        ItemGrid.ItemsSource = null;
        BusyPanel.Visibility = Visibility.Visible;
        WarningBox.Visibility = Visibility.Collapsed;
        RunButton.IsEnabled = RefreshButton.IsEnabled = false;
        SummaryText.Text = "";

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
    }

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
