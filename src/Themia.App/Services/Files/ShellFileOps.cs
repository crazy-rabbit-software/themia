using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Themia.Core.Diagnostics;
using Themia.Core.Files;
using Themia.Core.Localization;
using Themia.Core.Widgets;

namespace Themia.App.Services.Files;

/// <summary>How a file operation ended.</summary>
internal enum FileOpOutcome
{
    Done,

    /// <summary>The user cancelled in the shell's progress or conflict dialog. Not a failure.</summary>
    Cancelled,

    Failed,
}

/// <summary>
/// [S-1330]/[S-1331] File operations, done by the shell exactly as Explorer does them: copy, move,
/// rename and delete through <c>IFileOperation</c> (progress, conflict and "too big for the Recycle
/// Bin" dialogs, and Ctrl+Z in Explorer undoes them), plus open, open with, properties, show in
/// Explorer, new folder / file, and cloud pinning.
/// <para>
/// <b>Delete always recycles.</b> <c>FOFX_RECYCLEONDELETE</c> with <c>FOF_ALLOWUNDO</c>, and
/// <c>FOF_WANTNUKEWARNING</c>, so an item that cannot go to the Recycle Bin (a network share, a file
/// too large for it) is never deleted without the shell's own warning first. There is no permanent
/// delete in this class.
/// </para>
/// <para>
/// One worker thread (an STA, as <c>IFileOperation</c> requires) runs the queue, one operation at a
/// time: a long copy never blocks the desktop's UI thread, and two operations never race on the same
/// files. Results come back as tasks; completed operations raise <see cref="Completed"/> with the
/// folders they touched, which every folder widget listens to ([S-1333]).
/// </para>
/// </summary>
internal static unsafe partial class ShellFileOps
{
    private const string Scope = "file-ops";

    private const uint FofRenameOnCollision = 0x8;
    private const uint FofAllowUndo = 0x40;
    private const uint FofNoConfirmMkdir = 0x200;
    private const uint FofWantNukeWarning = 0x4000;
    private const uint FofxRecycleOnDelete = 0x80000;
    private const uint FofxAddUndoRecord = 0x20000000;
    private const int CopyEngineUserCancelled = unchecked((int)0x80270000);
    private const int ErrorCancelled = unchecked((int)0x800704C7);

    private static readonly Guid ClsidFileOperation = new("3ad05575-8857-4850-9277-11b85bdb8e09");
    private static readonly Guid IidFileOperation = new("947aab5f-0a5c-4c13-b4d6-4bf7836fc9f8");
    private static readonly Guid ClsidShellLink = new("00021401-0000-0000-C000-000000000046");
    private static readonly Guid IidShellLink = new("000214F9-0000-0000-C000-000000000046");
    private static readonly Guid IidPersistFile = new("0000010b-0000-0000-C000-000000000046");

    private static readonly BlockingCollection<Action> Queue = [];
    private static readonly Lock StartLock = new();
    private static Thread? _worker;

    /// <summary>Folders an operation changed (sources' parents and the destination). Raised on the worker thread.</summary>
    public static event Action<IReadOnlyList<string>>? Completed;

    // ---- the queue ------------------------------------------------------------------------------

    private static Task<T> Run<T>(Func<T> work)
    {
        var done = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        Start();

        Queue.Add(() =>
        {
            try
            {
                done.SetResult(work());
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "a file operation threw", ex);
                done.SetException(ex);
            }
        });

