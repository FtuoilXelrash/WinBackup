using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using WinBackup.Managers;
using WinBackup.Models;
using WinBackup.Services;

namespace WinBackup.Windows;

public partial class ReportsWindow : Window
{
    public ReportsWindow()
    {
        InitializeComponent();
        WindowPlacement.Track(this, "Reports");
        Action<RunReport> completed = _ => Dispatcher.BeginInvoke(Reload);
        App.Instance.Jobs.Completed += completed;
        Closed += (_, _) => App.Instance.Jobs.Completed -= completed;
        Reload();
    }

    private void Reload()
    {
        var reports = ReportStore.List();
        RunList.ItemsSource = reports;
        if (reports.Count > 0) RunList.SelectedIndex = 0;
        else SummaryText.Text = "There are no reports yet.";
    }

    private void Run_Changed(object sender, SelectionChangedEventArgs e)
    {
        EntryGrid.ItemsSource = null;
        if (RunList.SelectedItem is not ReportFile file) return;
        try
        {
            var report = ReportStore.Load(file.Path);
            SummaryText.Text = $"{report.ProfileName}  ({report.Trigger})\n" +
                               $"{report.Started:yyyy-MM-dd HH:mm:ss} to {report.Finished:HH:mm:ss}\n{report.Summary}";
            EntryGrid.ItemsSource = new ListCollectionView(report.Entries);
            ApplyLevel();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SummaryText.Text = ex.Message;
        }
    }

    private void ApplyLevel()
    {
        if (EntryGrid?.ItemsSource is not ListCollectionView view) return;
        var choice = LevelCombo.SelectedIndex;
        view.Filter = o => o is ReportEntry entry && choice switch
        {
            1 => entry.Level == EntryLevel.Error,
            2 => entry.Level != EntryLevel.Info,
            _ => true
        };
    }

    private void Level_Changed(object sender, SelectionChangedEventArgs e) => ApplyLevel();
}
