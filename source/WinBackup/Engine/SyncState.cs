using System.Security.Cryptography;
using System.Text;
using WinBackup.Managers;
using WinBackup.Models;

namespace WinBackup.Engine;

public sealed record SyncEntry(bool IsDirectory, long SourceSize, DateTime SourceTime,
    long DestinationSize, DateTime DestinationTime);

public static class SyncState
{
    private static string FileFor(Profile profile, PairPlan pair)
    {
        var key = $"{pair.SourceRoot}|{pair.DestinationRoot}".ToUpperInvariant();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..20];
        return Path.Combine(AppPaths.State, $"{profile.Id}-{hash}.json");
    }

    public static Dictionary<string, SyncEntry>? Load(Profile profile, PairPlan pair)
    {
        var path = FileFor(profile, pair);
        if (!File.Exists(path)) return null;
        var loaded = JsonStore.Load<Dictionary<string, SyncEntry>>(path);
        return new Dictionary<string, SyncEntry>(loaded, StringComparer.OrdinalIgnoreCase);
    }

    public static void Update(Profile profile, PairPlan pair)
    {
        if (!Directory.Exists(pair.SourceRoot) || !Directory.Exists(pair.DestinationRoot)) return;

        var filter = new FilterSet(profile) { ProjectRoots = new[] { pair.SourceRoot, pair.DestinationRoot } };
        using var control = new RunControl();
        var source = Scanner.Scan(pair.SourceRoot, pair.SourceRoot, filter, control);
        var destination = Scanner.Scan(pair.DestinationRoot, pair.SourceRoot, filter, control);
        var previous = Load(profile, pair) ?? new Dictionary<string, SyncEntry>(StringComparer.OrdinalIgnoreCase);
        var next = new Dictionary<string, SyncEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (var (relative, entry) in previous)
            if (source.IsProtected(relative) || destination.IsProtected(relative)
                || source.Entries.ContainsKey(relative) || destination.Entries.ContainsKey(relative))
                next[relative] = entry;

        foreach (var (relative, a) in source.Entries)
        {
            if (!destination.Entries.TryGetValue(relative, out var b) || a.IsDirectory != b.IsDirectory) continue;
            if (a.IsDirectory || (a.Size == b.Size && Planner.SameTime(a.LastWriteUtc, b.LastWriteUtc)))
                next[relative] = new SyncEntry(a.IsDirectory, a.Size, a.LastWriteUtc, b.Size, b.LastWriteUtc);
        }

        JsonStore.Save(FileFor(profile, pair), next);
    }
}
