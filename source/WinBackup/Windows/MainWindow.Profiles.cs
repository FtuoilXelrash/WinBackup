using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WinBackup.Models;
using WinBackup.ViewModels;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace WinBackup.Windows;

public partial class MainWindow
{
    private readonly Dictionary<string, ProfileEditorWindow> _editors = new();
    private readonly HashSet<string> _collapsed = new();
    private List<ProfileNode> _nodes = new();
    private string? _selectedKey;

    private ProfileNode? SelectedNode => ProfileTree.SelectedItem as ProfileNode;

    partial void InitProfilesPanel()
    {
        ProfileTree.SelectedItemChanged += (_, _) =>
        {
            if (SelectedNode is { } node) _selectedKey = KeyOf(node);
            ShowDetails();
        };
        App.Instance.Profiles.Changed += RebuildTree;
        Closed += (_, _) => App.Instance.Profiles.Changed -= RebuildTree;
        RebuildTree();
    }

    private static string KeyOf(ProfileNode node) => node.Profile?.Id ?? "folder:" + node.Name;

    private void RebuildTree()
    {
        foreach (var folder in _nodes.Where(n => n.IsFolder))
        {
            if (folder.IsExpanded) _collapsed.Remove(folder.Name);
            else _collapsed.Add(folder.Name);
        }

        var manager = App.Instance.Profiles;
        var roots = new List<ProfileNode>();
        foreach (var name in manager.Folders.OrderBy(f => f, StringComparer.CurrentCultureIgnoreCase))
        {
            var folder = new ProfileNode { Name = name, IsExpanded = !_collapsed.Contains(name) };
            foreach (var profile in Ordered(manager.Profiles.Where(p => p.Folder.Equals(name, StringComparison.OrdinalIgnoreCase))))
                folder.Children.Add(Node(profile));
            folder.IsSelected = KeyOf(folder) == _selectedKey;
            roots.Add(folder);
        }
        roots.AddRange(Ordered(manager.Profiles.Where(p => p.Folder.Length == 0)).Select(Node));

        _nodes = roots;
        ProfileTree.ItemsSource = _nodes;
        ShowDetails();
    }

    private static IEnumerable<Profile> Ordered(IEnumerable<Profile> profiles) =>
        profiles.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase);

    private ProfileNode Node(Profile profile) =>
        new() { Name = profile.Name, Profile = profile, IsSelected = profile.Id == _selectedKey };

    private void ShowDetails()
    {
        var node = SelectedNode ?? _nodes.SelectMany(n => n.Children.Prepend(n)).FirstOrDefault(n => n.IsSelected);
        if (node == null)
        {
            DetailTitle.Text = App.Instance.Profiles.Profiles.Count == 0 ? "No profiles yet" : "Select a profile";
            DetailText.Text = App.Instance.Profiles.Profiles.Count == 0 ? "Choose New Profile to create your first backup job." : "";
        }
        else if (node.Profile == null)
        {
            DetailTitle.Text = node.Name;
            DetailText.Text = $"Folder with {node.Children.Count} profile(s), {node.Children.Count(c => c.IsChecked)} enabled.";
        }
        else
        {
            DetailTitle.Text = node.Profile.Name;
            DetailText.Text = Describe(node.Profile);
        }
    }

    private string Describe(Profile profile)
    {
        var text = new StringBuilder();
        text.AppendLine($"Mode: {profile.Mode}");
        text.AppendLine($"Status: {(profile.Enabled ? "Enabled" : "Disabled")}");
        text.AppendLine();
        text.AppendLine("Sources:");
        foreach (var entry in profile.Sources)
            text.AppendLine($"   {entry.Path}{(entry.Enabled ? "" : "  (not used)")}");
        text.AppendLine("Destinations:");
        foreach (var entry in profile.Destinations)
            text.AppendLine($"   {entry.Path}{(entry.Enabled ? "" : "  (not used)")}");
        DescribeMore(profile, text);
        return text.ToString();
    }

    partial void DescribeMore(Profile profile, StringBuilder text);

    private void OpenEditor(Profile profile)
    {
        if (!_editors.TryGetValue(profile.Id, out var editor))
        {
            editor = new ProfileEditorWindow(profile);
            editor.Closed += (_, _) => _editors.Remove(profile.Id);
            _editors[profile.Id] = editor;
        }
        editor.Show();
        editor.Activate();
    }

    private void NewProfile_Click(object sender, RoutedEventArgs e)
    {
        var folder = SelectedNode is { IsFolder: true } node ? node.Name : SelectedNode?.Profile?.Folder ?? "";
        OpenEditor(new Profile { Name = App.Instance.Profiles.UniqueName("New Profile"), Folder = folder });
    }

    private void NewFolder_Click(object sender, RoutedEventArgs e)
    {
        if (InputDialog.Ask(this, "New Folder", "Folder name:") is { } name)
            App.Instance.Profiles.AddFolder(name);
    }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedNode?.Profile is { } profile) OpenEditor(profile);
    }

    private void Copy_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedNode?.Profile is { } profile) App.Instance.Profiles.Duplicate(profile);
    }

    private void Rename_Click(object sender, RoutedEventArgs e)
    {
        var node = SelectedNode;
        if (node == null) return;
        var name = InputDialog.Ask(this, "Rename", "New name:", node.Name);
        if (name == null || name == node.Name) return;

        if (node.Profile is not { } profile)
        {
            App.Instance.Profiles.RenameFolder(node.Name, name);
        }
        else if (App.Instance.Profiles.NameTaken(name, profile.Id))
        {
            Warn("Another profile already uses this name.");
        }
        else if (_editors.ContainsKey(profile.Id))
        {
            Warn("Close the profile editor before renaming this profile.");
        }
        else
        {
            profile.Name = name;
            App.Instance.Profiles.Put(profile);
        }
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        var node = SelectedNode;
        if (node == null) return;
        var question = node.IsFolder
            ? $"Delete the folder \"{node.Name}\"?\n\nIts profiles are moved to the top level."
            : $"Delete the profile \"{node.Name}\"?\n\nNo backed up files are removed.";
        if (MessageBox.Show(this, question, "WinBackup", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        if (node.Profile is { } profile)
        {
            if (_editors.TryGetValue(profile.Id, out var editor)) editor.Close();
            App.Instance.Profiles.Delete(profile);
        }
        else
        {
            App.Instance.Profiles.DeleteFolder(node.Name);
        }
    }

    private void Warn(string message) =>
        MessageBox.Show(this, message, "WinBackup", MessageBoxButton.OK, MessageBoxImage.Warning);

    private void Tree_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && Ancestor<TreeViewItem>(source)?.DataContext is ProfileNode { Profile: { } profile }
            && Ancestor<System.Windows.Controls.CheckBox>(source) == null)
            OpenEditor(profile);
    }

    private void Tree_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete) Delete_Click(sender, e);
        else if (e.Key == Key.F2) Rename_Click(sender, e);
        else if (e.Key == Key.Enter) Edit_Click(sender, e);
    }

    private void Tree_RightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && Ancestor<TreeViewItem>(source) is { } item)
            item.IsSelected = true;
    }

    private static T? Ancestor<T>(DependencyObject? element) where T : DependencyObject
    {
        while (element != null)
        {
            if (element is T match) return match;
            element = element is Visual ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);
        }
        return null;
    }
}
