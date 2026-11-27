using System.Text;
using WinBackup.Managers;

namespace WinBackup.Services;

public static class LogService
{
    private static readonly object Sync = new();

    public static string PathFor(DateTime date) =>
        Path.Combine(AppPaths.Logs, $"winbackup-{date:yyyy-MM-dd}.log");

    public static void Info(string message) => Write("INFO ", message);
    public static void Warning(string message) => Write("WARN ", message);
    public static void Error(string message) => Write("ERROR", message);

    private static void Write(string level, string message)
    {
        var line = $"{DateTime.Now:HH:mm:ss}  {level}  {message}{Environment.NewLine}";
        lock (Sync)
        {
            try
            {
                Directory.CreateDirectory(AppPaths.Logs);
                File.AppendAllText(PathFor(DateTime.Now), line, Encoding.UTF8);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                System.Diagnostics.Trace.WriteLine(ex.Message);
            }
        }
    }

    public static string Read(string path)
    {
        if (!File.Exists(path)) return "";
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    public static IEnumerable<string> Files() =>
        Directory.Exists(AppPaths.Logs)
            ? Directory.GetFiles(AppPaths.Logs, "winbackup-*.log").OrderByDescending(f => f)
            : Enumerable.Empty<string>();

    public static void Prune(int days)
    {
        if (days <= 0) return;
        var limit = DateTime.Now.AddDays(-days);
        foreach (var file in Files().Where(f => File.GetLastWriteTime(f) < limit).ToList())
        {
            try { File.Delete(file); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { Warning(ex.Message); }
        }
    }
}