        return done.Task;
    }

    private static void Start()
    {
        lock (StartLock)
        {
            if (_worker is not null)
            {
                return;
            }

            _worker = new Thread(Pump) { IsBackground = true, Name = "Themia file operations" };
            _worker.SetApartmentState(ApartmentState.STA);
            _worker.Start();
        }
    }

    private static void Pump()
    {
        foreach (var work in Queue.GetConsumingEnumerable())
        {
            // ignore: each item catches and reports its own exceptions into its task.
            work();
        }
    }

    // ---- IFileOperation --------------------------------------------------------------------------

    /// <summary>Copy <paramref name="sources"/> into <paramref name="destination"/>. A copy into the folder the items are already in names them "- Copy".</summary>
    public static Task<FileOpOutcome> CopyAsync(IReadOnlyList<string> sources, string destination)
        => Transfer(sources, destination, move: false);

    /// <summary>Move <paramref name="sources"/> into <paramref name="destination"/>.</summary>
    public static Task<FileOpOutcome> MoveAsync(IReadOnlyList<string> sources, string destination)
        => Transfer(sources, destination, move: true);

    private static Task<FileOpOutcome> Transfer(IReadOnlyList<string> sources, string destination, bool move)
    {
        var effect = move ? DropEffect.Move : DropEffect.Copy;
        var list = FileDrops.Pastable(sources, destination, effect);

        if (list.Count == 0)
        {
            return Task.FromResult(FileOpOutcome.Done);
        }

        var sameFolder = list.Any(s => FileDrops.SamePath(FileDrops.Parent(s), destination));
        var flags = FofAllowUndo | FofNoConfirmMkdir | FofxAddUndoRecord | (sameFolder ? FofRenameOnCollision : 0);

        return Run(() => Perform(flags, [.. list.Select(Path.GetDirectoryName).OfType<string>(), destination], op =>
        {
            using var items = DragDropInterop.CreateItemArray(list);
            using var target = DragDropInterop.CreateItem(destination);

            if (items.IsNull || target.IsNull)
            {
                return -1;
            }

            // MoveItems is slot 15, CopyItems 17: (IUnknown* items, IShellItem* destination).
            var slot = move ? 15 : 17;
            var call = (delegate* unmanaged[Stdcall]<nint, nint, nint, int>)(*(nint**)op)[slot];
            return call(op, items.Pointer, target.Pointer);
        }));
    }

    /// <summary>[S-1330] Send <paramref name="paths"/> to the Recycle Bin. Never a permanent delete.</summary>
    public static Task<FileOpOutcome> RecycleAsync(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0)
        {
            return Task.FromResult(FileOpOutcome.Done);
        }

        var copy = paths.ToArray();
        var flags = FofAllowUndo | FofxRecycleOnDelete | FofWantNukeWarning | FofxAddUndoRecord;

        return Run(() => Perform(flags, [.. copy.Select(Path.GetDirectoryName).OfType<string>()], op =>
        {
            using var items = DragDropInterop.CreateItemArray(copy);

            if (items.IsNull)
            {
                return -1;
            }

            // DeleteItems is slot 19.
            var call = (delegate* unmanaged[Stdcall]<nint, nint, int>)(*(nint**)op)[19];
            return call(op, items.Pointer);
        }));
    }

    /// <summary>[S-1331] Rename one item. The name has been validated by <see cref="FileNames.Validate"/>.</summary>
    public static Task<FileOpOutcome> RenameAsync(string path, string newName)
    {
        if (FileNames.Validate(newName) != FileNameProblem.None)
        {
            return Task.FromResult(FileOpOutcome.Failed);
        }

        return Run(() => Perform(FofAllowUndo | FofxAddUndoRecord, [Path.GetDirectoryName(path) ?? path], op =>
        {
            using var item = DragDropInterop.CreateItem(path);

            if (item.IsNull)
            {
                return -1;
            }

            // RenameItem is slot 12: (IShellItem*, LPCWSTR, IFileOperationProgressSink*).
            fixed (char* name = newName)
            {
                var call = (delegate* unmanaged[Stdcall]<nint, nint, char*, nint, int>)(*(nint**)op)[12];
                return call(op, item.Pointer, name, 0);
            }
        }));
    }

    /// <summary>
    /// One <c>IFileOperation</c>: create, flag, queue via <paramref name="queue"/>, perform, read
    /// whether anything was aborted, release. Worker thread.
    /// </summary>
    private static FileOpOutcome Perform(uint flags, IReadOnlyList<string> touched, Func<nint, int> queue)
    {
        nint op;
        var clsid = ClsidFileOperation;
        var iid = IidFileOperation;
        var hr = CoCreateInstance(&clsid, 0, 1 /* CLSCTX_INPROC_SERVER */, &iid, &op);

        if (hr < 0 || op == 0)
        {
            Log.Error(Scope, $"could not create IFileOperation (0x{hr:X8})");
            return FileOpOutcome.Failed;
        }

        using var owned = new DragDropInterop.ComRef(op);

        // SetOperationFlags is slot 5.
        hr = ((delegate* unmanaged[Stdcall]<nint, uint, int>)(*(nint**)op)[5])(op, flags);

        if (hr < 0)
        {
            Log.Error(Scope, $"SetOperationFlags refused (0x{hr:X8})");
            return FileOpOutcome.Failed;
        }

        hr = queue(op);

        if (hr < 0)
        {
            Log.Warn(Scope, $"the operation could not be queued (0x{hr:X8})");
            return FileOpOutcome.Failed;
        }

        // PerformOperations is slot 21; GetAnyOperationsAborted 22.
        hr = ((delegate* unmanaged[Stdcall]<nint, int>)(*(nint**)op)[21])(op);

        int aborted = 0;
        ((delegate* unmanaged[Stdcall]<nint, int*, int>)(*(nint**)op)[22])(op, &aborted);

        Notify(touched);

        if (hr is CopyEngineUserCancelled or ErrorCancelled || (hr >= 0 && aborted != 0))
        {
            return FileOpOutcome.Cancelled;
        }

        if (hr < 0)
        {
            Log.Warn(Scope, $"the shell reported the operation failed (0x{hr:X8})");
            return FileOpOutcome.Failed;
        }

        return FileOpOutcome.Done;
    }

    /// <summary>Say which folders changed; also for operations outside the queue (a ZIP, [S-6419]).</summary>
    internal static void Notify(IReadOnlyList<string> folders)
    {
        try
        {
            Completed?.Invoke(folders);
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "a file-operation listener failed", ex);
        }
    }

    // ---- creating --------------------------------------------------------------------------------

    /// <summary>[S-1331] A new folder named "New folder" (or the next free "New folder (2)") in <paramref name="destination"/>. Returns its path, or null.</summary>
    public static string? NewFolder(string destination)
    {
        try
        {
            var name = FileNames.Unique(Str.Shell_Files_NewFolder.Text(), Names(destination));
            var path = Path.Combine(destination, name);
            Directory.CreateDirectory(path);
            Notify([destination]);
            return path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warn(Scope, $"could not create a folder in '{destination}'", ex);
            return null;
        }
    }

    /// <summary>[S-1331] A new file from a ShellNew template in <paramref name="destination"/>. Returns its path, or null.</summary>
    public static string? NewFile(string destination, ShellNewTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);

        var path = Path.Combine(destination, FileNames.Unique(Str.Shell_Files_NewFile.Format(("name", template.Label)) + template.Extension, Names(destination)));

        try
        {
            switch (template.Kind)
            {
                case ShellNewKind.Copy when template.Source is { } source && File.Exists(source):
                    File.Copy(source, path, overwrite: false);
                    break;

                case ShellNewKind.Data when template.Data is { } data:
                    using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
                    {
                        stream.Write(data);
                    }

                    break;

                default:
                    using (new FileStream(path, FileMode.CreateNew, FileAccess.Write))
                    {
                        // An empty file: ShellNew's NullFile, or a template whose source has gone.
                    }

                    break;
            }

            Notify([destination]);
            return path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warn(Scope, $"could not create a {template.Extension} file in '{destination}'", ex);
            return null;
        }
    }

    private static IEnumerable<string> Names(string folder)
        => Directory.EnumerateFileSystemEntries(folder).Select(Path.GetFileName).OfType<string>();

    // ---- handing to the shell --------------------------------------------------------------------

    /// <summary>[S-1330] Open an item as a double-click in Explorer would. Never throws.</summary>
    public static void Open(string path)
    {
        try
        {
            // using: ShellExecute returns a process handle even when nothing waits on it.
            using var process = Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            Log.Warn(Scope, $"could not open '{path}'", ex);
        }
    }

    /// <summary>[S-1330] Windows' "Open with" dialog for one file, on the worker (it is modal).</summary>
    public static void OpenWith(string path)
    {
        _ = Run(() =>
        {
            fixed (char* file = path)
            {
                var info = new OpenAsInfo { File = file, Flags = 0x1 | 0x4 /* OAIF_ALLOW_REGISTRATION | OAIF_EXEC */ };
                var hr = SHOpenWithDialog(0, &info);

                if (hr < 0 && hr != ErrorCancelled)
                {
                    Log.Warn(Scope, $"the Open with dialog failed (0x{hr:X8})");
                }

                return hr;
            }
        });
    }

    /// <summary>[S-1330] The Properties sheet: one item's, or the shared sheet for several.</summary>
    public static void Properties(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0)
        {
            return;
        }

        if (paths.Count == 1)
        {
            if (!SHObjectProperties(0, 0x2 /* SHOP_FILEPATH */, paths[0], null))
            {
                Log.Warn(Scope, $"no Properties sheet for '{paths[0]}'");
            }

            return;
        }

        using var data = DragDropInterop.CreateDataObject(paths, DropEffect.None);

        if (!data.IsNull)
        {
            var hr = SHMultiFileProperties(data.Pointer, 0);

            if (hr < 0)
            {
                Log.Warn(Scope, $"no Properties sheet for {paths.Count} items (0x{hr:X8})");
            }
        }
    }

    /// <summary>[S-1330] Open Explorer at the items' folder with them selected.</summary>
    public static void ShowInExplorer(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0 || Path.GetDirectoryName(paths[0]) is not { } folder)
        {
            return;
        }

        var folderPidl = ILCreateFromPathW(folder);

        if (folderPidl == 0)
        {
            Log.Warn(Scope, $"no shell location for '{folder}'");
            return;
        }

        var items = new nint[paths.Count];

        try
        {
            for (var i = 0; i < paths.Count; i++)
            {
                items[i] = ILCreateFromPathW(paths[i]);
            }

            var children = items.Where(p => p != 0).Select(p => ILFindLastID(p)).ToArray();

            fixed (nint* list = children)
            {
                var hr = SHOpenFolderAndSelectItems(folderPidl, (uint)children.Length, list, 0);

                if (hr < 0)
                {
                    Log.Warn(Scope, $"Explorer could not show '{folder}' (0x{hr:X8})");
                }
            }
        }
        finally
        {
            foreach (var pidl in items)
            {
                if (pidl != 0)
                {
                    DragDropInterop.CoTaskMemFree(pidl);
                }
            }

            DragDropInterop.CoTaskMemFree(folderPidl);
        }
    }

    /// <summary>[S-1324] "Always keep on this device" / "Free up space" on each path; the sync engine does the rest.</summary>
    public static int SetCloudPin(IReadOnlyList<string> paths, CloudPin pin)
    {
        var failed = 0;

        foreach (var path in paths)
        {
            var attributes = GetFileAttributesW(path);

            if (attributes == uint.MaxValue || !SetFileAttributesW(path, (uint)CloudBadges.WithPin((int)attributes, pin)))
            {
                failed++;
                Log.Warn(Scope, $"could not change the cloud pin of '{path}' (win32 {Marshal.GetLastPInvokeError()})");
            }
        }

        if (paths.Count > failed && Path.GetDirectoryName(paths[0]) is { } parent)
        {
            Notify([parent]);
        }

        return failed;
    }

    /// <summary>
    /// [S-1330] Where a shortcut (.lnk) points, for navigate mode to open a linked folder inside the
    /// widget; null when it cannot be read or points nowhere on disk.
    /// <para>
    /// [S-1401] With <paramref name="withoutArguments"/>, also null when the link passes arguments
    /// (a browser profile, a game launcher): its target alone would open something else, so a drop
    /// onto the Shortcuts widget keeps the link itself.
    /// </para>
    /// </summary>
    public static string? ResolveShortcut(string path, bool withoutArguments = false)
    {
        nint link;
        var clsid = ClsidShellLink;
        var iid = IidShellLink;

        if (CoCreateInstance(&clsid, 0, 1, &iid, &link) < 0 || link == 0)
        {
            return null;
        }

        using var ownedLink = new DragDropInterop.ComRef(link);
        nint file;
        var persistIid = IidPersistFile;

        if (((delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int>)(*(nint**)link)[0])(link, &persistIid, &file) < 0 || file == 0)
        {
            return null;
        }

        using var ownedFile = new DragDropInterop.ComRef(file);

        fixed (char* name = path)
        {
            // IPersistFile::Load is slot 5 (STGM_READ = 0).
            if (((delegate* unmanaged[Stdcall]<nint, char*, uint, int>)(*(nint**)file)[5])(file, name, 0) < 0)
            {
                return null;
            }
        }

        var buffer = stackalloc char[1024];

        // IShellLinkW::GetPath is slot 3: (LPWSTR, int, WIN32_FIND_DATAW*, DWORD flags).
        if (((delegate* unmanaged[Stdcall]<nint, char*, int, nint, uint, int>)(*(nint**)link)[3])(link, buffer, 1024, 0, 0) != 0)
        {
            return null;
        }

        var target = new string(buffer);

        if (withoutArguments && target.Length > 0)
        {
            // IShellLinkW::GetArguments is slot 10: (LPWSTR, int).
            buffer[0] = '\0';

            if (((delegate* unmanaged[Stdcall]<nint, char*, int, int>)(*(nint**)link)[10])(link, buffer, 1024) < 0
                || new string(buffer).Trim().Length > 0)
            {
                return null;
            }
        }

        return target.Length == 0 ? null : target;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct OpenAsInfo
    {
        public char* File;
        public char* Class;
        public uint Flags;
    }

    [LibraryImport("ole32.dll")]
    private static partial int CoCreateInstance(Guid* clsid, nint outer, uint context, Guid* iid, nint* result);

    [LibraryImport("shell32.dll")]
    private static partial int SHOpenWithDialog(nint window, OpenAsInfo* info);

    [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SHObjectProperties(nint window, uint type, string name, string? page);

    [LibraryImport("shell32.dll")]
    private static partial int SHMultiFileProperties(nint data, uint flags);

    [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint ILCreateFromPathW(string path);

    [LibraryImport("shell32.dll")]
    private static partial nint ILFindLastID(nint pidl);

    [LibraryImport("shell32.dll")]
    private static partial int SHOpenFolderAndSelectItems(nint folder, uint count, nint* items, uint flags);

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    private static partial uint GetFileAttributesW(string path);

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetFileAttributesW(string path, uint attributes);
}
