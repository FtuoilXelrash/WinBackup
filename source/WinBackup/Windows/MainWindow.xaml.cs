using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using WinBackup.Managers;

namespace WinBackup.Windows;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        WindowPlacement.Track(this, "Main");
        VersionLabel.Text = $"WinBackup v{AboutWindow.VersionText}";
        InitViewMenu();
        InitProfilesPanel();
        InitJobPanel();
    }

    partial void InitViewMenu();
    partial void InitProfilesPanel();
    partial void InitJobPanel();

    private void AddViewItem(string header, Action action)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => action();
        ViewMenu.Items.Add(item);
    }

    private void Window_Closing(object? sender, CancelEventArgs e) =>
        e.Cancel = App.Instance.MainWindowClosing(this);

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        if (App.Instance.ConfirmExit()) App.Instance.RequestExit();
    }

    private void About_Click(object sender, RoutedEventArgs e) =>
        new AboutWindow { Owner = this }.ShowDialog();
}
