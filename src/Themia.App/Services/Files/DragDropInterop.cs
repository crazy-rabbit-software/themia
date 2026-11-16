using System.Runtime.InteropServices;
using Themia.Core.Diagnostics;
using Themia.Core.Files;

namespace Themia.App.Services.Files;

/// <summary>
/// [S-1329]/[S-1332] OLE file interop with Explorer: the shell's own data object for a set of paths
/// (what a copy or a cut carries, and what a drag of our own hands a folder's shell target), reading
/// files back out of any data object (the clipboard or a drop), saving "virtual" files (Outlook
/// attachments), and an <c>IDropTarget</c> for an HWND. There is no drop source here: a drag out is
/// XAML's own, started by the view (<c>ItemDrag</c>, [S-5549]).
/// <para>
/// Native AOT: every COM call goes through a function-pointer vtable slot, and the COM object
/// implemented here is a native block whose vtable points at <c>[UnmanagedCallersOnly]</c> methods —
/// no runtime-generated COM wrappers. Every interface pointer is a <see cref="ComRef"/> released on
/// every path, every <c>STGMEDIUM</c> goes back through <c>ReleaseStgMedium</c>, and every PIDL through
/// <c>CoTaskMemFree</c>. No callback lets a managed exception reach OLE.
/// </para>
/// <para>
/// The data object is the shell's (<c>IShellItemArray</c> → <c>BHID_DataObject</c>), not one built
/// here: it is the object Explorer itself puts on the clipboard and drags, so it carries
/// <c>CF_HDROP</c>, the shell ID lists, the drag image and the paste handshake exactly as Explorer
/// expects. Only <c>Preferred DropEffect</c> is added, which is what makes a paste a move.
/// </para>
/// <para>
/// Thread: the UI thread (an STA). OLE is initialised there once by <see cref="EnsureOle"/>; the
/// clipboard and the drop target need it.
/// </para>
/// </summary>
internal static unsafe partial class DragDropInterop
{
    private const string Scope = "file-interop";

    internal const ushort CfHdrop = 15;
    private const uint DvAspectContent = 1;
    private const uint TymedHGlobal = 1;
    private const uint TymedIStream = 4;
    private const int SOk = 0;
    private const int ENoInterface = unchecked((int)0x80004002);
    private const int EPointer = unchecked((int)0x80004003);
    private const int DragDropENotRegistered = unchecked((int)0x80040100);
    private const int DragDropEAlreadyRegistered = unchecked((int)0x80040101);
    private const int DragDropEInvalidHwnd = unchecked((int)0x80040102);
    private const uint GmemMoveable = 0x2;

    private static readonly Guid IidUnknown = new("00000000-0000-0000-C000-000000000046");
    private static readonly Guid IidDataObject = new("0000010e-0000-0000-C000-000000000046");
    private static readonly Guid IidDropTarget = new("00000122-0000-0000-C000-000000000046");
    private static readonly Guid IidShellItemArray = new("b63ea76d-1f85-456f-a19c-48159efa858b");
    internal static readonly Guid IidShellItem = new("43826d1e-e718-42ee-bc55-a1e261c37bfe");
    private static readonly Guid BhidDataObject = new("B8C0BD9F-ED24-455c-83E6-D5390C4FE8C4");

    [ThreadStatic]
    private static bool _oleReady;

    private static ushort _preferredEffect;
    private static ushort _descriptor;
    private static ushort _contents;

    /// <summary><c>Preferred DropEffect</c>, registered on first use.</summary>
    internal static ushort PreferredEffectFormat => _preferredEffect != 0 ? _preferredEffect : _preferredEffect = Register("Preferred DropEffect");

    internal static ushort FileDescriptorFormat => _descriptor != 0 ? _descriptor : _descriptor = Register("FileGroupDescriptorW");

    private static ushort ContentsFormat => _contents != 0 ? _contents : _contents = Register("FileContents");

