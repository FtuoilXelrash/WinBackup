using WinBackup.Models;

namespace WinBackup.Engine;

public static partial class Planner
{
    public static string BackupTitle(Profile profile, DateTime time)
    {
        var name = profile.BackupName
            .Replace("{profile}", profile.Name, StringComparison.OrdinalIgnoreCase)
            .Replace("{date}", time.ToString("yyyy-MM-dd"), StringComparison.OrdinalIgnoreCase)
            .Replace("{time}", time.ToString("HHmmss"), StringComparison.OrdinalIgnoreCase);
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim(' ', '.');
    }

    private static void BuildBackup(JobPlan plan, FilterSet filter, RunControl control, Action<string>? status)
    {
        var profile = plan.Profile;
        var subfolders = ProfileValidator.UsesSubfolders(profile);
        var extension = profile.BackupStyle == BackupStyle.Zip ? ".zip" : "";
        var title = BackupTitle(profile, plan.Created);

        foreach (var destination in ProfileValidator.Used(profile.Destinations))
        {
            var folder = ProfileValidator.Normalize(destination.Path);
            var path = Path.Combine(folder, title + extension);
            for (var i = 2; Path.Exists(path); i++)
                path = Path.Combine(folder, $"{title} ({i}){extension}");

            foreach (var source in ProfileValidator.Used(profile.Sources))
            {
                var sub = subfolders ? ProfileValidator.SubfolderName(source) : "";
                var sourceRoot = ProfileValidator.Normalize(source.Path);
                var pair = new PairPlan
                {
                    SourceRoot = sourceRoot,
                    DestinationRoot = extension.Length > 0 || sub.Length == 0 ? path : Path.Combine(path, sub),
                    BackupBase = folder,
                    BackupPath = path,
                    ArchiveFolder = extension.Length > 0 ? sub : ""
                };
                plan.Pairs.Add(pair);

                if (!Directory.Exists(sourceRoot))
                    pair.Failure = "The source folder is not available.";
                else if (!Directory.Exists(Path.GetPathRoot(folder)))
                    pair.Failure = "The destination drive or share is not available.";
                if (pair.Failure != null) continue;

                var scan = Scanner.Scan(sourceRoot, sourceRoot, filter, control, status);
                if (Unreadable(pair, scan)) continue;
                pair.Problems.AddRange(scan.Problems);
                pair.SourceFileCount = scan.FileCount;
                pair.SourceTotals = scan.Totals;

                foreach (var (relative, entry) in scan.Entries)
                    if (!entry.IsDirectory || profile.CreateEmptyFolders)
                        Add(pair, relative, entry, null, ItemAction.CopyToDestination, "Backup");
                Sort(pair);
            }
        }
    }
}
