using System.Collections.ObjectModel;

namespace WinBackup.Models;

public enum CopyMode
{
    Mirror,
    Update,
    Backup,
    Sync
}

public partial class Profile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "New Profile";
    public string Folder { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public CopyMode Mode { get; set; } = CopyMode.Update;
    public ObservableCollection<FolderEntry> Sources { get; set; } = new();
    public ObservableCollection<FolderEntry> Destinations { get; set; } = new();
    public bool SubfolderPerSource { get; set; }
}

public class FolderEntry
{
    public bool Enabled { get; set; } = true;
    public string Path { get; set; } = "";
    public string Subfolder { get; set; } = "";
}

public class ProfileData
{
    public List<string> Folders { get; set; } = new();
    public List<Profile> Profiles { get; set; } = new();
}
