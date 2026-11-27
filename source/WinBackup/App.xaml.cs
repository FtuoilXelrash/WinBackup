using System.Windows;
using WinBackup.Managers;
using WinBackup.Windows;

namespace WinBackup;

public partial class App : Application
{
    public static App Instance => (App)Current;

    public SettingsManager Settings { get; private set; } = null!;
    public bool IsExiting { get; private set; }

    private MainWindow? _mainWindow;
    private readonly Dictionary<Type, Window> _toolWindows = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var proceed = true;
        CheckSingleInstance(ref proceed);
        if (!proceed)
        {
            Shutdown();
            return;
        }

        try
        {
            AppPaths.Ensure();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"WinBackup cannot create its folders in {AppPaths.Root}\n\n{ex.Message}",
                "WinBackup", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        Settings = new SettingsManager();
        Settings.Load();
        ThemeManager.Apply(Settings.Current.Theme);

        InitLogging();
        InitProfiles();
        InitJobs();
        InitEmail();
        InitScheduler();
        InitTray();

        if (!(Settings.Current.StartMinimized && HasTray))
            ShowMainWindow();

        AfterStartup();
    }

    partial void CheckSingleInstance(ref bool proceed);
    partial void InitLogging();
    partial void InitProfiles();
    partial void InitJobs();
    partial void InitEmail();
    partial void InitScheduler();
    partial void InitTray();
    partial void AfterStartup();
    partial void ConfirmBusyExit(ref bool allowed);
    partial void SettingsChanged();
    partial void Cleanup();

    private bool HasTray
    {
        get
        {
            var available = false;
            CheckTray(ref available);
            return available;
        }
    }

    partial void CheckTray(ref bool available);

    public void ShowMainWindow()
    {
        if (_mainWindow == null)
        {
            _mainWindow = new MainWindow();
            _mainWindow.Closed += (_, _) => _mainWindow = null;
        }
        Reveal(_mainWindow);
    }

    public void ShowTool<T>(Func<T> create) where T : Window
    {
        if (!_toolWindows.TryGetValue(typeof(T), out var window))
        {
            window = create();
            window.Closed += (_, _) => _toolWindows.Remove(typeof(T));
            _toolWindows[typeof(T)] = window;
        }
        Reveal(window);
    }

    private static void Reveal(Window window)
    {
        window.Show();
        if (window.WindowState == WindowState.Minimized)
            window.WindowState = WindowState.Normal;
        window.Activate();
    }

    public bool MainWindowClosing(Window window)
    {
        if (IsExiting) return false;
        if (Settings.Current.CloseToTray && HasTray)
        {
            window.Hide();
            return true;
        }
        if (ConfirmExit()) RequestExit();
        return !IsExiting;
    }

    public void RequestExit()
    {
        if (IsExiting) return;

        var allowed = true;
        ConfirmBusyExit(ref allowed);
        if (!allowed) return;

        IsExiting = true;
        foreach (var window in Windows.OfType<Window>().ToList())
        {
            WindowPlacement.Save(window);
        }
        Cleanup();
        Settings.Save();
        Shutdown();
    }

    public void ApplySettings()
    {
        ThemeManager.Apply(Settings.Current.Theme);
        foreach (var window in Windows.OfType<Window>().ToList())
            WindowPlacement.ApplyOptions(window);
        SettingsChanged();
    }
}
