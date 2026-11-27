using System.Diagnostics;
using WinBackup.Engine;
using WinBackup.Models;

namespace WinBackup.Services;

public sealed record JobRequest(Profile Profile, string Trigger, JobPlan? Plan = null, bool AllowBlocked = false);

public sealed class JobRunner
{
    private readonly object _sync = new();
    private readonly Queue<JobRequest> _queue = new();
    private readonly Stopwatch _throttle = Stopwatch.StartNew();
    private JobRequest? _current;
    private RunControl? _control;
    private Thread? _worker;
    private string _lastPhase = "";

    public event Action<JobProgress>? Progress;
    public event Action<RunReport>? Completed;
    public event Action? StateChanged;

    public bool IsBusy { get { lock (_sync) return _worker != null; } }
    public bool IsPaused { get { lock (_sync) return _control?.IsPaused == true; } }
    public int Waiting { get { lock (_sync) return _queue.Count; } }
    public string CurrentName { get { lock (_sync) return _current?.Profile.Name ?? ""; } }

    public bool Enqueue(JobRequest request)
    {
        lock (_sync)
        {
            if (_current?.Profile.Id == request.Profile.Id || _queue.Any(r => r.Profile.Id == request.Profile.Id))
                return false;
            _queue.Enqueue(request);
            if (_worker == null)
            {
                _worker = new Thread(Work) { IsBackground = true, Name = "WinBackup jobs" };
                _worker.SetApartmentState(ApartmentState.STA);
                _worker.Start();
            }
        }
        StateChanged?.Invoke();
        return true;
    }

    public void TogglePause()
    {
        lock (_sync)
        {
            if (_control == null) return;
            if (_control.IsPaused) _control.Resume();
            else _control.Pause();
        }
        StateChanged?.Invoke();
    }

    public void CancelAll()
    {
        lock (_sync)
        {
            _queue.Clear();
            _control?.Cancel();
        }
        StateChanged?.Invoke();
    }

    public void WaitForIdle(TimeSpan limit)
    {
        Thread? worker;
        lock (_sync) worker = _worker;
        worker?.Join(limit);
    }

    private void Work()
    {
        while (true)
        {
            JobRequest request;
            RunControl control;
            lock (_sync)
            {
                if (_queue.Count == 0)
                {
                    _worker = null;
                    _current = null;
                    break;
                }
                request = _current = _queue.Dequeue();
                control = _control = new RunControl();
            }
            StateChanged?.Invoke();

            var report = Run(request, control);

            lock (_sync)
            {
                _control = null;
                _current = null;
                control.Dispose();
            }
            Completed?.Invoke(report);
        }
        StateChanged?.Invoke();
    }

    private RunReport Run(JobRequest request, RunControl control)
    {
        var report = new RunReport
        {
            ProfileId = request.Profile.Id,
            ProfileName = request.Profile.Name,
            Trigger = request.Trigger
        };
        LogService.Info($"{report.ProfileName}: started ({request.Trigger})");
        try
        {
            var plan = request.Plan ?? Planner.Build(request.Profile, control,
                path => Publish(new JobProgress(report.ProfileName, "Scanning", 0, 0, 0, 0, path)));
            new Executor(plan.Profile, report, control, Publish).Run(plan, request.AllowBlocked);
            report.Outcome = report.Errors > 0 ? RunOutcome.Errors : RunOutcome.Success;
        }
        catch (OperationCanceledException)
        {
            report.Outcome = RunOutcome.Cancelled;
            report.Add(EntryLevel.Warning, "Cancelled", report.ProfileName, "The run was cancelled.");
        }
        catch (Exception ex)
        {
            report.Add(EntryLevel.Error, "Run", report.ProfileName, ex.Message);
            report.Outcome = RunOutcome.Errors;
            LogService.Error(ex.ToString());
        }

        report.Finished = DateTime.Now;
        foreach (var entry in report.Entries.Where(e => e.Level == EntryLevel.Error).Take(50))
            LogService.Error($"{report.ProfileName}: {entry.Action}: {entry.Path}: {entry.Message}");
        LogService.Info($"{report.ProfileName}: {report.Summary}");
        return report;
    }

    private void Publish(JobProgress progress)
    {
        if (progress.Phase == _lastPhase && _throttle.ElapsedMilliseconds < 120) return;
        _lastPhase = progress.Phase;
        _throttle.Restart();
        Progress?.Invoke(progress);
    }
}
