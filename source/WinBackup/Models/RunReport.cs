namespace WinBackup.Models;

public enum EntryLevel
{
    Info,
    Warning,
    Error
}

public enum RunOutcome
{
    Success,
    Errors,
    Cancelled
}

public sealed record ReportEntry(DateTime Time, EntryLevel Level, string Action, string Path, string Message);

public sealed class RunReport
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ProfileId { get; set; } = "";
    public string ProfileName { get; set; } = "";
    public string Trigger { get; set; } = "";
    public DateTime Started { get; set; } = DateTime.Now;
    public DateTime Finished { get; set; }
    public RunOutcome Outcome { get; set; }
    public int Copied { get; set; }
    public int Deleted { get; set; }
    public int Skipped { get; set; }
    public int Errors { get; set; }
    public int Warnings { get; set; }
    public long Bytes { get; set; }
    public List<ReportEntry> Entries { get; set; } = new();

    public void Add(EntryLevel level, string action, string path, string message = "")
    {
        if (level == EntryLevel.Error) Errors++;
        if (level == EntryLevel.Warning) Warnings++;
        Entries.Add(new ReportEntry(DateTime.Now, level, action, path, message));
    }

    public string Summary =>
        $"{(Outcome == RunOutcome.Success ? "Completed" : Outcome == RunOutcome.Cancelled ? "Cancelled" : "Completed with errors")}: " +
        $"{Copied} copied, {Deleted} deleted, {Skipped} skipped, {Warnings} warning(s), {Errors} error(s)";
}
