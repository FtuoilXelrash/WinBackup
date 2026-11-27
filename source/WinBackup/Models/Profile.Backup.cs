namespace WinBackup.Models;

public enum BackupStyle
{
    DatedFolder,
    Zip
}

public enum ZipCompression
{
    Optimal,
    Fastest,
    Stored
}

public partial class Profile
{
    public BackupStyle BackupStyle { get; set; } = BackupStyle.DatedFolder;
    public string BackupName { get; set; } = "{profile} {date} {time}";
    public int BackupGenerations { get; set; }
    public ZipCompression Compression { get; set; } = ZipCompression.Optimal;
}

public sealed partial class PairPlan
{
    public string BackupBase { get; init; } = "";
    public string BackupPath { get; init; } = "";
    public string ArchiveFolder { get; init; } = "";
}
