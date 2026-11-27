namespace WinBackup.Models;

public partial class Profile
{
    public string IncludeFiles { get; set; } = "";
    public string ExcludeFiles { get; set; } = "";
    public string IncludeFolders { get; set; } = "";
    public string ExcludeFolders { get; set; } = "";
}
