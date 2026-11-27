using System.IO.Compression;
using System.Security.Cryptography;
using WinBackup.Models;

namespace WinBackup.Engine;

public sealed partial class Executor
{
    private void RunBackup(JobPlan plan, List<PairPlan> pairs)
    {
        foreach (var group in pairs.GroupBy(p => p.BackupPath, StringComparer.OrdinalIgnoreCase))
        {
            var path = group.Key;
            var folder = group.First().BackupBase;
            if (Path.Exists(path))
            {
                _report.Add(EntryLevel.Error, "Backup", path, "This backup already exists. Create a new preview.");
                continue;
            }

            var errorsBefore = _report.Errors;
            var copiedBefore = _report.Copied;
            var finished = false;
            try
            {
                if (_profile.BackupStyle == BackupStyle.Zip)
                {
                    WriteArchive(path, group.ToList());
                }
                else if (Attempt("Create folder", path, () => Directory.CreateDirectory(path)))
                {
                    foreach (var pair in group)
                        RunPair(pair);
                }
                finished = true;
            }
            finally
            {
                var complete = finished
                               && _report.Errors == errorsBefore
                               && _report.Copied > copiedBefore
                               && plan.Failures.Count == 0
                               && plan.Pairs.Where(p => p.BackupPath == path).All(p =>
                                   p.Failure == null && p.Problems.All(x => !x.IsError)
                                   && p.Items.All(i => i.Action != ItemAction.None));

                if (Path.Exists(path))
                    BackupHistory.Add(new BackupRecord
                    {
                        ProfileId = _profile.Id,
                        Folder = folder,
                        Path = path,
                        Created = DateTime.Now,
                        Complete = complete
                    });

                if (complete) PruneBackups(folder);
                else if (_profile.BackupGenerations > 0 && finished)
                    _report.Add(EntryLevel.Warning, "Old backups", folder, "Old backups were kept because this backup is not complete.");
            }
        }
    }

    private CompressionLevel Level => _profile.Compression switch
    {
        ZipCompression.Fastest => CompressionLevel.Fastest,
        ZipCompression.Stored => CompressionLevel.NoCompression,
        _ => CompressionLevel.Optimal
    };

    private static DateTimeOffset ZipTime(DateTime utc)
    {
        var local = utc.ToLocalTime();
        if (local.Year < 1980) local = new DateTime(1980, 1, 1);
        if (local.Year > 2107) local = new DateTime(2107, 12, 31);
        return new DateTimeOffset(local);
    }

    private void WriteArchive(string path, List<PairPlan> pairs)
    {
        var temporary = path + ".part";
        var hashes = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var added = new List<string>();
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using (var archive = new ZipArchive(new FileStream(temporary, FileMode.Create, FileAccess.ReadWrite), ZipArchiveMode.Create))
            {
                foreach (var pair in pairs)
                    foreach (var item in pair.Items.Where(i => i.Action == ItemAction.CopyToDestination))
                    {
                        _control.Checkpoint();
                        var name = Path.Combine(pair.ArchiveFolder, item.RelativePath).Replace('\\', '/');
                        if (item.IsDirectory)
                            archive.CreateEntry(name + "/");
                        else if (AddEntry(archive, Path.Combine(pair.SourceRoot, item.RelativePath), name, hashes))
                            added.Add(name);
                    }
            }

            if (_profile.Verify) VerifyArchive(temporary, hashes);
            File.Move(temporary, path);
            foreach (var name in added)
            {
                _report.Copied++;
                _report.Add(EntryLevel.Info, "Added to ZIP", name, path);
            }
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            _report.Add(EntryLevel.Error, "ZIP", path, "The ZIP file could not be created: " + ex.Message);
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

    private bool AddEntry(ZipArchive archive, string source, string name, Dictionary<string, byte[]> hashes)
    {
        Report("Compressing", source);
        FileStream? input = null;
        _filesDone++;
        if (!Attempt("Copy", source, () => input = OpenSource(source))) return false;

        using (input!)
        {
            var entry = archive.CreateEntry(name, Level);
            entry.LastWriteTime = ZipTime(File.GetLastWriteTimeUtc(source));
            using var output = entry.Open();
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[BufferSize];
            while (true)
            {
                _control.Checkpoint();
                int read;
                try
                {
                    read = input!.Read(buffer, 0, buffer.Length);
                }
                catch (Exception ex) when (IsExpected(ex))
                {
                    _report.Add(EntryLevel.Error, "Copy", source, "The file in the ZIP is incomplete: " + ex.Message);
                    return false;
                }
                if (read == 0) break;
                output.Write(buffer, 0, read);
                hash.AppendData(buffer, 0, read);
                _bytesDone += read;
                _report.Bytes += read;
                Report("Compressing", source);
            }
            hashes[name] = hash.GetHashAndReset();
        }
        return true;
    }

    private void VerifyArchive(string path, Dictionary<string, byte[]> hashes)
    {
        using var archive = ZipFile.OpenRead(path);
        foreach (var entry in archive.Entries)
        {
            if (!hashes.TryGetValue(entry.FullName, out var expected)) continue;
            Report("Verifying", entry.FullName);
            using var stream = entry.Open();
            if (!HashOf(stream).AsSpan().SequenceEqual(expected))
                _report.Add(EntryLevel.Error, "Verify", entry.FullName, "The file in the ZIP does not match the source.");
        }
    }

    private void PruneBackups(string folder)
    {
        if (_profile.BackupGenerations <= 0) return;

        var records = BackupHistory.Load();
        var mine = records
            .Where(r => r.ProfileId == _profile.Id && r.Folder.Equals(folder, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.Created)
            .ToList();

        var kept = 0;
        foreach (var record in mine)
        {
            if (!Path.Exists(record.Path))
            {
                records.Remove(record);
                continue;
            }
            if (kept < _profile.BackupGenerations)
            {
                if (record.Complete) kept++;
                continue;
            }
            if (!string.Equals(Path.GetDirectoryName(record.Path), folder, StringComparison.OrdinalIgnoreCase)) continue;

            if (Attempt("Remove old backup", record.Path, () =>
                {
                    if (Directory.Exists(record.Path)) DeleteTree(new DirectoryInfo(record.Path));
                    else
                    {
                        ClearReadOnly(record.Path);
                        File.Delete(record.Path);
                    }
                }))
            {
                records.Remove(record);
                _report.Add(EntryLevel.Info, "Removed old backup", record.Path);
            }
        }
        BackupHistory.Save(records);
    }

    partial void RunSpecial(JobPlan plan, List<PairPlan> pairs, ref bool handled)
    {
        if (plan.Profile.Mode != CopyMode.Backup) return;
        handled = true;
        RunBackup(plan, pairs);
    }
}
