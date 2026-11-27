using System.Text.RegularExpressions;
using WinBackup.Models;

namespace WinBackup.Engine;

public sealed class FilterSet
{
    private sealed record Rule(Regex Pattern, bool UsesPath);

    private readonly List<Rule> _includeFiles;
    private readonly List<Rule> _excludeFiles;
    private readonly List<Rule> _includeFolders;
    private readonly List<Rule> _excludeFolders;

    public bool HasFolderIncludes => _includeFolders.Count > 0;

    public FilterSet(Profile profile)
    {
        _includeFiles = Parse(profile.IncludeFiles);
        _excludeFiles = Parse(profile.ExcludeFiles);
        _includeFolders = Parse(profile.IncludeFolders);
        _excludeFolders = Parse(profile.ExcludeFolders);
    }

    private static List<Rule> Parse(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => Environment.ExpandEnvironmentVariables(line).Replace('/', '\\').TrimEnd('\\'))
            .Where(line => line.Length > 0)
            .Select(line => new Rule(
                new Regex("^" + Regex.Escape(line).Replace("\\*", ".*").Replace("\\?", ".") + "$",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking),
                line.Contains('\\')))
            .ToList();

    private static bool Matches(List<Rule> rules, string relativePath, string sourceRoot)
    {
        if (rules.Count == 0) return false;
        var name = Path.GetFileName(relativePath);
        var fullPath = Path.Combine(sourceRoot, relativePath);
        return rules.Any(rule => rule.UsesPath
            ? rule.Pattern.IsMatch(relativePath) || rule.Pattern.IsMatch(fullPath)
            : rule.Pattern.IsMatch(name));
    }

    public bool ExcludesFolder(string relativePath, string sourceRoot) =>
        Matches(_excludeFolders, relativePath, sourceRoot);

    public bool IncludesFolder(string relativePath, string sourceRoot)
    {
        for (var current = relativePath; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
            if (Matches(_includeFolders, current, sourceRoot))
                return true;
        return false;
    }

    public bool IncludesFile(string relativePath, string sourceRoot)
    {
        if (HasFolderIncludes && !IncludesFolder(Path.GetDirectoryName(relativePath) ?? "", sourceRoot)) return false;
        if (_includeFiles.Count > 0 && !Matches(_includeFiles, relativePath, sourceRoot)) return false;
        return !Matches(_excludeFiles, relativePath, sourceRoot);
    }
}
