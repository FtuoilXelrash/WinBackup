using System.Security;
using System.Text.RegularExpressions;
using WinBackup.Models;

namespace WinBackup.Engine;

public sealed class ScanResult
{
    public Dictionary<string, EntryInfo> Entries { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> Protected { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<Problem> Problems { get; } = new();
    public Dictionary<string, EntryInfo> Removable { get; } = new(StringComparer.OrdinalIgnoreCase);

    public int FileCount => Entries.Values.Count(e => !e.IsDirectory);
    public int SkippedFiles { get; set; }
    public int SkippedFolders { get; set; }

    public ScanTotals Totals => new(FileCount, Entries.Values.Count(e => e.IsDirectory),
        Entries.Values.Sum(e => e.Size), SkippedFiles, SkippedFolders);

    public bool IsProtected(string relativePath)
    {
        if (Protected.Contains("")) return true;
        for (var current = relativePath; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
            if (Protected.Contains(current))
                return true;
        return false;
    }
}

public static partial class Scanner
{
    private static readonly string[] SystemFolders = { "$RECYCLE.BIN", "System Volume Information" };

    private static readonly EnumerationOptions Options = new()
    {
        AttributesToSkip = 0,
        IgnoreInaccessible = false,
        RecurseSubdirectories = false
    };

    [GeneratedRegex(@"^~wb[0-9a-f]{32}\.part$", RegexOptions.IgnoreCase)]
    public static partial Regex PartialFile();

    private static bool IsLink(FileSystemInfo info) =>
        (info.Attributes & FileAttributes.ReparsePoint) != 0 && info.LinkTarget != null;

    public static ScanResult Scan(string root, string sourceRoot, FilterSet filter, RunControl control,
        Action<string>? status = null, bool collectRemovable = false)
    {
        var result = new ScanResult();
        var pending = new Stack<string>();
        pending.Push("");

        while (pending.Count > 0)
        {
            control.Checkpoint();
            var folder = pending.Pop();
            var fullFolder = folder.Length == 0 ? root : Path.Combine(root, folder);
            status?.Invoke(fullFolder);
            try
            {
                foreach (var info in new DirectoryInfo(fullFolder).EnumerateFileSystemInfos("*", Options))
                {
                    control.Checkpoint();
                    var relative = folder.Length == 0 ? info.Name : Path.Combine(folder, info.Name);
                    var isDirectory = (info.Attributes & FileAttributes.Directory) != 0;

                    if (folder.Length == 0 && isDirectory && SystemFolders.Contains(info.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        result.Protected.Add(relative);
                        continue;
                    }

                    if (isDirectory && filter.ExcludesFolder(relative, sourceRoot))
                    {
                        result.SkippedFolders++;
                        if (collectRemovable && !IsLink(info)
                            && filter.IsDevelopmentFolder(relative, sourceRoot)
                            && MayRemoveFolder(relative, sourceRoot, filter))
                            CollectRemovable(result, (DirectoryInfo)info, relative, sourceRoot, filter, control);
                        continue;
                    }

                    if (!isDirectory && PartialFile().IsMatch(info.Name))
                        continue;

                    if (IsLink(info))
                    {
                        result.Protected.Add(relative);
                        result.Problems.Add(new Problem(false, info.FullName, "Link or junction was not followed"));
                        continue;
                    }

                    if (isDirectory)
                    {
                        result.Entries[relative] = new EntryInfo(true, 0, info.LastWriteTimeUtc);
                        pending.Push(relative);
                    }
                    else if (filter.IncludesFile(relative, sourceRoot))
                    {
                        result.Entries[relative] = new EntryInfo(false, ((FileInfo)info).Length, info.LastWriteTimeUtc);
                    }
                    else
                    {
                        result.SkippedFiles++;
                        if (collectRemovable && filter.IsDevelopmentFile(relative) && filter.PassesPatterns(relative, sourceRoot))
                            result.Removable[relative] = new EntryInfo(false, ((FileInfo)info).Length, info.LastWriteTimeUtc);
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or ArgumentException)
            {
                result.Protected.Add(folder);
                result.Problems.Add(new Problem(true, fullFolder, "Folder could not be read: " + ex.Message));
            }
        }

        if (filter.HasFolderIncludes)
            RemoveUnwantedFolders(result, sourceRoot, filter);

        return result;
    }

    private static bool MayRemoveFolder(string relative, string sourceRoot, FilterSet filter) =>
        !filter.MatchesExcludedFolder(relative, sourceRoot)
        && (!filter.HasFolderIncludes || filter.IncludesFolder(relative, sourceRoot));

    private static void CollectRemovable(ScanResult result, DirectoryInfo top, string topRelative, string sourceRoot,
        FilterSet filter, RunControl control)
    {
        var pending = new Stack<(DirectoryInfo Folder, string Relative)>();
        pending.Push((top, topRelative));
        result.Removable[topRelative] = new EntryInfo(true, 0, top.LastWriteTimeUtc);

        while (pending.Count > 0)
        {
            control.Checkpoint();
            var (folder, folderRelative) = pending.Pop();
            try
            {
                foreach (var info in folder.EnumerateFileSystemInfos("*", Options))
                {
                    control.Checkpoint();
                    var relative = Path.Combine(folderRelative, info.Name);
                    if (IsLink(info))
                    {
                        result.Problems.Add(new Problem(false, info.FullName, "Link or junction was not followed and will be kept"));
                    }
                    else if (info is DirectoryInfo child)
                    {
                        if (filter.MatchesExcludedFolder(relative, sourceRoot)) continue;
                        result.Removable[relative] = new EntryInfo(true, 0, child.LastWriteTimeUtc);
                        pending.Push((child, relative));
                    }
                    else if (!PartialFile().IsMatch(info.Name) && filter.PassesPatterns(relative, sourceRoot))
                    {
                        result.Removable[relative] = new EntryInfo(false, ((FileInfo)info).Length, info.LastWriteTimeUtc);
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException or ArgumentException)
            {
                result.Problems.Add(new Problem(true, folder.FullName, "Folder could not be read: " + ex.Message));
            }
        }
    }

    private static void RemoveUnwantedFolders(ScanResult result, string sourceRoot, FilterSet filter)
    {
        var needed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (relative, info) in result.Entries)
        {
            if (info.IsDirectory && !filter.IncludesFolder(relative, sourceRoot)) continue;
            for (var current = Path.GetDirectoryName(relative); !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
                needed.Add(current);
        }

        var unwanted = result.Entries
            .Where(e => e.Value.IsDirectory && !needed.Contains(e.Key) && !filter.IncludesFolder(e.Key, sourceRoot))
            .Select(e => e.Key)
            .ToList();
        foreach (var key in unwanted)
            result.Entries.Remove(key);
    }
}
