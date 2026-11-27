using System.Reflection;
using System.Windows;

namespace WinBackup.Windows;

public partial class AboutWindow : Window
{
    public static string VersionText =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "";

    public AboutWindow()
    {
        InitializeComponent();
        VersionLabel.Text = VersionText;
    }
}
