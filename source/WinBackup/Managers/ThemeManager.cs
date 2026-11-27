using System.Windows;
using WinBackup.Models;

namespace WinBackup.Managers;

public static class ThemeManager
{
    private static AppTheme _current = AppTheme.Light;

    public static void Apply(AppTheme theme)
    {
        if (theme == _current) return;
        _current = theme;
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        dictionaries[0] = new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/WinBackup;component/Themes/{theme}.xaml")
        };
    }
}
