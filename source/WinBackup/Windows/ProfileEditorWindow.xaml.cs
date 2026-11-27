using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using WinBackup.Managers;
using WinBackup.Models;
using DataFormats = System.Windows.DataFormats;
using DragEventArgs = System.Windows.DragEventArgs;
using DragDropEffects = System.Windows.DragDropEffects;

namespace WinBackup.Windows;

public partial class ProfileEditorWindow : Window
{
    private readonly Profile _profile;
    private bool _savedEnabled;

    public ProfileEditorWindow(Profile profile)
    {
        InitializeComponent();
        WindowPlacement.Track(this, "ProfileEditor");
        _profile = JsonStore.Clone(profile);
        Title = $"WinBackup — Profile — {_profile.Name}";
        FolderCombo.ItemsSource = FolderChoices();
        DataContext = _profile;
        App.Instance.Profiles.FolderMoved += FollowFolder;
        Closed += (_, _) => App.Instance.Profiles.FolderMoved -= FollowFolder;
        _savedEnabled = _profile.Enabled;
        App.Instance.Profiles.Changed += FollowEnabled;
        Closed += (_, _) => App.Instance.Profiles.Changed -= FollowEnabled;
    }

    private static List<string> FolderChoices() =>
        new[] { "" }.Concat(App.Instance.Profiles.Folders).ToList();

    private void FollowFolder(string oldName, string newName)
    {
        var selected = _profile.Folder ?? "";
        var wanted = selected.Equals(oldName, StringComparison.OrdinalIgnoreCase) ? newName : selected;
        var choices = FolderChoices();
        var match = choices.FirstOrDefault(c => c.Equals(wanted, StringComparison.OrdinalIgnoreCase)) ?? "";
        FolderCombo.ItemsSource = choices;
        FolderCombo.SelectedItem = match;
        _profile.Folder = match;
    }

    private void FollowEnabled()
    {
        var saved = App.Instance.Profiles.Find(_profile.Id);
        if (saved == null || saved.Enabled == _savedEnabled) return;
        _savedEnabled = saved.Enabled;
        _profile.Enabled = saved.Enabled;
        EnabledCheck.IsChecked = saved.Enabled;
    }

    private (ObservableCollection<FolderEntry> List, DataGrid Grid) Target(object sender) =>
        (string)((FrameworkElement)sender).Tag == "Source"
            ? (_profile.Sources, SourceGrid)
            : (_profile.Destinations, DestinationGrid);

    private string? PickFolder()
    {
        var dialog = new OpenFolderDialog { Title = "Select a folder" };
        return dialog.ShowDialog(this) == true ? dialog.FolderName : null;
    }

    private void CommitGrids()
    {
        SourceGrid.CommitEdit(DataGridEditingUnit.Row, true);
        DestinationGrid.CommitEdit(DataGridEditingUnit.Row, true);
    }

    private void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        CommitGrids();
        if (PickFolder() is { } path)
            Target(sender).List.Add(new FolderEntry { Path = path });
    }

    private void AddText_Click(object sender, RoutedEventArgs e)
    {
        CommitGrids();
        var text = InputDialog.Ask(this, "Add Folder", "Folder path (environment variables are allowed):");
        if (text != null) Target(sender).List.Add(new FolderEntry { Path = text });
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        CommitGrids();
        var (_, grid) = Target(sender);
        if (grid.SelectedItem is not FolderEntry entry || PickFolder() is not { } path) return;
        entry.Path = path;
        grid.Items.Refresh();
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        CommitGrids();
        var (list, grid) = Target(sender);
        if (grid.SelectedItem is FolderEntry entry) list.Remove(entry);
    }

    private void Grid_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Link : DragDropEffects.None;
        e.Handled = true;
    }

    private void Grid_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return;
        CommitGrids();
        var list = sender == SourceGrid ? _profile.Sources : _profile.Destinations;
        foreach (var path in paths.Where(Directory.Exists))
            list.Add(new FolderEntry { Path = path });
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ((Button)sender).Focus();
        CommitGrids();
        _profile.Name = _profile.Name.Trim();
        _profile.Folder ??= "";

        var errors = new List<string>();
        if (_profile.Name.Length == 0) errors.Add("Enter a profile name.");
        if (App.Instance.Profiles.NameTaken(_profile.Name, _profile.Id)) errors.Add("Another profile already uses this name.");
        Validate(_profile, errors);

        if (errors.Count > 0)
        {
            MessageBox.Show(this, string.Join("\n", errors), "WinBackup", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        App.Instance.Profiles.Put(_profile);
        Close();
    }

    private static void Validate(Profile profile, List<string> errors) =>
        errors.AddRange(Engine.ProfileValidator.Validate(profile));

    private void BrowseVersions_Click(object sender, RoutedEventArgs e)
    {
        if (PickFolder() is { } path) VersionFolderBox.Text = path;
        VersionFolderBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