    /// <summary>
    /// <c>OleInitialize</c> for the calling thread, once. False (logged) when this thread cannot have
    /// OLE — then nothing here may be used on it.
    /// </summary>
    public static bool EnsureOle()
    {
        if (_oleReady)
        {
            return true;
        }

        var hr = OleInitialize(0);

        if (hr < 0)
        {
            Log.Error(Scope, $"OleInitialize failed (0x{hr:X8}); file clipboard and drag and drop are unavailable on this thread");
            return false;
        }

        // Never balanced with OleUninitialize: OLE stays up for the thread's life, which is the
        // process's, and an early uninitialise would break a drag or a clipboard still in flight.
        _oleReady = true;
        return true;
    }

    // ---- references -----------------------------------------------------------------------------

    /// <summary>An owned COM interface pointer: released exactly once, on dispose.</summary>
    internal struct ComRef : IDisposable
    {
        public nint Pointer;

        public ComRef(nint pointer) => Pointer = pointer;

        public readonly bool IsNull => Pointer == 0;

        /// <summary>Hand the pointer on (to OLE, to a caller) without releasing it here.</summary>
        public nint Detach()
        {
            var p = Pointer;
            Pointer = 0;
            return p;
        }

        public void Dispose()
        {
            var p = Pointer;
            Pointer = 0;

            if (p != 0)
            {
                Release(p);
            }
        }
    }

    internal static uint Release(nint unknown) => ((delegate* unmanaged[Stdcall]<nint, uint>)(*(nint**)unknown)[2])(unknown);

    internal static uint AddRef(nint unknown) => ((delegate* unmanaged[Stdcall]<nint, uint>)(*(nint**)unknown)[1])(unknown);

    // ---- the shell's items and data object -------------------------------------------------------

    /// <summary>
    /// An <c>IShellItemArray</c> for <paramref name="paths"/>, or a null ref (logged) when any path
    /// cannot be parsed — a half set would copy or delete something other than what was selected.
    /// Any COM thread.
    /// </summary>
    public static ComRef CreateItemArray(IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        if (paths.Count == 0)
        {
            return default;
        }

        var pidls = new nint[paths.Count];

        try
        {
            for (var i = 0; i < paths.Count; i++)
            {
                nint pidl;
                uint attributes;
                var hr = SHParseDisplayName(paths[i], 0, &pidl, 0, &attributes);

                if (hr < 0 || pidl == 0)
                {
                    Log.Warn(Scope, $"the shell could not parse '{paths[i]}' (0x{hr:X8})");
                    return default;
                }

                pidls[i] = pidl;
            }

            nint array;

            fixed (nint* list = pidls)
            {
                var hr = SHCreateShellItemArrayFromIDLists((uint)pidls.Length, list, &array);

                if (hr < 0 || array == 0)
                {
                    Log.Warn(Scope, $"SHCreateShellItemArrayFromIDLists failed (0x{hr:X8})");
                    return default;
                }
            }

            return new ComRef(array);
        }
        finally
        {
            foreach (var pidl in pidls)
            {
                if (pidl != 0)
                {
                    CoTaskMemFree(pidl);
                }
            }
        }
    }

    /// <summary>An <c>IShellItem</c> for one path, or a null ref.</summary>
    public static ComRef CreateItem(string path)
    {
        nint item;
        var iid = IidShellItem;
        var hr = SHCreateItemFromParsingName(path, 0, &iid, &item);

        if (hr < 0 || item == 0)
        {
            Log.Warn(Scope, $"no shell item for '{path}' (0x{hr:X8})");
            return default;
        }

        return new ComRef(item);
    }

    /// <summary>
    /// The shell's data object for <paramref name="paths"/>, carrying <paramref name="preferred"/> as
    /// <c>Preferred DropEffect</c> when it is not None. A null ref (logged) on failure.
    /// </summary>
    public static ComRef CreateDataObject(IReadOnlyList<string> paths, DropEffect preferred)
    {
        using var array = CreateItemArray(paths);

        if (array.IsNull)
        {
            return default;
        }

        nint data;
        var bhid = BhidDataObject;
        var iid = IidDataObject;

        // IShellItemArray::BindToHandler is slot 3.
        var bind = (delegate* unmanaged[Stdcall]<nint, nint, Guid*, Guid*, nint*, int>)(*(nint**)array.Pointer)[3];
        var hr = bind(array.Pointer, 0, &bhid, &iid, &data);

        if (hr < 0 || data == 0)
        {
            Log.Warn(Scope, $"the shell made no data object for {paths.Count} item(s) (0x{hr:X8})");
            return default;
        }

        var result = new ComRef(data);

        if (preferred != DropEffect.None && !SetBytes(data, PreferredEffectFormat, DropFiles.EncodeEffect(preferred)))
        {
            // A data object without the effect still copies; a cut that silently becomes a copy
            // is wrong, so the caller hears about it.
            result.Dispose();
            return default;
        }

        return result;
    }

