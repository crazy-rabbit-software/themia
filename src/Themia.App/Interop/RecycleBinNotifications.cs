using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Themia.Core.Diagnostics;

namespace Themia.App.Interop;

/// <summary>
/// [S-1389] [S-4863] The shell's change notifications for the Recycle Bin folder, posted to a window
/// as <see cref="Message"/>: an item deleted into any drive's bin, restored, or the bin emptied. What
/// lets <see cref="Services.RecycleBinStatus"/> ask when the bin changes instead of on a clock.
/// Shell-level delivery without <c>SHCNRF_NewDelivery</c>, so a message carries nothing that must be
/// locked or freed; its contents are never read, it only says "ask again".
/// </summary>
internal sealed unsafe partial class RecycleBinNotifications : IDisposable
{
    private const string Scope = "recycle-bin";

    /// <summary>The window message a change arrives as: WM_APP + 0x51, private to the command window.</summary>
    public const uint Message = 0x8000 + 0x51;

    private const int CsidlBitBucket = 0x000A;
    private const int ShcnrfShellLevel = 0x0002;
    private const int ShcneAllEvents = 0x7FFFFFFF;

    private readonly PidlHandle _folder;
    private uint _registration;

    private RecycleBinNotifications(PidlHandle folder, uint registration)
    {
        _folder = folder;
        _registration = registration;
    }

    /// <summary>Register <paramref name="hwnd"/> for the bin's changes; null (logged) when the shell refuses.</summary>
    public static RecycleBinNotifications? Register(nint hwnd)
    {
        var hr = SHGetSpecialFolderLocation(0, CsidlBitBucket, out var pidl);
        if (hr < 0 || pidl == 0)
        {
            Log.Warn(Scope, $"could not locate the Recycle Bin folder (0x{hr:X8}); its badge updates only when a widget is shown");
            return null;
        }

        var folder = new PidlHandle(pidl);
        var entry = new ChangeNotifyEntry { Pidl = pidl, Recursive = 1 };
        var id = SHChangeNotifyRegister(hwnd, ShcnrfShellLevel, ShcneAllEvents, Message, 1, &entry);

        if (id == 0)
        {
            Log.Warn(Scope, "SHChangeNotifyRegister refused the Recycle Bin; its badge updates only when a widget is shown");
            folder.Dispose();
            return null;
        }

        return new RecycleBinNotifications(folder, id);
    }

    public void Dispose()
    {
        if (_registration != 0)
        {
            if (!SHChangeNotifyDeregister(_registration))
            {
                Log.Warn(Scope, "SHChangeNotifyDeregister failed");
            }

            _registration = 0;
        }

        // After the registration is gone: the shell no longer refers to the folder's ID list.
        _folder.Dispose();
    }

    /// <summary>SHChangeNotifyEntry: the folder's ID list and whether its subfolders count (every drive's bin).</summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct ChangeNotifyEntry
    {
        public nint Pidl;
        public int Recursive;
    }

    /// <summary>An ID list the shell allocated, freed with <c>CoTaskMemFree</c> on every path.</summary>
    private sealed partial class PidlHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public PidlHandle(nint pidl)
            : base(ownsHandle: true) => SetHandle(pidl);

        protected override bool ReleaseHandle()
        {
            CoTaskMemFree(handle);
            return true;
        }

        [LibraryImport("ole32.dll")]
        private static partial void CoTaskMemFree(nint memory);
    }

    [LibraryImport("shell32.dll")]
    private static partial int SHGetSpecialFolderLocation(nint owner, int folder, out nint pidl);

    [LibraryImport("shell32.dll")]
    private static partial uint SHChangeNotifyRegister(nint hwnd, int sources, int events, uint message, int entries, ChangeNotifyEntry* entry);

    [LibraryImport("shell32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SHChangeNotifyDeregister(uint registration);
}
