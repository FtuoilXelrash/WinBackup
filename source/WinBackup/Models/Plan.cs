using System.ComponentModel;

namespace WinBackup.Models;

public enum ItemAction
{
    None,
    CopyToDestination,
    CopyToSource,
    DeleteDestination,
    DeleteSource
}

public sealed record EntryInfo(bool IsDirectory, long Size, DateTime LastWriteUtc);

public sealed class PlanItem : INotifyPropertyChanged
{
    private ItemAction _action;

    public PlanItem(PairPlan pair, string relativePath, EntryInfo? source, EntryInfo? destination,
        ItemAction action, string reason)
    {
        Pair = pair;
        RelativePath = relativePath;
        Source = source;
        Destination = destination;
        PlannedAction = _action = action;
        Reason = reason;
    }

    public PairPlan Pair { get; }
    public string RelativePath { get; }
    public EntryInfo? Source { get; }
    public EntryInfo? Destination { get; }
    public bool IsSkippedRemoval { get; init; }
    public ItemAction PlannedAction { get; }
    public string Reason { get; }
    public bool IsDirectory => (Source ?? Destination)!.IsDirectory;

    public ItemAction Action
    {
        get => _action;
        set
        {
            if (_action == value) return;
            _action = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Action)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ActionText)));
        }
    }

    public string ActionText => Action switch
    {
        ItemAction.CopyToDestination => "Copy →",
        ItemAction.CopyToSource => "← Copy",
        ItemAction.DeleteDestination => "Delete in destination",
        ItemAction.DeleteSource => "Delete in source",
        _ => "Skip"
    };

    public string SourceText => Format(Source);
    public string DestinationText => Format(Destination);

    private static string Format(EntryInfo? info) => info switch
    {
        null => "",
        { IsDirectory: true } => "Folder",
        _ => $"{info.Size:N0} B   {info.LastWriteUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}"
    };

    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed record Problem(bool IsError, string Path, string Message);

public sealed record ScanTotals(int Files, int Folders, long Bytes, int SkippedFiles, int SkippedFolders);

public sealed partial class PairPlan
{
    public string SourceRoot { get; init; } = "";
    public string DestinationRoot { get; init; } = "";
    public List<PlanItem> Items { get; } = new();
    public List<Problem> Problems { get; } = new();
    public string? Failure { get; set; }
    public string? BlockReason { get; set; }
    public int SourceFileCount { get; set; }
    public ScanTotals? SourceTotals { get; set; }
    public ScanTotals? DestinationTotals { get; set; }
}

public sealed class JobPlan
{
    public JobPlan(Profile profile) => Profile = profile;

    public Profile Profile { get; }
    public List<PairPlan> Pairs { get; } = new();
    public List<string> Failures { get; } = new();
    public DateTime Created { get; } = DateTime.Now;
    public TimeSpan Elapsed { get; set; }
}