    /// <summary><c>IDataObject::SetData</c> with an HGLOBAL holding <paramref name="bytes"/>; the object owns it on success.</summary>
    private static bool SetBytes(nint data, ushort format, byte[] bytes, bool quiet = false)
    {
        var global = GlobalAlloc(GmemMoveable, (nuint)bytes.Length);

        if (global == 0)
        {
            Log.Warn(Scope, "GlobalAlloc failed");
            return false;
        }

        var target = GlobalLock(global);

        if (target == 0)
        {
            GlobalFree(global);
            return false;
        }

        Marshal.Copy(bytes, 0, target, bytes.Length);
        GlobalUnlock(global);

        var formatEtc = new FormatEtc { Format = format, Aspect = DvAspectContent, Index = -1, Tymed = TymedHGlobal };
        var medium = new StgMedium { Tymed = TymedHGlobal, Handle = global };

        // IDataObject::SetData is slot 7; fRelease = TRUE hands the global to the object.
        var setData = (delegate* unmanaged[Stdcall]<nint, FormatEtc*, StgMedium*, int, int>)(*(nint**)data)[7];
        var hr = setData(data, &formatEtc, &medium, 1);

        if (hr < 0)
        {
            GlobalFree(global);

            if (quiet)
            {
                Log.Debug(Scope, $"IDataObject::SetData declined format {format} (0x{hr:X8})");
            }
            else
            {
                Log.Warn(Scope, $"IDataObject::SetData refused format {format} (0x{hr:X8})");
            }

            return false;
        }

        return true;
    }

    // ---- reading a data object -------------------------------------------------------------------

    /// <summary>The bytes of one HGLOBAL format, or null when the object does not offer it.</summary>
    private static byte[]? GetBytes(nint data, ushort format, int index = -1)
    {
        var formatEtc = new FormatEtc { Format = format, Aspect = DvAspectContent, Index = index, Tymed = TymedHGlobal };
        var medium = default(StgMedium);

        // IDataObject::GetData is slot 3.
        var getData = (delegate* unmanaged[Stdcall]<nint, FormatEtc*, StgMedium*, int>)(*(nint**)data)[3];

        if (getData(data, &formatEtc, &medium) < 0)
        {
            return null;
        }

        try
        {
            if (medium.Tymed != TymedHGlobal || medium.Handle == 0)
            {
                return null;
            }

            var size = (long)GlobalSize(medium.Handle);
            var source = GlobalLock(medium.Handle);

            if (source == 0 || size <= 0 || size > int.MaxValue)
            {
                return null;
            }

            try
            {
                var bytes = new byte[size];
                Marshal.Copy(source, bytes, 0, (int)size);
                return bytes;
            }
            finally
            {
                GlobalUnlock(medium.Handle);
            }
        }
        finally
        {
            ReleaseStgMedium(&medium);
        }
    }

    /// <summary>The file-system paths a data object carries (<c>CF_HDROP</c>), or none.</summary>
    public static IReadOnlyList<string> ReadPaths(nint data)
        => GetBytes(data, CfHdrop) is { } bytes ? DropFiles.Decode(bytes) : [];

    /// <summary>What the source asked a paste to do: Move for a cut, Copy otherwise.</summary>
    public static DropEffect ReadPreferredEffect(nint data)
        => GetBytes(data, PreferredEffectFormat) is { } bytes ? DropFiles.DecodeEffect(bytes) : DropEffect.Copy;

    /// <summary>The virtual files a data object offers (Outlook attachments), or none.</summary>
    public static IReadOnlyList<(int Index, VirtualFile File)> ReadVirtualFiles(nint data)
        => GetBytes(data, FileDescriptorFormat) is { } bytes ? FileDescriptors.Parse(bytes) : [];

