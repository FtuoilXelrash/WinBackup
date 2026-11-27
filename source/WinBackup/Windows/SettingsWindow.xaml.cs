using System.Windows;
using System.Windows.Controls;
using WinBackup.Managers;

namespace WinBackup.Windows;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        WindowPlacement.Track(this, "Settings");
        Reload();
    }

    private void Reload()
    {
        DataContext = JsonStore.Clone(App.Instance.Settings.Current);
        LoadExtras();
    }

    partial void LoadExtras();
    partial void StoreExtras();

    private void Commit(object sender)
    {
        (sender as Button)?.Focus();
        StoreExtras();
        App.Instance.Settings.Apply((Models.AppSettings)DataContext);
        if (!App.Instance.Settings.Save())
            MessageBox.Show(this, "The settings could not be saved to the config folder.",
                "WinBackup", MessageBoxButton.OK, MessageBoxImage.Warning);
        App.Instance.ApplySettings();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        Commit(sender);
        Close();
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        Commit(sender);
        Reload();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
