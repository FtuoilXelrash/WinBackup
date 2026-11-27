using WinBackup.Managers;

namespace WinBackup.Engine;

public sealed class BackupRecord
{
    public string ProfileId { get; set; } = "";
    public string Folder { get; set; } = "";
    public string Path { get; set; } = "";
    public DateTime Created { get; set; }
    public bool Complete { get; set; }
}

public static class BackupHistory
{
    private static string FilePath => System.IO.Path.Combine(AppPaths.Config, "backups.json");

    public static List<BackupRecord> Load() => JsonStore.Load<List<BackupRecord>>(FilePath);

    public static void Save(List<BackupRecord> records) => JsonStore.Save(FilePath, records);

    public static void Add(BackupRecord record)
    {
        var records = Load();
        records.Add(record);
        Save(records);
    }
}
