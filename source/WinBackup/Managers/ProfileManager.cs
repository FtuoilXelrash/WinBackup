using WinBackup.Models;
using WinBackup.Services;

namespace WinBackup.Managers;

public class ProfileManager
{
    private static string FilePath => Path.Combine(AppPaths.Config, "profiles.json");

    private ProfileData _data = new();
    private bool _writable = true;

    public event Action? Changed;
    public event Action<string, string>? FolderMoved;

    public IReadOnlyList<Profile> Profiles => _data.Profiles;
    public IReadOnlyList<string> Folders => _data.Folders;

    public void Load()
    {
        try
        {
            _data = JsonStore.Load<ProfileData>(FilePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _writable = false;
            LogService.Error($"Profiles could not be read and will not be overwritten: {ex.Message}");
        }
    }

    public Profile? Find(string id) => _data.Profiles.FirstOrDefault(p => p.Id == id);

    public void Put(Profile profile)
    {
        var index = _data.Profiles.FindIndex(p => p.Id == profile.Id);
        if (index < 0) _data.Profiles.Add(profile);
        else _data.Profiles[index] = profile;
        EnsureFolder(profile.Folder);
        Commit();
    }

    public Profile Duplicate(Profile source)
    {
        var copy = JsonStore.Clone(source);
        copy.Id = Guid.NewGuid().ToString("N");
        copy.Name = UniqueName(source.Name + " - Copy");
        Put(copy);
        return copy;
    }

    public string UniqueName(string wanted)
    {
        var name = wanted;
        for (var i = 2; _data.Profiles.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)); i++)
            name = $"{wanted} ({i})";
        return name;
    }

    public bool NameTaken(string name, string exceptId) =>
        _data.Profiles.Any(p => p.Id != exceptId && p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    public void Delete(Profile profile)
    {
        _data.Profiles.RemoveAll(p => p.Id == profile.Id);
        Commit();
    }

    public void AddFolder(string name)
    {
        EnsureFolder(name);
        Commit();
    }

    public void RenameFolder(string oldName, string newName)
    {
        _data.Folders.Remove(oldName);
        EnsureFolder(newName);
        foreach (var profile in _data.Profiles.Where(p => p.Folder == oldName))
            profile.Folder = newName;
        FolderMoved?.Invoke(oldName, newName);
        Commit();
    }

    public void DeleteFolder(string name)
    {
        _data.Folders.Remove(name);
        foreach (var profile in _data.Profiles.Where(p => p.Folder == name))
            profile.Folder = "";
        FolderMoved?.Invoke(name, "");
        Commit();
    }

    private void EnsureFolder(string name)
    {
        if (name.Length > 0 && !_data.Folders.Contains(name, StringComparer.OrdinalIgnoreCase))
            _data.Folders.Add(name);
    }

    public void Commit()
    {
        if (_writable)
        {
            try
            {
                JsonStore.Save(FilePath, _data);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                LogService.Error($"Profiles could not be saved: {ex.Message}");
                App.Instance.Tray?.Notify("WinBackup", "Profiles could not be saved. See the log for details.", true);
            }
        }
        Changed?.Invoke();
    }
}
