using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using WinBackup.Models;

namespace WinBackup.Services;

public static class EmailService
{
    private const int ListedEntries = 200;

    public static string Protect(string password) =>
        password.Length == 0
            ? ""
            : Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(password), null, DataProtectionScope.CurrentUser));

    private static string Unprotect(string value)
    {
        if (value.Length == 0) return "";
        try
        {
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(value), null, DataProtectionScope.CurrentUser));
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            throw new InvalidOperationException("The saved email password cannot be read by this user or computer. Enter it again in Settings.", ex);
        }
    }

    public static bool Wanted(EmailSettings settings, RunReport report)
    {
        var problems = report.Outcome != RunOutcome.Success || report.Warnings > 0;
        var changes = report.Copied + report.Deleted > 0;
        return settings.When switch
        {
            EmailWhen.Always => true,
            EmailWhen.Problems => problems,
            EmailWhen.Changes => changes,
            EmailWhen.ProblemsOrChanges => problems || changes,
            _ => false
        };
    }

    public static Task SendReport(EmailSettings settings, RunReport report)
    {
        var body = new StringBuilder()
            .AppendLine($"Profile: {report.ProfileName}")
            .AppendLine($"Computer: {Environment.MachineName}")
            .AppendLine($"Started: {report.Started:yyyy-MM-dd HH:mm:ss}")
            .AppendLine($"Finished: {report.Finished:yyyy-MM-dd HH:mm:ss}")
            .AppendLine(report.Summary);

        var problems = report.Entries.Where(e => e.Level != EntryLevel.Info).ToList();
        if (problems.Count > 0)
        {
            body.AppendLine().AppendLine("Errors and warnings:");
            foreach (var entry in problems.OrderByDescending(e => e.Level).Take(ListedEntries))
                body.AppendLine($"{entry.Level}  {entry.Action}  {entry.Path}  {entry.Message}");
            if (problems.Count > ListedEntries)
                body.AppendLine($"{problems.Count - ListedEntries} more are listed in the report in WinBackup.");
        }

        var state = report.Outcome == RunOutcome.Success ? "completed" : report.Outcome == RunOutcome.Cancelled ? "cancelled" : "had errors";
        return Send(settings, $"WinBackup: {report.ProfileName} {state}", body.ToString());
    }

    public static async Task Send(EmailSettings settings, string subject, string body)
    {
        if (settings.Server.Length == 0 || settings.From.Length == 0 || settings.To.Length == 0)
            throw new InvalidOperationException("Enter the mail server, sender, and recipient.");

        using var message = new MailMessage
        {
            From = new MailAddress(settings.From.Trim()),
            Subject = subject.ReplaceLineEndings(" "),
            Body = body
        };
        foreach (var address in settings.To.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            message.To.Add(address);

        using var client = new SmtpClient(settings.Server.Trim(), settings.Port) { EnableSsl = settings.UseTls };
        if (settings.User.Length > 0)
            client.Credentials = new NetworkCredential(settings.User, Unprotect(settings.ProtectedPassword));

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await client.SendMailAsync(message, timeout.Token);
    }
}
