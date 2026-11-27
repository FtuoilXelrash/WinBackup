using System.Windows;
using System.Windows.Controls;
using WinBackup.Managers;
using WinBackup.Models;
using WinBackup.Services;

namespace WinBackup.Windows;

public partial class SettingsWindow
{
    partial void LoadExtras()
    {
        PasswordBox.Clear();
        EmailStatus.Text = "";
        PasswordHint.Text = ((AppSettings)DataContext).Email.ProtectedPassword.Length > 0
            ? "A password is saved. Leave empty to keep it."
            : "";
    }

    partial void StoreExtras()
    {
        var email = ((AppSettings)DataContext).Email;
        if (PasswordBox.Password.Length > 0)
            email.ProtectedPassword = EmailService.Protect(PasswordBox.Password);
        else if (email.User.Length == 0)
            email.ProtectedPassword = "";
    }

    private async void TestEmail_Click(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        button.Focus();
        var settings = JsonStore.Clone(((AppSettings)DataContext).Email);
        button.IsEnabled = false;
        EmailStatus.Text = "Sending...";
        try
        {
            if (PasswordBox.Password.Length > 0)
                settings.ProtectedPassword = EmailService.Protect(PasswordBox.Password);
            await EmailService.Send(settings, "WinBackup test message", "Email notification is working.");
            EmailStatus.Text = "The test message was sent.";
        }
        catch (Exception ex)
        {
            EmailStatus.Text = ex.InnerException == null ? ex.Message : $"{ex.Message} {ex.InnerException.Message}";
        }
        finally
        {
            button.IsEnabled = true;
        }
    }
}
