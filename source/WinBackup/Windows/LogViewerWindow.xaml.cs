using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WinBackup.Managers;
using WinBackup.Services;

namespace WinBackup.Windows;

public partial class LogViewerWindow : Window
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(2) };
    private (string Path, long Length, string Filter) _shown;

    public LogViewerWindow()
    {
        InitializeComponent();
        WindowPlacement.Track(this, "LogViewer");
        LoadFiles();
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();
        Closed += (_, _) => _timer.Stop();
    }

    private void LoadFiles()
    {
        var files = LogService.Files().Select(f => new FileInfo(f)).ToList();
        FileCombo.ItemsSource = files;
        if (files.Count > 0) FileCombo.SelectedIndex = 0;
    }

    private void Refresh()
    {
        if (FileCombo.SelectedItem is not FileInfo file) { LoadFiles(); return; }
        try
        {
            var current = new FileInfo(file.FullName);
            var key = (file.FullName, current.Exists ? current.Length : 0, FilterBox.Text);
            if (key == _shown) return;
            _shown = key;

            var text = LogService.Read(file.FullName);
            if (FilterBox.Text.Length > 0)
                text = string.Join(Environment.NewLine, text.Split(Environment.NewLine)
                    .Where(l => l.Contains(FilterBox.Text, StringComparison.OrdinalIgnoreCase)));
            LogText.Text = text;
            LogText.ScrollToEnd();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogText.Text = ex.Message;
        }
    }

    private void Selection_Changed(object sender, SelectionChangedEventArgs e) => Refresh();

    private void Filter_Changed(object sender, TextChangedEventArgs e) => Refresh();

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(AppPaths.Logs);
        Process.Start(new ProcessStartInfo(AppPaths.Logs) { UseShellExecute = true });
    }
}