    /// <summary>
    /// [S-1332] Save a data object's virtual files under <paramref name="destination"/>: folders made,
    /// each stream written to a new file (never over an existing one — the name gets " (2)"). A file
    /// that fails is removed rather than left half-written. Returns the paths written.
    /// </summary>
    public static IReadOnlyList<string> SaveVirtualFiles(nint data, string destination)
    {
        var written = new List<string>();

        foreach (var (index, file) in ReadVirtualFiles(data))
        {
            var target = Path.Combine(destination, file.RelativePath);

            if (!FileDrops.IsWithin(target, destination))
            {
                continue;
            }

            try
            {
                if (file.IsDirectory)
                {
                    Directory.CreateDirectory(target);
                    continue;
                }

                var folder = Path.GetDirectoryName(target) ?? destination;
                Directory.CreateDirectory(folder);
                var name = FileNames.Unique(Path.GetFileName(target), Directory.EnumerateFileSystemEntries(folder).Select(Path.GetFileName).OfType<string>());
                target = Path.Combine(folder, name);

                if (WriteContents(data, index, target))
                {
                    written.Add(target);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Log.Warn(Scope, $"could not save the dropped file '{file.RelativePath}'", ex);
            }
        }

        return written;
    }

    private static bool WriteContents(nint data, int index, string target)
    {
        var formatEtc = new FormatEtc { Format = ContentsFormat, Aspect = DvAspectContent, Index = index, Tymed = TymedHGlobal | TymedIStream };
        var medium = default(StgMedium);
        var getData = (delegate* unmanaged[Stdcall]<nint, FormatEtc*, StgMedium*, int>)(*(nint**)data)[3];
        var hr = getData(data, &formatEtc, &medium);

        if (hr < 0)
        {
            Log.Warn(Scope, $"the source gave no contents for virtual file {index} (0x{hr:X8})");
            return false;
        }

        var complete = false;

        try
        {
            using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);

            if (medium.Tymed == TymedHGlobal && medium.Handle != 0)
            {
                var size = (long)GlobalSize(medium.Handle);
                var source = GlobalLock(medium.Handle);

                if (source != 0)
                {
                    try
                    {
                        output.Write(new ReadOnlySpan<byte>((void*)source, (int)Math.Min(size, int.MaxValue)));
                    }
                    finally
                    {
                        GlobalUnlock(medium.Handle);
                    }
                }
            }
            else if (medium.Tymed == TymedIStream && medium.Handle != 0)
            {
                var buffer = new byte[64 * 1024];

                // ISequentialStream::Read is slot 3. A source that over-reports what it read is
                // clamped to the buffer, never trusted.
                var read = (delegate* unmanaged[Stdcall]<nint, byte*, uint, uint*, int>)(*(nint**)medium.Handle)[3];

                fixed (byte* b = buffer)
                {
                    while (true)
                    {
                        uint got;

                        if (read(medium.Handle, b, (uint)buffer.Length, &got) < 0)
                        {
                            Log.Warn(Scope, $"reading virtual file {index} failed");
                            return false;
                        }

                        if (got == 0)
                        {
                            break;
                        }

                        output.Write(buffer, 0, (int)Math.Min(got, (uint)buffer.Length));
                    }
                }
            }
            else
            {
                return false;
            }

            complete = true;
            return true;
        }
        finally
        {
            ReleaseStgMedium(&medium);

            if (!complete)
            {
                try
                {
                    File.Delete(target);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    Log.Warn(Scope, $"could not remove the partial file '{target}'", ex);
                }
            }
        }
    }

    private static int Answer(nint self, Guid* iid, nint* result, Guid own)
    {
        if (result is null)
        {
            return EPointer;
        }

        if (*iid == IidUnknown || *iid == own)
        {
            *result = self;
            return SOk;
        }

        *result = 0;
        return ENoInterface;
    }

    // ---- dropping in (seam) ----------------------------------------------------------------------

    /// <summary>
    /// What an OLE drop target forwards to. <c>keys</c> is the raw <c>MK_*</c> state (buttons and
    /// modifiers, <see cref="SurfaceDrops"/> names them). The data object pointer is valid only for
    /// the call unless the handler AddRefs it; read it with <see cref="ReadPaths"/> /
    /// <see cref="ReadVirtualFiles"/> there and then.
    /// </summary>
    internal interface IOleDropHandler
    {
        DropEffect Enter(nint data, uint keys, int screenX, int screenY, DropEffect allowed);

