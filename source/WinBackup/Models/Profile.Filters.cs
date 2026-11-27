namespace WinBackup.Models;

public partial class Profile
{
    public string IncludeFiles { get; set; } = "";
    public string ExcludeFiles { get; set; } = "";
    public string IncludeFolders { get; set; } = "";
    public string ExcludeFolders { get; set; } = "";
    public bool SkipBuildOutput { get; set; }
    public bool SkipVisualStudioFiles { get; set; }
    public bool SkipGitData { get; set; }
    public bool RemoveSkippedFromDestination { get; set; }
}
