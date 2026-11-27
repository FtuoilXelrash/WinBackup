using System.Windows;
using WinBackup.Models;

namespace WinBackup.Managers;

public static class WindowPlacement
{
    public static readonly DependencyProperty KeyProperty = DependencyProperty.RegisterAttached(
        "Key", typeof(string), typeof(WindowPlacement), new PropertyMetadata(null));

    public static void Track(Window window, string key)
    {
        window.SetValue(KeyProperty, key);
        Restore(window, key);
        ApplyOptions(window);
        window.Closing += (_, _) => Save(window);
    }

    public static void ApplyOptions(Window window)
    {
        if (window.GetValue(KeyProperty) is not string key) return;
        var set = App.Instance.Settings.Current.Windows;
        if (typeof(WindowOptionSet).GetProperty(key)?.GetValue(set) is not WindowOption option) return;
        window.Topmost = option.AlwaysOnTop;
        window.ShowInTaskbar = option.ShowInTaskbar;
    }

    public static void Save(Window window)
    {
        if (window.GetValue(KeyProperty) is not string key || !window.IsLoaded) return;
        var bounds = window.WindowState == WindowState.Normal
            ? new Rect(window.Left, window.Top, window.ActualWidth, window.ActualHeight)
            : window.RestoreBounds;
        if (bounds.IsEmpty || double.IsNaN(bounds.Left)) return;
        App.Instance.Settings.Current.Positions[key] = new WindowPosition
        {
            Left = bounds.Left,
            Top = bounds.Top,
            Width = bounds.Width,
            Height = bounds.Height,
            Maximized = window.WindowState == WindowState.Maximized
        };
        App.Instance.Settings.Save();
    }

    private static void Restore(Window window, string key)
    {
        if (!App.Instance.Settings.Current.Positions.TryGetValue(key, out var saved)) return;

        window.WindowStartupLocation = WindowStartupLocation.Manual;
        var resizable = window.ResizeMode is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip;
        if (resizable && saved.Width > 100) window.Width = saved.Width;
        if (resizable && saved.Height > 100) window.Height = saved.Height;

        var left = SystemParameters.VirtualScreenLeft;
        var top = SystemParameters.VirtualScreenTop;
        var right = left + SystemParameters.VirtualScreenWidth;
        var bottom = top + SystemParameters.VirtualScreenHeight;
        var visible = saved.Left + saved.Width > left + 50 && saved.Left < right - 50
                      && saved.Top > top - 20 && saved.Top < bottom - 50;

        window.Left = visible ? saved.Left : left + 50;
        window.Top = visible ? saved.Top : top + 50;
        if (saved.Maximized && resizable) window.WindowState = WindowState.Maximized;
    }
}