        DropEffect Over(uint keys, int screenX, int screenY, DropEffect allowed);

        void Leave();

        DropEffect Drop(nint data, uint keys, int screenX, int screenY, DropEffect allowed);
    }

    /// <summary>
    /// [S-1021] Register one <c>IDropTarget</c> forwarding to <paramref name="handler"/> on
    /// <paramref name="window"/> <b>and every window inside it</b>. Dispose to revoke them all. Null
    /// (logged) when OLE is unavailable or no window took it.
    /// <para>
    /// The whole tree, because OLE asks the innermost window under the pointer first and walks out:
    /// a WinUI window's content lives in child windows (the content bridge and its input site), and a
    /// target that XAML registered there would win over one on the top level. A window that already
    /// has a target has it revoked and replaced, and one that XAML tries to register later is refused
    /// as already registered, so the surface has exactly one drop path: this one.
    /// </para>
    /// </summary>
    public static IDisposable? RegisterDropTarget(nint window, IOleDropHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);

        if (!EnsureOle())
        {
            return null;
        }

        var target = DropTarget.Create(handler);
        var registered = new List<nint>();

        foreach (var hwnd in WindowTree(window))
        {
            var hr = RegisterDragDrop(hwnd, target);

            if (hr == DragDropEAlreadyRegistered)
            {
                // Someone (XAML) got there first: take the window over, so there is one drop path.
                _ = RevokeDragDrop(hwnd);
                hr = RegisterDragDrop(hwnd, target);
            }

            if (hr < 0)
            {
                Log.Warn(Scope, $"RegisterDragDrop refused window {hwnd:X} (0x{hr:X8})");
                continue;
            }

            registered.Add(hwnd);
        }

        if (registered.Count == 0)
        {
            DropTarget.Free(target);
            return null;
        }

