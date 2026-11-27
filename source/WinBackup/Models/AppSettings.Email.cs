namespace WinBackup.Models;

public enum EmailWhen
{
    Never,
    Always,
    Problems,
    Changes,
    ProblemsOrChanges
}

public class EmailSettings
{
    public EmailWhen When { get; set; } = EmailWhen.Never;
    public string Server { get; set; } = "";
    public int Port { get; set; } = 587;
    public bool UseTls { get; set; } = true;
    public string User { get; set; } = "";
    public string ProtectedPassword { get; set; } = "";
    public string From { get; set; } = "";
    public string To { get; set; } = "";
}

public partial class AppSettings
{
    public EmailSettings Email { get; set; } = new();
}
