namespace WinBackup.Models;

public enum OverwriteRule
{
    Automatic,
    Newer,
    Different,
    Always,
    Never
}

public enum RemovedItems
{
    RecycleBin,
    VersionFolder,
    Permanent
}

public partial class Profile
{
    public OverwriteRule Overwrite { get; set; } = OverwriteRule.Automatic;
    public bool CreateEmptyFolders { get; set; } = true;
    public bool Verify { get; set; }
    public int Retries { get; set; } = 2;
    public int RetryDelaySeconds { get; set; } = 3;
    public RemovedItems Removed { get; set; } = RemovedItems.RecycleBin;
    public string VersionFolder { get; set; } = "";
    public int VersionGenerations { get; set; }
    public int MaxDeletePercent { get; set; } = 50;
    public int MaxDeleteMinimum { get; set; } = 10;
}
