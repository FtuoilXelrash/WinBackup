namespace WinBackup.Managers;

public static class AppPaths
{
    public static string Root { get; set; } = AppContext.BaseDirectory;
    public static string Config => Path.Combine(Root, "config");
    public static string State => Path.Combine(Config, "state");
    public static string Logs => Path.Combine(Root, "logs");
    public static string Reports => Path.Combine(Logs, "reports");
    public static string Docs => Path.Combine(Root, "docs");

    public static void Ensure()
    {
        Directory.CreateDirectory(Config);
        Directory.CreateDirectory(State);
        Directory.CreateDirectory(Reports);
    }
}
