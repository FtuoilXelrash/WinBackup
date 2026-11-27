using System.Collections.ObjectModel;
using WinBackup.Models;

namespace WinBackup.ViewModels;

public class ProfileNode
{
    public string Name { get; init; } = "";
    public Profile? Profile { get; init; }
    public bool IsFolder => Profile == null;
    public ObservableCollection<ProfileNode> Children { get; } = new();
    public bool IsExpanded { get; set; } = true;
    public bool IsSelected { get; set; }

    public bool IsChecked
    {
        get => Profile?.Enabled ?? false;
        set
        {
            if (Profile == null || Profile.Enabled == value) return;
            Profile.Enabled = value;
            App.Instance.Dispatcher.BeginInvoke(App.Instance.Profiles.Commit);
        }
    }
}
