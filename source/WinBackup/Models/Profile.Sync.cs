namespace WinBackup.Models;

public enum ConflictRule
{
    Skip,
    Newer,
    Source,
    Destination
}

public partial class Profile
{
    public ConflictRule Conflicts { get; set; } = ConflictRule.Skip;
}
