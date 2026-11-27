using WinBackup.Managers;

namespace WinBackup;

public partial class App
{
    public ProfileManager Profiles { get; } = new();

    partial void InitProfiles() => Profiles.Load();
}
