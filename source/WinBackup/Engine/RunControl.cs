namespace WinBackup.Engine;

public sealed class RunControl : IDisposable
{
    private readonly CancellationTokenSource _cancel = new();
    private readonly ManualResetEventSlim _gate = new(true);

    public CancellationToken Token => _cancel.Token;
    public bool IsPaused => !_gate.IsSet;

    public void Pause() => _gate.Reset();
    public void Resume() => _gate.Set();
    public void Cancel() => _cancel.Cancel();

    public void Checkpoint() => _gate.Wait(_cancel.Token);

    public void Delay(TimeSpan time)
    {
        _cancel.Token.WaitHandle.WaitOne(time);
        Checkpoint();
    }

    public void Dispose()
    {
        _cancel.Dispose();
        _gate.Dispose();
    }
}
