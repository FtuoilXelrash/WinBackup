using System.Runtime.InteropServices;

namespace WinBackup.Engine;

public static class RecycleBin
{
    private const uint Delete = 3;
    private const ushort Silent = 0x0004;
    private const ushort NoConfirmation = 0x0010;
    private const ushort AllowUndo = 0x0040;
    private const ushort NoErrorUi = 0x0400;
    private const ushort WarnBeforePermanentDelete = 0x4000;
    private const int LongestPath = 259;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct FileOperation
    {
        public IntPtr Window;
        public uint Function;
        public string From;
        public string? To;
        public ushort Flags;
        [MarshalAs(UnmanagedType.Bool)] public bool Aborted;
        public IntPtr NameMappings;
        public string? ProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "SHFileOperationW")]
    private static extern int Execute(ref FileOperation operation);

    public static void Send(string path)
    {
        if (path.Length > LongestPath)
            throw new NotSupportedException("The path is too long for the Recycle Bin. Use a version folder for this profile.");

        var root = Path.GetPathRoot(path)!;
        if (root.StartsWith(@"\\", StringComparison.Ordinal) || new DriveInfo(root).DriveType != DriveType.Fixed)
            throw new NotSupportedException("This drive has no Recycle Bin. Use a version folder for this profile.");

        var operation = new FileOperation
        {
            Function = Delete,
            From = path + "\0\0",
            Flags = Silent | NoConfirmation | AllowUndo | NoErrorUi | WarnBeforePermanentDelete
        };
        var result = Execute(ref operation);
        if (result != 0 || operation.Aborted || File.Exists(path))
            throw new IOException($"The file could not be moved to the Recycle Bin (code {result}).");
    }
}
