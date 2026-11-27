using WinBackup.Models;
using WinBackup.Services;

namespace WinBackup;

public partial class App
{
    partial void InitEmail() =>
        Jobs.Completed += report => Dispatcher.BeginInvoke(() => EmailReport(report));

    private async void EmailReport(RunReport report)
    {
        var settings = Settings.Current.Email;
        if (!EmailService.Wanted(settings, report)) return;
        try
        {
            await EmailService.SendReport(settings, report);
            LogService.Info($"{report.ProfileName}: report emailed");
        }
        catch (Exception ex)
        {
            LogService.Error($"{report.ProfileName}: the report email failed: {ex.Message}");
            Tray?.Notify("WinBackup", "A report could not be emailed. See the log for details.", true);
        }
    }
}
