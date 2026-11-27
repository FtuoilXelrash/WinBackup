using System.ComponentModel;
using System.Security;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using WinBackup.Models;

namespace WinBackup.Engine;

public sealed record JobProgress(string Profile, string Phase, int FilesDone, int FilesTotal,
    long BytesDone, long BytesTotal, string CurrentPath);

public sealed partial class Executor
{
    private const int BufferSize = 1024 * 1024;
    private const string StampFormat = "yyyy-MM-dd_HHmmss";

    private readonly Profile _profile;
    private readonly RunReport _report;
    private readonly RunControl _control;
    private readonly Action<JobProgress> _progress;
    private readonly string _stamp;

    private int _filesDone;
    private int _filesTotal;
    private long _bytesDone;
    private long _bytesTotal;
    private bool _versionsCreated;

    public Executor(Profile profile, RunReport report, RunControl control, Action<JobProgress> progress)
    {
        _profile = profile;
        _report = report;
        _control = control;
        _progress = progress;
        _stamp = DateTime.Now.ToString(StampFormat);
    }

    public static bool IsExpected(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException
            or SecurityException or Win32Exception or InvalidDataException;

    private static bool IsCopy(ItemAction action) =>
        action is ItemAction.CopyToDestination or ItemAction.CopyToSource;

    private static bool IsDelete(ItemAction action) =>
        action is ItemAction.DeleteDestination or ItemAction.DeleteSource;

    public void Run(JobPlan plan, bool allowBlocked)
    {
        foreach (var failure in plan.Failures)
            _report.Add(EntryLevel.Error, "Profile", _profile.Name, failure);

        var ready = new List<PairPlan>();
        foreach (var pair in plan.Pairs)
        {
            foreach (var problem in pair.Problems)
                _report.Add(problem.IsError ? EntryLevel.Error : EntryLevel.Warning, "Scan", problem.Path, problem.Message);

            var label = $"{pair.SourceRoot}  →  {pair.DestinationRoot}";
            if (pair.Failure != null)
                _report.Add(EntryLevel.Error, "Folders", label, pair.Failure);
            else if (pair.BlockReason != null && !allowBlocked)
                _report.Add(EntryLevel.Error, "Safeguard", label, "Nothing was changed. " + pair.BlockReason);
            else
                ready.Add(pair);
        }

        var copies = ready.SelectMany(p => p.Items).Where(i => !i.IsDirectory && IsCopy(i.Action)).ToList();
        _filesTotal = ready.SelectMany(p => p.Items).Count(i => !i.IsDirectory && i.Action != ItemAction.None);
        _bytesTotal = copies.Sum(i => (i.Action == ItemAction.CopyToSource ? i.Destination : i.Source)?.Size ?? 0);

        var handled = false;
        RunSpecial(plan, ready, ref handled);
        if (!handled)
            foreach (var pair in ready)
                RunPair(pair);

        PruneVersions();
    }

    partial void RunSpecial(JobPlan plan, List<PairPlan> pairs, ref bool handled);

    private void RunPair(PairPlan pair)
    {
        foreach (var item in pair.Items.Where(i => i.Action == ItemAction.None))
        {
            _report.Skipped++;
            _report.Add(EntryLevel.Warning, "Skipped", Path.Combine(pair.SourceRoot, item.RelativePath), item.Reason);
        }

        foreach (var item in pair.Items.Where(i => i.IsDirectory && IsCopy(i.Action)))
        {
            _control.Checkpoint();
            var target = Path.Combine(TargetRoot(pair, item.Action), item.RelativePath);
            Attempt("Create folder", target, () => Directory.CreateDirectory(target));
        }

        if (pair.Items.Any(i => i.Action == ItemAction.CopyToDestination))
            Attempt("Create folder", pair.DestinationRoot, () => Directory.CreateDirectory(pair.DestinationRoot));

        foreach (var item in pair.Items.Where(i => !i.IsDirectory && IsCopy(i.Action)))
            CopyItem(pair, item);

        foreach (var item in pair.Items.Where(i => !i.IsDirectory && IsDelete(i.Action)))
            DeleteFile(pair, item);

        foreach (var item in pair.Items.Where(i => i.IsDirectory && IsDelete(i.Action))
                     .OrderByDescending(i => i.RelativePath.Length))
            DeleteFolder(pair, item);
    }

    private static string TargetRoot(PairPlan pair, ItemAction action) =>
        action is ItemAction.CopyToDestination or ItemAction.DeleteDestination ? pair.DestinationRoot : pair.SourceRoot;

    private static string OtherRoot(PairPlan pair, ItemAction action) =>
        action is ItemAction.CopyToDestination or ItemAction.DeleteDestination ? pair.SourceRoot : pair.DestinationRoot;

    private bool Attempt(string action, string path, Action work)
    {
        for (var attempt = 0; ; attempt++)
        {
            _control.Checkpoint();
            try
            {
                work();
                return true;
            }
            catch (Exception ex) when (IsExpected(ex))
            {
                if (ex is (IOException or UnauthorizedAccessException) and not FileNotFoundException and not DirectoryNotFoundException
                    && attempt < _profile.Retries)
                {
                    _control.Delay(TimeSpan.FromSeconds(Math.Max(0, _profile.RetryDelaySeconds)));
                    continue;
                }
                _report.Add(EntryLevel.Error, action, path, ex.Message);
                return false;
            }
        }
    }

    private void Report(string phase, string path) =>
        _progress(new JobProgress(_profile.Name, phase, _filesDone, _filesTotal, _bytesDone, _bytesTotal, path));

    private void CopyItem(PairPlan pair, PlanItem item)
    {
        var from = Path.Combine(OtherRoot(pair, item.Action), item.RelativePath);
        var to = Path.Combine(TargetRoot(pair, item.Action), item.RelativePath);
        var targetRoot = TargetRoot(pair, item.Action);
        var before = _bytesDone;
        long copied = 0;

        Report("Copying", from);
        var succeeded = Attempt("Copy", from, () =>
        {
            _bytesDone = before;
            copied = CopyFile(from, to, targetRoot, item.RelativePath);
        });

        _filesDone++;
        if (!succeeded) return;
        _report.Copied++;
        _report.Bytes += copied;
        _report.Add(EntryLevel.Info, "Copied", to, item.Reason);
    }

    private long CopyFile(string from, string to, string targetRoot, string relative)
    {
        var folder = Path.GetDirectoryName(to)!;
        Directory.CreateDirectory(folder);
        var temporary = Path.Combine(folder, $"~wb{Guid.NewGuid():N}.part");
        try
        {
            long length;
            byte[]? hash = null;
            var attributes = File.GetAttributes(from);
            var created = File.GetCreationTimeUtc(from);
            var written = File.GetLastWriteTimeUtc(from);

            using (var input = OpenSource(from))
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize))
            {
                hash = Transfer(input, output, from, _profile.Verify);
                output.Flush();
                length = output.Length;
            }

            if (hash != null)
            {
                using var check = OpenSource(temporary);
                if (!HashOf(check).AsSpan().SequenceEqual(hash))
                    throw new IOException("Verification failed: the copy does not match the source.");
            }

            SetTimes(temporary, created, written);

            if (File.Exists(to))
            {
                ClearReadOnly(to);
                if (_profile.Removed != RemovedItems.Permanent)
                    Remove(to, targetRoot, relative);
            }
            File.Move(temporary, to, true);

            var kept = attributes & (FileAttributes.ReadOnly | FileAttributes.Hidden | FileAttributes.System | FileAttributes.Archive);
            try
            {
                File.SetAttributes(to, kept == 0 ? FileAttributes.Normal : kept);
            }
            catch (Exception ex) when (IsExpected(ex))
            {
                _report.Add(EntryLevel.Warning, "Attributes", to, ex.Message);
            }
            return length;
        }
        finally
        {
            if (File.Exists(temporary))
            {
                try { File.Delete(temporary); }
                catch (Exception ex) when (IsExpected(ex)) { _report.Add(EntryLevel.Warning, "Clean up", temporary, ex.Message); }
            }
        }
    }

    private static FileStream OpenSource(string path) =>
        new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, BufferSize, FileOptions.SequentialScan);

    private byte[]? Transfer(Stream input, Stream output, string path, bool hashWanted)
    {
        using var hash = hashWanted ? IncrementalHash.CreateHash(HashAlgorithmName.SHA256) : null;
        var buffer = new byte[BufferSize];
        int read;
        while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
        {
            _control.Checkpoint();
            output.Write(buffer, 0, read);
            hash?.AppendData(buffer, 0, read);
            _bytesDone += read;
            Report("Copying", path);
        }
        return hash?.GetHashAndReset();
    }

    private byte[] HashOf(Stream stream)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[BufferSize];
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            _control.Checkpoint();
            hash.AppendData(buffer, 0, read);
        }
        return hash.GetHashAndReset();
    }

    private void SetTimes(string path, DateTime created, DateTime written)
    {
        try
        {
            File.SetCreationTimeUtc(path, created);
            File.SetLastWriteTimeUtc(path, written);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            _report.Add(EntryLevel.Warning, "File time", path, ex.Message);
        }
    }

    private static void ClearReadOnly(string path)
    {
        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReadOnly) != 0)
            File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
    }

    private static string SafeName(string text)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var result = new string(text.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim(' ', '.', '_');
        return result.Length == 0 ? "_" : result;
    }

    private string VersionBase => Path.Combine(ProfileValidator.Normalize(_profile.VersionFolder), SafeName(_profile.Name));

    private void Remove(string path, string root, string relative)
    {
        switch (_profile.Removed)
        {
            case RemovedItems.RecycleBin:
                RecycleBin.Send(path);
                break;
            case RemovedItems.VersionFolder:
                var target = Path.Combine(VersionBase, _stamp, SafeName(root), relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                for (var i = 2; File.Exists(target); i++)
                    target = Path.Combine(VersionBase, _stamp, SafeName(root), $"{relative}.{i}");
                File.Move(path, target);
                _versionsCreated = true;
                break;
            default:
                File.Delete(path);
                break;
        }
    }

    private bool StillValid(PairPlan pair, PlanItem item, string path)
    {
        var other = OtherRoot(pair, item.Action);
        if (!Directory.Exists(other))
        {
            _report.Add(EntryLevel.Error, "Delete", path, "Not deleted because the other folder is no longer available.");
            return false;
        }

        var otherInfo = item.Action == ItemAction.DeleteDestination ? item.Source : item.Destination;
        if (otherInfo == null && Path.Exists(Path.Combine(other, item.RelativePath)))
        {
            _report.Skipped++;
            _report.Add(EntryLevel.Warning, "Skipped", path, "Not deleted because the item has reappeared on the other side.");
            return false;
        }
        return true;
    }

    private void DeleteFile(PairPlan pair, PlanItem item)
    {
        var root = TargetRoot(pair, item.Action);
        var path = Path.Combine(root, item.RelativePath);
        Report("Deleting", path);
        _filesDone++;
        if (!File.Exists(path) || !StillValid(pair, item, path)) return;

        if (Attempt("Delete", path, () =>
            {
                ClearReadOnly(path);
                Remove(path, root, item.RelativePath);
            }))
        {
            _report.Deleted++;
            _report.Add(EntryLevel.Info, "Deleted", path, item.Reason);
        }
    }

    private void DeleteFolder(PairPlan pair, PlanItem item)
    {
        var path = Path.Combine(TargetRoot(pair, item.Action), item.RelativePath);
        if (!Directory.Exists(path) || !StillValid(pair, item, path)) return;

        if (Attempt("Delete folder", path, () =>
            {
                foreach (var leftover in Directory.EnumerateFiles(path, "~wb*.part"))
                    if (Scanner.PartialFile().IsMatch(Path.GetFileName(leftover)))
                        File.Delete(leftover);

                if (Directory.EnumerateFileSystemEntries(path).Any())
                {
                    _report.Skipped++;
                    _report.Add(EntryLevel.Warning, "Folder kept", path, "The folder still contains items.");
                    return;
                }
                new DirectoryInfo(path).Attributes = FileAttributes.Directory;
                Directory.Delete(path);
                _report.Deleted++;
                _report.Add(EntryLevel.Info, "Deleted folder", path);
            }))
            return;
    }

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}_\d{6}$")]
    private static partial Regex StampFolder();

    private void PruneVersions()
    {
        if (!_versionsCreated || _profile.VersionGenerations <= 0) return;

        Attempt("Remove old versions", VersionBase, () =>
        {
            var old = new DirectoryInfo(VersionBase).GetDirectories()
                .Where(d => StampFolder().IsMatch(d.Name))
                .OrderByDescending(d => d.Name)
                .Skip(_profile.VersionGenerations);
            foreach (var folder in old)
            {
                DeleteTree(folder);
                _report.Add(EntryLevel.Info, "Removed old version", folder.FullName);
            }
        });
    }

    private static void DeleteTree(DirectoryInfo folder)
    {
        if (folder.LinkTarget == null)
        {
            foreach (var child in folder.GetDirectories())
                DeleteTree(child);
            foreach (var file in folder.GetFiles())
            {
                file.Attributes = FileAttributes.Normal;
                file.Delete();
            }
            folder.Attributes = FileAttributes.Directory;
        }
        folder.Delete();
    }
}