        Log.Debug(Scope, $"drop target registered on {registered.Count} window(s) under {window:X}");
        return new Registration(registered, target);
    }

    /// <summary><paramref name="root"/> and every descendant window, parents first.</summary>
    private static List<nint> WindowTree(nint root)
    {
        var all = new List<nint> { root };

        for (var i = 0; i < all.Count; i++)
        {
            for (var child = FindWindowExW(all[i], 0, null, null); child != 0; child = FindWindowExW(all[i], child, null, null))
            {
                all.Add(child);
            }
        }

        return all;
    }

    private sealed class Registration(List<nint> windows, nint target) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            foreach (var window in windows)
            {
                var hr = RevokeDragDrop(window);

                // A window already destroyed took its registration with it.
                if (hr < 0 && hr != DragDropENotRegistered && hr != DragDropEInvalidHwnd)
                {
                    Log.Warn(Scope, $"RevokeDragDrop failed on {window:X} (0x{hr:X8})");
                }
            }

            DropTarget.Free(target);
        }
    }

    /// <summary>A native <c>IDropTarget</c>: [vtable][GCHandle to the handler]. Freed by its registration, never by its count.</summary>
    private static class DropTarget
    {
        private static nint _table;

        public static nint Create(IOleDropHandler handler)
        {
            if (_table == 0)
            {
                var table = (nint*)NativeMemory.Alloc((nuint)(7 * sizeof(nint)));
                table[0] = (nint)(delegate* unmanaged<nint, Guid*, nint*, int>)&Query;
                table[1] = (nint)(delegate* unmanaged<nint, uint>)&Immortal;
                table[2] = (nint)(delegate* unmanaged<nint, uint>)&Immortal;
                table[3] = (nint)(delegate* unmanaged<nint, nint, uint, PointL, uint*, int>)&DragEnter;
                table[4] = (nint)(delegate* unmanaged<nint, uint, PointL, uint*, int>)&DragOver;
                table[5] = (nint)(delegate* unmanaged<nint, int>)&DragLeave;
                table[6] = (nint)(delegate* unmanaged<nint, nint, uint, PointL, uint*, int>)&OnDrop;
                _table = (nint)table;
            }

            var block = (nint*)NativeMemory.Alloc((nuint)(2 * sizeof(nint)));
            block[0] = _table;
            block[1] = GCHandle.ToIntPtr(GCHandle.Alloc(handler));
            return (nint)block;
        }

        public static void Free(nint target)
        {
            var block = (nint*)target;
            GCHandle.FromIntPtr(block[1]).Free();
            NativeMemory.Free(block);
        }

        private static IOleDropHandler? Handler(nint self)
            => GCHandle.FromIntPtr(((nint*)self)[1]).Target as IOleDropHandler;

        [UnmanagedCallersOnly]
        private static int Query(nint self, Guid* iid, nint* result) => Answer(self, iid, result, IidDropTarget);

        [UnmanagedCallersOnly]
        private static uint Immortal(nint self) => 1;

        [UnmanagedCallersOnly]
        private static int DragEnter(nint self, nint data, uint keys, PointL point, uint* effect)
            => Guard(effect, () => Handler(self)?.Enter(data, keys, point.X, point.Y, (DropEffect)(*effect)));

        [UnmanagedCallersOnly]
        private static int DragOver(nint self, uint keys, PointL point, uint* effect)
            => Guard(effect, () => Handler(self)?.Over(keys, point.X, point.Y, (DropEffect)(*effect)));

        [UnmanagedCallersOnly]
        private static int DragLeave(nint self)
        {
            try
            {
                Handler(self)?.Leave();
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "a drop target failed on leave", ex);
            }

            return SOk;
        }

        [UnmanagedCallersOnly]
        private static int OnDrop(nint self, nint data, uint keys, PointL point, uint* effect)
            => Guard(effect, () => Handler(self)?.Drop(data, keys, point.X, point.Y, (DropEffect)(*effect)));

        private static int Guard(uint* effect, Func<DropEffect?> call)
        {
            try
            {
                *effect = (uint)(call() ?? DropEffect.None);
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "a drop target failed", ex);
                *effect = 0;
            }

            return SOk;
        }
    }

    // ---- natives ---------------------------------------------------------------------------------

    [StructLayout(LayoutKind.Sequential)]
    internal struct FormatEtc
    {
        public ushort Format;
        public nint TargetDevice;
        public uint Aspect;
        public int Index;
        public uint Tymed;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct StgMedium
    {
        public uint Tymed;
        public nint Handle;
        public nint ReleaseOwner;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PointL
    {
        public int X;
        public int Y;
    }

    private static ushort Register(string name)
    {
        var id = RegisterClipboardFormatW(name);

        if (id == 0)
        {
            Log.Error(Scope, $"could not register the clipboard format '{name}' (win32 {Marshal.GetLastPInvokeError()})");
        }

        return (ushort)id;
    }

    [LibraryImport("ole32.dll")]
    private static partial int OleInitialize(nint reserved);

    [LibraryImport("ole32.dll")]
    private static partial void ReleaseStgMedium(StgMedium* medium);

    [LibraryImport("ole32.dll")]
    internal static partial void CoTaskMemFree(nint memory);

    [LibraryImport("ole32.dll")]
    private static partial int RegisterDragDrop(nint window, nint target);

    [LibraryImport("ole32.dll")]
    private static partial int RevokeDragDrop(nint window);

    [LibraryImport("user32.dll", EntryPoint = "FindWindowExW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial nint FindWindowExW(nint parent, nint childAfter, string? className, string? windowName);

    [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int SHParseDisplayName(string name, nint bindContext, nint* pidl, uint attributesIn, uint* attributesOut);

    [LibraryImport("shell32.dll")]
    private static partial int SHCreateShellItemArrayFromIDLists(uint count, nint* pidls, nint* array);

    [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
    internal static partial int SHCreateItemFromParsingName(string path, nint bindContext, Guid* iid, nint* item);

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    private static partial uint RegisterClipboardFormatW(string name);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial nint GlobalAlloc(uint flags, nuint bytes);

    [LibraryImport("kernel32.dll")]
    private static partial nint GlobalLock(nint memory);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GlobalUnlock(nint memory);

    [LibraryImport("kernel32.dll")]
    private static partial nuint GlobalSize(nint memory);

    [LibraryImport("kernel32.dll")]
    private static partial nint GlobalFree(nint memory);
}
