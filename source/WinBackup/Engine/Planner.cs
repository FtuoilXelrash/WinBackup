using WinBackup.Models;

namespace WinBackup.Engine;

public static partial class Planner
{
    private static readonly TimeSpan Tolerance = TimeSpan.FromSeconds(2);

    public static JobPlan Build(Profile profile, RunControl control, Action<string>? status = null)
    {
        var plan = new JobPlan(profile);
        plan.Failures.AddRange(ProfileValidator.Validate(profile));
        if (plan.Failures.Count > 0) return plan;

        var filter = new FilterSet(profile);
        switch (profile.Mode)
        {
            case CopyMode.Mirror:
            case CopyMode.Update:
                foreach (var pair in ProfileValidator.Pairs(profile))
                    plan.Pairs.Add(BuildOneWay(profile, pair, filter, control, status));
                break;
            case CopyMode.Sync:
                foreach (var pair in ProfileValidator.Pairs(profile))
                    plan.Pairs.Add(BuildSync(profile, pair, filter, control, status));
                break;
            case CopyMode.Backup:
                BuildBackup(plan, filter, control, status);
                break;
        }
        return plan;
    }

    private static bool Available(PairPlan pair)
    {
        if (!Directory.Exists(pair.SourceRoot))
            pair.Failure = "The source folder is not available.";
        else if (!Directory.Exists(Path.GetPathRoot(pair.DestinationRoot)))
            pair.Failure = "The destination drive or share is not available.";
        else if (File.Exists(pair.DestinationRoot))
            pair.Failure = "The destination is a file, not a folder.";
        return pair.Failure == null;
    }

    private static bool Unreadable(PairPlan pair, ScanResult scan)
    {
        if (!scan.Protected.Contains("")) return false;
        pair.Failure = scan.Problems.FirstOrDefault(p => p.IsError)?.Message ?? "The folder could not be read.";
        return true;
    }

    public static bool SameTime(DateTime a, DateTime b) => (a - b).Duration() <= Tolerance;

    private static string? CopyReason(Profile profile, EntryInfo source, EntryInfo destination)
    {
        var newer = source.LastWriteUtc - destination.LastWriteUtc > Tolerance;
        var changed = source.Size != destination.Size || !SameTime(source.LastWriteUtc, destination.LastWriteUtc);
        var rule = profile.Overwrite == OverwriteRule.Automatic
            ? profile.Mode == CopyMode.Mirror ? OverwriteRule.Different : OverwriteRule.Newer
            : profile.Overwrite;

        return rule switch
        {
            OverwriteRule.Newer when newer => "Source is newer",
            OverwriteRule.Different when changed => "File is different",
            OverwriteRule.Always => "Always overwrite",
            _ => null
        };
    }

    private static PairPlan BuildOneWay(Profile profile, FolderPair folders, FilterSet filter,
        RunControl control, Action<string>? status)
    {
        var pair = new PairPlan { SourceRoot = folders.Source, DestinationRoot = folders.Destination };
        if (!Available(pair)) return pair;

        var source = Scanner.Scan(pair.SourceRoot, pair.SourceRoot, filter, control, status);
        if (Unreadable(pair, source)) return pair;

        var destination = Directory.Exists(pair.DestinationRoot)
            ? Scanner.Scan(pair.DestinationRoot, pair.SourceRoot, filter, control, status)
            : new ScanResult();
        if (Unreadable(pair, destination)) return pair;

        pair.Problems.AddRange(source.Problems);
        pair.Problems.AddRange(destination.Problems);
        pair.SourceFileCount = source.FileCount;

        foreach (var (relative, entry) in source.Entries)
        {
            control.Checkpoint();
            if (destination.IsProtected(relative)) continue;
            destination.Entries.TryGetValue(relative, out var existing);

            if (existing != null && existing.IsDirectory != entry.IsDirectory)
                Add(pair, relative, entry, existing, ItemAction.None, "A file and a folder have the same name");
            else if (entry.IsDirectory)
            {
                if (existing == null && profile.CreateEmptyFolders)
                    Add(pair, relative, entry, null, ItemAction.CopyToDestination, "New folder");
            }
            else if (existing == null)
                Add(pair, relative, entry, null, ItemAction.CopyToDestination, "New file");
            else if (CopyReason(profile, entry, existing) is { } reason)
                Add(pair, relative, entry, existing, ItemAction.CopyToDestination, reason);
        }

        if (profile.Mode == CopyMode.Mirror)
        {
            foreach (var (relative, entry) in destination.Entries)
                if (!source.Entries.ContainsKey(relative) && !source.IsProtected(relative))
                    Add(pair, relative, null, entry, ItemAction.DeleteDestination, "Not in source");

            Safeguard(profile, pair, source.FileCount, destination.FileCount, ItemAction.DeleteDestination);
        }

        Sort(pair);
        return pair;
    }

    private static void Add(PairPlan pair, string relative, EntryInfo? source, EntryInfo? destination,
        ItemAction action, string reason) =>
        pair.Items.Add(new PlanItem(pair, relative, source, destination, action, reason));

    private static void Sort(PairPlan pair) =>
        pair.Items.Sort((a, b) => string.Compare(a.RelativePath, b.RelativePath, StringComparison.OrdinalIgnoreCase));

    private static void Safeguard(Profile profile, PairPlan pair, int originalCount, int targetCount, ItemAction action)
    {
        var deletes = pair.Items.Count(i => i.Action == action && !i.IsDirectory);
        if (deletes == 0) return;

        if (originalCount == 0)
            pair.BlockReason = $"One side contains no files, and {deletes} file(s) would be deleted from the other.";
        else if (deletes > profile.MaxDeleteMinimum && deletes * 100L > (long)targetCount * profile.MaxDeletePercent)
            pair.BlockReason = $"{deletes} of {targetCount} files would be deleted, more than the limit of {profile.MaxDeletePercent}%.";
    }
}
