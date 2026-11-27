using WinBackup.Models;

namespace WinBackup.Engine;

public static partial class Planner
{
    private sealed record Candidate(string Path, EntryInfo? Source, EntryInfo? Destination, ItemAction Action, string Reason);

    private static PairPlan BuildSync(Profile profile, FolderPair folders, FilterSet filter,
        RunControl control, Action<string>? status)
    {
        var pair = new PairPlan { SourceRoot = folders.Source, DestinationRoot = folders.Destination };
        if (!Available(pair)) return pair;

        var history = SyncState.Load(profile, pair);
        if (history is { Count: > 0 } && !Directory.Exists(pair.DestinationRoot))
        {
            pair.Failure = "The destination folder has disappeared since the last sync.";
            return pair;
        }

        filter.ProjectRoots = new[] { pair.SourceRoot, pair.DestinationRoot };
        var source = Scanner.Scan(pair.SourceRoot, pair.SourceRoot, filter, control, status);
        if (Unreadable(pair, source)) return pair;
        var destination = Directory.Exists(pair.DestinationRoot)
            ? Scanner.Scan(pair.DestinationRoot, pair.SourceRoot, filter, control, status)
            : new ScanResult();
        if (Unreadable(pair, destination)) return pair;

        pair.Problems.AddRange(source.Problems);
        pair.Problems.AddRange(destination.Problems);
        pair.SourceFileCount = source.FileCount;
        pair.SourceTotals = source.Totals;
        pair.DestinationTotals = destination.Totals;

        var candidates = new List<Candidate>();
        foreach (var relative in source.Entries.Keys.Union(destination.Entries.Keys, StringComparer.OrdinalIgnoreCase))
        {
            control.Checkpoint();
            if (source.IsProtected(relative) || destination.IsProtected(relative)) continue;
            source.Entries.TryGetValue(relative, out var a);
            destination.Entries.TryGetValue(relative, out var b);
            SyncEntry? last = null;
            history?.TryGetValue(relative, out last);

            if (Compare(profile, relative, a, b, last) is { } candidate)
                candidates.Add(candidate);
        }

        foreach (var c in candidates)
        {
            var action = c.Action;
            var reason = c.Reason;
            var entry = (c.Source ?? c.Destination)!;
            if (entry.IsDirectory && action is ItemAction.DeleteSource or ItemAction.DeleteDestination)
            {
                var restoring = action == ItemAction.DeleteSource ? ItemAction.CopyToDestination : ItemAction.CopyToSource;
                var prefix = c.Path + "\\";
                if (candidates.Any(x => x.Action == restoring && x.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                {
                    action = restoring;
                    reason = "Folder contains new or changed items";
                }
            }
            Add(pair, c.Path, c.Source, c.Destination, action, reason);
        }

        Safeguard(profile, pair, source.FileCount, destination.FileCount, ItemAction.DeleteDestination);
        Safeguard(profile, pair, destination.FileCount, source.FileCount, ItemAction.DeleteSource);
        Sort(pair);
        return pair;
    }

    private static Candidate? Compare(Profile profile, string path, EntryInfo? a, EntryInfo? b, SyncEntry? last)
    {
        static bool Changed(EntryInfo now, long size, DateTime time) =>
            now.Size != size || !SameTime(now.LastWriteUtc, time);

        Candidate Result(ItemAction action, string reason) => new(path, a, b, action, reason);

        if (a != null && b != null)
        {
            if (a.IsDirectory != b.IsDirectory) return Result(ItemAction.None, "A file and a folder have the same name");
            if (a.IsDirectory || (a.Size == b.Size && SameTime(a.LastWriteUtc, b.LastWriteUtc))) return null;

            if (last != null)
            {
                var sourceChanged = Changed(a, last.SourceSize, last.SourceTime);
                var destinationChanged = Changed(b, last.DestinationSize, last.DestinationTime);
                if (sourceChanged && !destinationChanged) return Result(ItemAction.CopyToDestination, "Changed in source");
                if (destinationChanged && !sourceChanged) return Result(ItemAction.CopyToSource, "Changed in destination");
            }

            return profile.Conflicts switch
            {
                ConflictRule.Source => Result(ItemAction.CopyToDestination, "Conflict: source wins"),
                ConflictRule.Destination => Result(ItemAction.CopyToSource, "Conflict: destination wins"),
                ConflictRule.Newer when !SameTime(a.LastWriteUtc, b.LastWriteUtc) => Result(
                    a.LastWriteUtc > b.LastWriteUtc ? ItemAction.CopyToDestination : ItemAction.CopyToSource,
                    "Conflict: newer wins"),
                _ => Result(ItemAction.None, "Conflict: the files differ")
            };
        }

        var onlyInSource = a != null;
        var present = (a ?? b)!;
        var copy = onlyInSource ? ItemAction.CopyToDestination : ItemAction.CopyToSource;
        var side = onlyInSource ? "source" : "destination";

        if (present.IsDirectory && !profile.CreateEmptyFolders && last == null) return null;
        if (last == null || last.IsDirectory != present.IsDirectory) return Result(copy, $"New in {side}");

        var changed = !present.IsDirectory && (onlyInSource
            ? Changed(present, last.SourceSize, last.SourceTime)
            : Changed(present, last.DestinationSize, last.DestinationTime));
        if (changed) return Result(copy, $"Changed in {side} and deleted on the other side");

        return Result(onlyInSource ? ItemAction.DeleteSource : ItemAction.DeleteDestination,
            onlyInSource ? "Deleted in destination" : "Deleted in source");
    }
}
