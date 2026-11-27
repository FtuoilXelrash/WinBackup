using WinBackup.Models;

namespace WinBackup.Engine;

public sealed record FolderPair(string Source, string Destination);

public static partial class ProfileValidator
{
    public static string Normalize(string path)
    {
        var expanded = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
        if (!Path.IsPathFullyQualified(expanded)) return "";
        try
        {
            return Path.TrimEndingDirectorySeparator(Path.GetFullPath(expanded));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or IOException)
        {
            return "";
        }
    }

    public static bool IsSameOrInside(string path, string parent)
    {
        var child = Path.TrimEndingDirectorySeparator(path) + "\\";
        var root = Path.TrimEndingDirectorySeparator(parent).TrimEnd('\\') + "\\";
        return child.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    private static bool Overlap(string a, string b) => IsSameOrInside(a, b) || IsSameOrInside(b, a);

    public static List<FolderEntry> Used(IEnumerable<FolderEntry> entries) =>
        entries.Where(e => e.Enabled && !string.IsNullOrWhiteSpace(e.Path)).ToList();

    public static bool UsesSubfolders(Profile profile)
    {
        var result = profile.SubfolderPerSource;
        RequireSubfolders(profile, ref result);
        return result;
    }

    static partial void RequireSubfolders(Profile profile, ref bool required);

    public static string SubfolderName(FolderEntry entry)
    {
        var name = (entry.Subfolder ?? "").Trim();
        if (name.Length > 0) return name;
        var path = Normalize(entry.Path);
        name = Path.GetFileName(path);
        return name.Length > 0 ? name : path.Replace(":", "").Trim('\\').Replace('\\', '_');
    }

    public static List<FolderPair> Pairs(Profile profile)
    {
        var subfolders = UsesSubfolders(profile);
        return (from source in Used(profile.Sources)
                from destination in Used(profile.Destinations)
                let target = Normalize(destination.Path)
                select new FolderPair(Normalize(source.Path),
                    subfolders ? Path.Combine(target, SubfolderName(source)) : target)).ToList();
    }

    public static List<string> Validate(Profile profile)
    {
        var errors = new List<string>();
        var sources = Used(profile.Sources);
        var destinations = Used(profile.Destinations);

        if (sources.Count == 0) errors.Add("Add at least one source folder.");
        if (destinations.Count == 0) errors.Add("Add at least one destination folder.");

        foreach (var entry in sources.Concat(destinations).Where(e => Normalize(e.Path).Length == 0))
            errors.Add($"Not a complete folder path: {entry.Path}");
        if (errors.Count > 0) return errors;

        var sourcePaths = sources.Select(e => Normalize(e.Path)).ToList();
        var destinationPaths = destinations.Select(e => Normalize(e.Path)).ToList();

        foreach (var source in sourcePaths)
            foreach (var destination in destinationPaths.Where(d => Overlap(source, d)))
                errors.Add($"A source and destination cannot be the same or contain each other: {source} and {destination}");

        for (var i = 0; i < destinationPaths.Count; i++)
            for (var j = i + 1; j < destinationPaths.Count; j++)
                if (Overlap(destinationPaths[i], destinationPaths[j]))
                    errors.Add($"Destinations cannot be the same or contain each other: {destinationPaths[i]} and {destinationPaths[j]}");

        if (UsesSubfolders(profile))
        {
            var names = sources.Select(SubfolderName).ToList();
            if (names.Any(n => n.Length == 0 || n is "." or ".." || n.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
                errors.Add("Every source needs a valid subfolder name.");
            if (names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Count)
                errors.Add("Two sources use the same subfolder name. Enter different subfolder names.");
        }
        else if (profile.Mode != CopyMode.Update && sources.Count > 1)
        {
            errors.Add($"{profile.Mode} with several sources requires \"Create a subfolder for each source\".");
        }

        if (profile.Removed == RemovedItems.VersionFolder)
        {
            var versions = Normalize(profile.VersionFolder);
            if (versions.Length == 0)
                errors.Add("Enter a complete path for the version folder.");
            else if (sourcePaths.Concat(destinationPaths).Any(p => Overlap(versions, p)))
                errors.Add("The version folder cannot overlap a source or destination folder.");
        }

        ValidateMore(profile, sourcePaths, destinationPaths, errors);
        return errors;
    }

    static partial void ValidateMore(Profile profile, List<string> sources, List<string> destinations, List<string> errors);
}
