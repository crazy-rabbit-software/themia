using System.Runtime.InteropServices;
using Microsoft.Win32;
using Themia.Core.Desktop;
using Themia.Core.Diagnostics;
using Themia.Sdk;

namespace Themia.App.Interop;

/// <summary>
/// [S-5510] The desktop's namespace as Explorer's desktop lists it: the desktop root's own
/// <c>IShellFolder</c> enumerated (the user's and the Public desktop merged, and the shell items),
/// each child's parsing name, display name and attributes, kept to what Explorer's desktop shows
/// (<see cref="DesktopShown"/>: the root also lists the navigation pane's items).
/// <para>
/// Function-pointer vtables, as in <see cref="DesktopFolderView"/>, so it stays Native AOT clean. Each
/// slot number is the interface's declaration order. Every PIDL and COM object is freed on every path.
/// </para>
/// </summary>
internal static unsafe partial class DesktopNamespace
{
    private const string Scope = "desktop-items";

    private const int SlotRelease = 2;
    private const int SlotEnumObjects = 4; // IShellFolder: IUnknown (3) + ParseDisplayName
    private const int SlotGetAttributesOf = 9;
    private const int SlotGetDisplayNameOf = 11;
    private const int SlotNext = 3; // IEnumIDList: IUnknown (3)

    private const uint ShgdnNormal = 0x0;
    private const uint ShgdnForParsing = 0x8000;
    private const uint SfgaoLink = 0x00010000;
    private const uint SfgaoHidden = 0x00080000;
    private const uint SsfShowAllObjects = 0x00000001;
    private const uint SsfShowSuperHidden = 0x00040000;

    /// <summary>SHELLSTATE's first bit field: fShowAllObjects is bit 0, fShowSuperHidden bit 15.</summary>
    private const uint ShowAllObjectsBit = 1u << 0;
    private const uint ShowSuperHiddenBit = 1u << 15;
    private const uint SfgaoStream = 0x00400000;
    private const uint SfgaoFolder = 0x20000000;
    private const uint SfgaoFileSystem = 0x40000000;
    private const int SOk = 0;
    private const uint CoInitMultithreaded = 0x0;
    private const int RpcEChangedMode = unchecked((int)0x80010106);

