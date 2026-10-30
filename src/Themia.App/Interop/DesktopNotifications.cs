using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Themia.Core.Diagnostics;

namespace Themia.App.Interop;

/// <summary>One change the shell reported on the desktop: what kind, and the old and new keys of a rename.</summary>
/// <param name="Renamed">A rename's (old, new) keys; null for any other change.</param>
/// <param name="IconsChanged">The shell's icons changed (an association, an image, an item's attributes): drawn icons are stale.</param>
internal readonly record struct DesktopChange((string Old, string New)? Renamed, bool IconsChanged);

/// <summary>
/// [S-5511] The shell's change notifications for the desktop, posted to the command window as
/// <see cref="Message"/>: the desktop root and both desktop folders (the user's and the Public one),
/// shell and interrupt level, every event. New delivery, so each message is a handle locked with
/// <c>SHChangeNotification_Lock</c> while it is handled: that is how a rename's two names are read.
/// Registered while the desktop shows Themia's items; disposed otherwise.
/// <para>
/// One registration per folder: on Windows 11 build 26300 a call naming two or more entries crashes
/// the process inside windows.storage.dll, so each folder is its own call to the same message.
/// </para>
/// </summary>
internal sealed unsafe partial class DesktopNotifications : IDisposable
{
    private const string Scope = "desktop-items";

    /// <summary>The window message a change arrives as: WM_APP + 0x52, private to the command window.</summary>
    public const uint Message = 0x8000 + 0x52;

    private const int CsidlDesktop = 0x0000;
    private const int CsidlDesktopDirectory = 0x0010;
    private const int CsidlCommonDesktopDirectory = 0x0019;
    private const int ShcnrfInterruptLevel = 0x0001;
    private const int ShcnrfShellLevel = 0x0002;
    private const int ShcnrfNewDelivery = 0x8000;
    private const int ShcneAllEvents = 0x7FFFFFFF;
    private const int ShcneRenameItem = 0x00000001;
    private const int ShcneRenameFolder = 0x00020000;
    private const int ShcneAttributes = 0x00000800;
    private const int ShcneUpdateItem = 0x00002000;
    private const int ShcneUpdateImage = 0x00008000;
    private const int ShcneAssocChanged = 0x08000000;

    private readonly List<PidlHandle> _folders;
    private readonly List<uint> _registrations;

    private DesktopNotifications(List<PidlHandle> folders, List<uint> registrations)
    {
        _folders = folders;
        _registrations = registrations;
    }

    /// <summary>Register <paramref name="hwnd"/> for the desktop's changes; null (logged) when the shell refuses.</summary>
    public static DesktopNotifications? Register(nint hwnd)
    {
        var folders = new List<PidlHandle>(3);
        foreach (var csidl in (ReadOnlySpan<int>)[CsidlDesktop, CsidlDesktopDirectory, CsidlCommonDesktopDirectory])
        {
            if (SHGetSpecialFolderLocation(0, csidl, out var pidl) >= 0 && pidl != 0)
            {
                folders.Add(new PidlHandle(pidl));
            }
        }

        if (folders.Count == 0)
        {
            Log.Warn(Scope, "could not locate the desktop's folders; the items are read only when the desktop choice is applied");
            return null;
        }

        var registrations = new List<uint>(folders.Count);
        foreach (var folder in folders)
        {
            var entry = new ChangeNotifyEntry { Pidl = folder.DangerousGetHandle(), Recursive = 0 };
            var id = SHChangeNotifyRegister(hwnd, ShcnrfShellLevel | ShcnrfInterruptLevel | ShcnrfNewDelivery, ShcneAllEvents, Message, 1, &entry);
            if (id != 0)
            {
                registrations.Add(id);
            }
        }

        if (registrations.Count < folders.Count)
        {
            Log.Warn(Scope, $"SHChangeNotifyRegister refused {folders.Count - registrations.Count} of the desktop's {folders.Count} folders");
        }

        if (registrations.Count == 0)
        {
            Log.Warn(Scope, "the items are read only when the desktop choice is applied");
            foreach (var folder in folders)
            {
                folder.Dispose();
            }

            return null;
        }

        return new DesktopNotifications(folders, registrations);
    }

    /// <summary>
    /// Read one notification (the window procedure's <paramref name="wParam"/> and
    /// <paramref name="lParam"/>, while it handles <see cref="Message"/>): locked, read, unlocked.
    /// Never throws; an unreadable one is a plain change.
    /// </summary>
    public static DesktopChange Read(nint wParam, nint lParam)
    {
        nint* pidls = null;
        var events = 0;
        var locked = SHChangeNotification_Lock(wParam, (uint)lParam, &pidls, &events);
        if (locked == 0)
        {
            return new DesktopChange(null, false);
        }

        try
        {
            var icons = (events & (ShcneAttributes | ShcneUpdateItem | ShcneUpdateImage | ShcneAssocChanged)) != 0;
            if ((events & (ShcneRenameItem | ShcneRenameFolder)) != 0 && pidls != null
                && DesktopNamespace.KeyOf(pidls[0]) is { } before && DesktopNamespace.KeyOf(pidls[1]) is { } after)
            {
                return new DesktopChange((before, after), icons);
            }

            return new DesktopChange(null, icons);
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "reading a desktop change notification threw", ex);
            return new DesktopChange(null, false);
        }
        finally
        {
            SHChangeNotification_Unlock(locked);
        }
    }

    public void Dispose()
    {
        foreach (var registration in _registrations)
        {
            if (!SHChangeNotifyDeregister(registration))
            {
                Log.Warn(Scope, "SHChangeNotifyDeregister failed");
            }
        }

        _registrations.Clear();

        // After the registrations are gone: the shell no longer refers to the folders' ID lists.
        foreach (var folder in _folders)
        {
            folder.Dispose();
        }

        _folders.Clear();
    }

    /// <summary>SHChangeNotifyEntry: a folder's ID list and whether its subfolders count (not here).</summary>
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

    [LibraryImport("shell32.dll")]
    private static partial nint SHChangeNotification_Lock(nint change, uint processId, nint** pidls, int* events);

    [LibraryImport("shell32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SHChangeNotification_Unlock(nint locked);
}
