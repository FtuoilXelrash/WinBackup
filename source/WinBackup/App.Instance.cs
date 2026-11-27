namespace WinBackup;

public partial class App
{
    private Mutex? _instanceMutex;
    private EventWaitHandle? _showSignal;
    private RegisteredWaitHandle? _showWait;

    partial void CheckSingleInstance(ref bool proceed)
    {
        _instanceMutex = new Mutex(true, "WinBackup_SingleInstance", out var createdNew);
        _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, "WinBackup_ShowWindow");
        if (createdNew) return;

        _showSignal.Set();
        proceed = false;
    }

    partial void AfterStartup()
    {
        _showWait = ThreadPool.RegisterWaitForSingleObject(_showSignal!,
            (_, _) => Dispatcher.BeginInvoke(ShowMainWindow), null, Timeout.Infinite, false);
    }

    private void CloseInstance()
    {
        _showWait?.Unregister(null);
        _showSignal?.Dispose();
        _instanceMutex?.Dispose();
    }

    partial void Cleanup()
    {
        CloseServices();
        Tray?.Dispose();
        CloseInstance();
    }

    partial void CloseServices();
}