    /// <summary>
    /// The items as the shell lists them now, or null (logged) when the namespace cannot be read: a
    /// failed read is never "no items" ([S-5510]). Blocking COM work: a worker thread only.
    /// </summary>
    public static IReadOnlyList<DesktopItem>? Read()
    {
        var init = CoInitializeEx(0, CoInitMultithreaded);
        if (init < 0 && init != RpcEChangedMode)
        {
            Log.Warn(Scope, $"CoInitializeEx failed (0x{init:X8}); the desktop's items were not read");
            return null;
        }

        nint folder = 0;
        nint items = 0;
        try
        {
            var hr = SHGetDesktopFolder(&folder);
            if (hr < 0 || folder == 0)
            {
                Log.Warn(Scope, $"SHGetDesktopFolder failed (0x{hr:X8}); the desktop's items were not read");
                return null;
            }

            // [S-5547] Explorer's Hidden items and protected system files, read now.
            var (showHidden, showSuperHidden) = ExplorerHiddenSettings();
            var enumerate = (delegate* unmanaged[Stdcall]<nint, nint, uint, nint*, int>)(*(nint**)folder)[SlotEnumObjects];
            hr = enumerate(folder, 0, DesktopViewSettings.EnumFlags(showHidden, showSuperHidden), &items);
            if (hr < 0)
            {
                Log.Warn(Scope, $"the desktop's items could not be enumerated (0x{hr:X8})");
                return null;
            }

            var found = new List<DesktopItem>();
            var shown = Shown();

            // S_FALSE with no enumerator: an empty desktop.
            if (items == 0)
            {
                return found;
            }

            var next = (delegate* unmanaged[Stdcall]<nint, uint, nint*, uint*, int>)(*(nint**)items)[SlotNext];
            while (true)
            {
                nint child = 0;
                uint fetched = 0;
                hr = next(items, 1, &child, &fetched);
                if (hr != SOk || fetched == 0 || child == 0)
                {
                    if (child != 0)
                    {
                        CoTaskMemFree(child);
                    }

                    if (hr < 0)
                    {
                        Log.Warn(Scope, $"reading the desktop's items stopped (0x{hr:X8}); the listing is kept as it was");
                        return null;
                    }

                    break;
                }

                try
                {
                    if (Describe(folder, child) is { } item && shown(item))
                    {
                        found.Add(item);
                    }
                }
                finally
                {
                    CoTaskMemFree(child);
                }
            }

            return found;
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "reading the desktop's items threw", ex);
            return null;
        }
        finally
        {
            Release(items);
            Release(folder);
            if (init >= 0)
            {
                CoUninitialize();
            }
        }
    }

    /// <summary>
    /// [S-5547] Explorer's "Hidden items" and "Hide protected operating system files", as the shell keeps
    /// them (<c>SHGetSetSettings</c>, read only): what its own desktop lists. The names follow "File name
    /// extensions" by themselves, through the shell's display names.
    /// </summary>
    private static (bool ShowHidden, bool ShowSuperHidden) ExplorerHiddenSettings()
    {
        // SHELLSTATE is 32 bytes today; a larger buffer leaves room for any the shell may write.
        var state = stackalloc uint[16];
        SHGetSetSettings(state, SsfShowAllObjects | SsfShowSuperHidden, 0);
        return ((state[0] & ShowAllObjectsBit) != 0, (state[0] & ShowSuperHiddenBit) != 0);
    }

    private const string IconSettingsKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\NewStartPanel";

    /// <summary>[S-5510] What Explorer's desktop shows, as read now: the two desktop folders, the profile, the desktop-icon settings.</summary>
    private static Func<DesktopItem, bool> Shown()
    {
        string[] folders =
        [
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
        ];
        var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var settings = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(IconSettingsKey, writable: false);
            foreach (var name in key?.GetValueNames() ?? [])
            {
                if (key!.GetValue(name) is int value)
                {
                    settings[name] = value;
                }
            }
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException)
        {
            // The defaults then: only the Recycle Bin of the shell items.
            Log.Warn(Scope, "could not read the desktop-icon settings; the desktop shows Windows' defaults", ex);
        }

        return item => DesktopShown.Shows(item, folders, profile, clsid => settings.TryGetValue(clsid, out var value) ? value : null);
    }

    /// <summary>One child as a <see cref="DesktopItem"/>, or null (debug-logged) when the shell will not name it.</summary>
    private static DesktopItem? Describe(nint folder, nint child)
    {
        var key = DisplayName(folder, child, ShgdnForParsing);
        var name = DisplayName(folder, child, ShgdnNormal);
        if (key is null || !DesktopItemKeys.IsValid(key))
        {
            Log.Debug(Scope, "a desktop item without a usable parsing name was skipped");
            return null;
        }

        var attributes = SfgaoLink | SfgaoStream | SfgaoFolder | SfgaoFileSystem | SfgaoHidden;
        var getAttributes = (delegate* unmanaged[Stdcall]<nint, uint, nint*, uint*, int>)(*(nint**)folder)[SlotGetAttributesOf];
        if (getAttributes(folder, 1, &child, &attributes) < 0)
        {
            attributes = 0;
        }

        var kind = DesktopItemKeys.IsShellItem(key) && (attributes & SfgaoFileSystem) == 0 ? DesktopItemKind.ShellItem
            : (attributes & SfgaoLink) != 0 ? DesktopItemKind.Shortcut
            : (attributes & SfgaoFolder) != 0 && (attributes & SfgaoStream) == 0 ? DesktopItemKind.Folder
            : DesktopItemKind.File;

        return new DesktopItem(key, string.IsNullOrWhiteSpace(name) ? DesktopItemKeys.FallbackName(key) : name, kind, (attributes & SfgaoHidden) != 0);
    }

    private static string? DisplayName(nint folder, nint child, uint flags)
    {
        var strret = default(StrRet);
        var getName = (delegate* unmanaged[Stdcall]<nint, nint, uint, StrRet*, int>)(*(nint**)folder)[SlotGetDisplayNameOf];
        if (getName(folder, child, flags, &strret) < 0)
        {
            return null;
        }

        // StrRetToStrW frees what the STRRET owns whatever its type, and hands back a string we free.
        char* text = null;
        if (StrRetToStrW(&strret, child, &text) < 0 || text == null)
        {
            return null;
        }

        try
        {
            return new string(text);
        }
        finally
        {
            CoTaskMemFree((nint)text);
        }
    }

    private static void Release(nint unknown)
    {
        if (unknown != 0)
        {
            ((delegate* unmanaged[Stdcall]<nint, uint>)(*(nint**)unknown)[SlotRelease])(unknown);
        }
    }

    /// <summary>
    /// The desktop-absolute parsing name of an absolute PIDL (what <see cref="Read"/> calls an item's
    /// key), or null. For the change notifications: a rename's old and new names.
    /// </summary>
    public static string? KeyOf(nint absolutePidl)
    {
        if (absolutePidl == 0)
        {
            return null;
        }

        char* text = null;
        if (SHGetNameFromIDList(absolutePidl, SigdnDesktopAbsoluteParsing, &text) < 0 || text == null)
        {
            return null;
        }

        try
        {
            return new string(text);
        }
        finally
        {
            CoTaskMemFree((nint)text);
        }
    }

    private const uint SigdnDesktopAbsoluteParsing = 0x80028000;

    /// <summary>STRRET: a type, then an 8-aligned union of a pointer, an offset or 260 ANSI characters.</summary>
    [StructLayout(LayoutKind.Explicit, Size = 272)]
    private struct StrRet
    {
        [FieldOffset(0)]
        public uint Type;
    }

    [LibraryImport("shell32.dll")]
    private static partial int SHGetDesktopFolder(nint* folder);

    [LibraryImport("shell32.dll")]
    private static partial void SHGetSetSettings(uint* state, uint mask, int set);

    [LibraryImport("shell32.dll")]
    private static partial int SHGetNameFromIDList(nint pidl, uint form, char** name);

    [LibraryImport("shlwapi.dll")]
    private static partial int StrRetToStrW(StrRet* strret, nint pidl, char** text);

    [LibraryImport("ole32.dll")]
    private static partial int CoInitializeEx(nint reserved, uint flags);

    [LibraryImport("ole32.dll")]
    private static partial void CoUninitialize();

    [LibraryImport("ole32.dll")]
    private static partial void CoTaskMemFree(nint memory);
}
