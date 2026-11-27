using System.Globalization;
using WinBackup.Managers;
using WinBackup.Models;

namespace WinBackup.Services;

public sealed record ReportFile(string Path, DateTime Started, string Profile, string Outcome)
{
    public string Display => $"{Started:yyyy-MM-dd HH:mm}   {Profile}   ({Outcome})";
}

public static class ReportStore
{
    private const string StampFormat = "yyyyMMdd-HHmmss";

    public static void Save(RunReport report)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var name = new string(report.ProfileName.Select(c => invalid.Contains(c) || c == '~' ? '-' : c).Take(60).ToArray());
        var file = $"{report.Started.ToString(StampFormat)}~{name}~{report.Outcome}~{report.Id[..8]}.json";
        try
        {
            JsonStore.Save(Path.Combine(AppPaths.Reports, file), report);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogService.Error($"The report could not be saved: {ex.Message}");
        }
    }

    public static List<ReportFile> List()
    {
        var result = new List<ReportFile>();
        if (!Directory.Exists(AppPaths.Reports)) return result;
        foreach (var path in Directory.GetFiles(AppPaths.Reports, "*.json"))
        {
            var parts = Path.GetFileNameWithoutExtension(path).Split('~');
            if (parts.Length == 4 && DateTime.TryParseExact(parts[0], StampFormat, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var started))
                result.Add(new ReportFile(path, started, parts[1], parts[2]));
        }
        return result.OrderByDescending(r => r.Started).ToList();
    }

    public static RunReport Load(string path) => JsonStore.Load<RunReport>(path);

    public static void Prune(int days)
    {
        if (days <= 0) return;
        var limit = DateTime.Now.AddDays(-days);
        foreach (var report in List().Where(r => r.Started < limit))
        {
            try { File.Delete(report.Path); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { LogService.Warning(ex.Message); }
        }
    }
}
