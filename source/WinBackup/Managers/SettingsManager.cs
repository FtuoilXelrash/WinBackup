using WinBackup.Models;

namespace WinBackup.Managers;

public class SettingsManager
{
    private static string FilePath => Path.Combine(AppPaths.Config, "settings.json");

    public AppSettings Current { get; private set; } = new();

    public void Load()
    {
        try
        {
            Current = JsonStore.Load<AppSettings>(FilePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Current = new AppSettings();
        }
    }

    public bool Save()
    {
        try
        {
            JsonStore.Save(FilePath, Current);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public void Apply(AppSettings settings)
    {
        settings.Positions = Current.Positions;
        Current = settings;
    }
}
