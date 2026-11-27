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

    private readonly bool _skipBuildOutput;
    private readonly bool _skipVisualStudioFiles;
    private readonly bool _skipGitData;
    private readonly Dictionary<string, bool> _projectFolders = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> ProjectRoots { get; set; } = Array.Empty<string>();

    public bool HasFolderIncludes => _includeFolders.Count > 0;

    public FilterSet(Profile profile)
    {
        _skipBuildOutput = profile.SkipBuildOutput;
        _skipVisualStudioFiles = profile.SkipVisualStudioFiles;
        _skipGitData = profile.SkipGitData;
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
        IsDevelopmentFolder(relativePath, sourceRoot) || MatchesExcludedFolder(relativePath, sourceRoot);

    public bool MatchesExcludedFolder(string relativePath, string sourceRoot) =>
        Matches(_excludeFolders, relativePath, sourceRoot);

    public bool IsDevelopmentFolder(string relativePath, string sourceRoot)
    {
        var name = Path.GetFileName(relativePath);
        bool Is(string other) => name.Equals(other, StringComparison.OrdinalIgnoreCase);

        if (_skipVisualStudioFiles && Is(".vs")) return true;
        if (_skipGitData && Is(".git")) return true;
        if (!_skipBuildOutput || !(Is("bin") || Is("obj"))) return false;

        var parent = Path.GetDirectoryName(relativePath) ?? "";
        var roots = ProjectRoots.Count > 0 ? ProjectRoots : new[] { sourceRoot };
        return roots.Any(root => HasProjectFile(Path.Combine(root, parent)));
    }

    private bool HasProjectFile(string folder)
    {
        if (_projectFolders.TryGetValue(folder, out var known)) return known;
        var found = false;
        try
        {
            found = Directory.Exists(folder) && Directory.EnumerateFiles(folder)
                .Any(file => Path.GetExtension(file).EndsWith("proj", StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            found = false;
        }
        return _projectFolders[folder] = found;
    }

    public bool IsDevelopmentFile(string relativePath) =>
        _skipVisualStudioFiles && (relativePath.EndsWith(".suo", StringComparison.OrdinalIgnoreCase)
                                   || relativePath.EndsWith("proj.user", StringComparison.OrdinalIgnoreCase));

    public bool IncludesFolder(string relativePath, string sourceRoot)
    {
        for (var current = relativePath; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
            if (Matches(_includeFolders, current, sourceRoot))
                return true;
        return false;
    }

    public bool IncludesFile(string relativePath, string sourceRoot) =>
        !IsDevelopmentFile(relativePath) && PassesPatterns(relativePath, sourceRoot);

    public bool PassesPatterns(string relativePath, string sourceRoot)
    {
        if (HasFolderIncludes && !IncludesFolder(Path.GetDirectoryName(relativePath) ?? "", sourceRoot)) return false;
        if (_includeFiles.Count > 0 && !Matches(_includeFiles, relativePath, sourceRoot)) return false;
        return !Matches(_excludeFiles, relativePath, sourceRoot);
    }
}
