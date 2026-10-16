using System.Runtime.InteropServices;
using Themia.Core.Diagnostics;

namespace Themia.App.Services.Files;

/// <summary>
/// [S-1330] "Show more options": Windows' own context menu for items or for a folder's background,
/// with every shell extension's entries (Send to, 7-Zip, Open with Code…), as Explorer shows it.
/// <para>
/// The menu is the shell's <c>IContextMenu</c> filled into a Win32 popup and tracked at the pointer.
/// Submenus that fill themselves when opened (Send to, Open with) need the menu's owner window to
/// pass <c>WM_INITMENUPOPUP</c> / <c>WM_DRAWITEM</c> / <c>WM_MEASUREITEM</c> / <c>WM_MENUCHAR</c>
/// on to <c>IContextMenu3</c>: the owner is subclassed (<c>SetWindowSubclass</c>) for exactly as long
/// as the menu is open and unsubclassed on every path. UI thread; every COM object and the menu
/// handle are released on every path, and the subclass procedure never lets an exception out.
/// </para>
/// </summary>
internal static unsafe partial class ShellContextMenu
{
    private const string Scope = "shell-menu";
    private const uint CmfNormal = 0x0;
    private const uint CmfExplore = 0x4;
    private const uint CmfCanRename = 0x10;
    private const uint TpmReturnCmd = 0x100;
    private const uint TpmRightButton = 0x2;
    private const uint WmNull = 0x0000;
    private const uint WmDrawItem = 0x002B;
    private const uint WmMeasureItem = 0x002C;
    private const uint WmInitMenuPopup = 0x0117;
    private const uint WmMenuChar = 0x0120;
    private const int FirstCommand = 1;
    private const int LastCommand = 0x7FFF;
    private const nuint SubclassId = 0x7E5F;

    private static readonly Guid BhidSfUiObject = new("3981e225-f559-11d3-8e3a-00c04f6837d5");
    private static readonly Guid BhidSfObject = new("3981e224-f559-11d3-8e3a-00c04f6837d5");
    private static readonly Guid IidContextMenu = new("000214e4-0000-0000-c000-000000000046");
    private static readonly Guid IidContextMenu2 = new("000214f4-0000-0000-c000-000000000046");
    private static readonly Guid IidContextMenu3 = new("bcfce0a0-ec17-11d0-8d10-00a0c90f2719");
    private static readonly Guid IidShellFolder = new("000214E6-0000-0000-C000-000000000046");

    /// <summary>The open menu's IContextMenu2/3 while it is tracked, for the subclass to forward to.</summary>
    private static nint _menu2;
    private static nint _menu3;

    /// <summary>The shell's menu for <paramref name="paths"/> at a screen point; the chosen command runs.</summary>
    public static void ShowForItems(IReadOnlyList<string> paths, int screenX, int screenY, nint owner)
    {
        using var owned = ItemsMenu(paths);

        if (!owned.IsNull)
        {
            Track(owned.Pointer, CmfNormal | CmfExplore | CmfCanRename, screenX, screenY, owner);
        }
    }

    /// <summary>
    /// [S-6416] Run one of the shell's own commands on <paramref name="paths"/> by its canonical verb
    /// ("pintohome", "pintostartscreen", "Windows.ModernShare"), as its line in the full menu would.
    /// False (logged) when the shell has no such command for these items.
    /// </summary>
    public static bool InvokeVerb(IReadOnlyList<string> paths, string verb, nint owner)
    {
        using var owned = ItemsMenu(paths);

        if (owned.IsNull)
        {
            return false;
        }

        var popup = CreatePopupMenu();

        if (popup == 0)
        {
            return false;
        }

        var ansi = Marshal.StringToHGlobalAnsi(verb);

        try
        {
            // The handlers fill their commands in on QueryContextMenu; a verb is only known after it.
            var query = (delegate* unmanaged[Stdcall]<nint, nint, uint, uint, uint, uint, int>)(*(nint**)owned.Pointer)[3];
            var hr = query(owned.Pointer, popup, 0, FirstCommand, LastCommand, CmfNormal | CmfExplore);

            if (hr < 0)
            {
                Log.Warn(Scope, $"QueryContextMenu failed for '{verb}' (0x{hr:X8})");
                return false;
            }

            var info = new InvokeInfo
            {
                Size = (uint)sizeof(InvokeInfo),
                Window = owner,
                Verb = ansi,
                Show = 1, // SW_SHOWNORMAL
            };

            // IContextMenu::InvokeCommand is slot 4.
            var invoke = (delegate* unmanaged[Stdcall]<nint, InvokeInfo*, int>)(*(nint**)owned.Pointer)[4];
            hr = invoke(owned.Pointer, &info);

            if (hr < 0)
            {
                Log.Warn(Scope, $"the shell has no '{verb}' for {paths.Count} item(s) (0x{hr:X8})");
                return false;
            }

            return true;
        }
        finally
        {
            Marshal.FreeHGlobal(ansi);
            DestroyMenu(popup);
        }
    }

    /// <summary>The shell's <c>IContextMenu</c> for <paramref name="paths"/>, or a null ref (logged).</summary>
    private static DragDropInterop.ComRef ItemsMenu(IReadOnlyList<string> paths)
    {
        using var array = DragDropInterop.CreateItemArray(paths);

        if (array.IsNull)
        {
            return default;
        }

        nint menu;
        var bhid = BhidSfUiObject;
        var iid = IidContextMenu;

        // IShellItemArray::BindToHandler is slot 3.
        var bind = (delegate* unmanaged[Stdcall]<nint, nint, Guid*, Guid*, nint*, int>)(*(nint**)array.Pointer)[3];
        var hr = bind(array.Pointer, 0, &bhid, &iid, &menu);

        if (hr < 0 || menu == 0)
        {
            Log.Warn(Scope, $"no shell menu for {paths.Count} item(s) (0x{hr:X8})");
            return default;
        }

        return new DragDropInterop.ComRef(menu);
    }

    /// <summary>The shell's menu for the background of <paramref name="folder"/> (New, View, Paste…).</summary>
    public static void ShowForBackground(string folder, int screenX, int screenY, nint owner)
    {
        using var item = DragDropInterop.CreateItem(folder);

        if (item.IsNull)
        {
            return;
        }

        nint shellFolder;
        var bhid = BhidSfObject;
        var iidFolder = IidShellFolder;

        // IShellItem::BindToHandler is slot 3.
        var bind = (delegate* unmanaged[Stdcall]<nint, nint, Guid*, Guid*, nint*, int>)(*(nint**)item.Pointer)[3];

        if (bind(item.Pointer, 0, &bhid, &iidFolder, &shellFolder) < 0 || shellFolder == 0)
        {
            Log.Warn(Scope, $"no shell folder for '{folder}'");
            return;
        }

        using var ownedFolder = new DragDropInterop.ComRef(shellFolder);
        nint menu;
        var iid = IidContextMenu;

        // IShellFolder::CreateViewObject is slot 8.
        var create = (delegate* unmanaged[Stdcall]<nint, nint, Guid*, nint*, int>)(*(nint**)shellFolder)[8];
        var hr = create(shellFolder, owner, &iid, &menu);

        if (hr < 0 || menu == 0)
        {
            Log.Warn(Scope, $"no background menu for '{folder}' (0x{hr:X8})");
            return;
        }

        using var owned = new DragDropInterop.ComRef(menu);
        Track(menu, CmfNormal, screenX, screenY, owner);
    }

    private static void Track(nint menu, uint flags, int x, int y, nint owner)
    {
        var popup = CreatePopupMenu();

        if (popup == 0)
        {
            return;
        }

        var subclassed = false;
        nint menu2 = 0;
        nint menu3 = 0;

        try
        {
            // IContextMenu::QueryContextMenu is slot 3.
            var query = (delegate* unmanaged[Stdcall]<nint, nint, uint, uint, uint, uint, int>)(*(nint**)menu)[3];
            var hr = query(menu, popup, 0, FirstCommand, LastCommand, flags);

            if (hr < 0)
            {
                Log.Warn(Scope, $"QueryContextMenu failed (0x{hr:X8})");
                return;
            }

            menu3 = QueryInterface(menu, IidContextMenu3);
            menu2 = menu3 == 0 ? QueryInterface(menu, IidContextMenu2) : 0;
            _menu3 = menu3;
            _menu2 = menu2;

            if (owner != 0 && (menu2 != 0 || menu3 != 0))
            {
                subclassed = SetWindowSubclass(owner, &Forward, SubclassId, 0);
            }

            // A tracked menu only closes on an outside click when its owner is the foreground window.
            if (owner != 0)
            {
                SetForegroundWindow(owner);
            }

            var command = TrackPopupMenuEx(popup, TpmReturnCmd | TpmRightButton, x, y, owner, 0);

            if (owner != 0)
            {
                PostMessageW(owner, WmNull, 0, 0);
            }

            if (command >= FirstCommand)
            {
                Invoke(menu, command - FirstCommand, owner);
            }
        }
        finally
        {
            if (subclassed)
            {
                RemoveWindowSubclass(owner, &Forward, SubclassId);
            }

            _menu2 = 0;
            _menu3 = 0;

            if (menu2 != 0)
            {
                DragDropInterop.Release(menu2);
            }

            if (menu3 != 0)
            {
                DragDropInterop.Release(menu3);
            }

            DestroyMenu(popup);
        }
    }

    private static void Invoke(nint menu, int offset, nint owner)
    {
        var info = new InvokeInfo
        {
            Size = (uint)sizeof(InvokeInfo),
            Window = owner,
            Verb = offset, // MAKEINTRESOURCE(offset)
            Show = 1, // SW_SHOWNORMAL
        };

        // IContextMenu::InvokeCommand is slot 4.
        var invoke = (delegate* unmanaged[Stdcall]<nint, InvokeInfo*, int>)(*(nint**)menu)[4];
        var hr = invoke(menu, &info);

        if (hr < 0)
        {
            Log.Warn(Scope, $"the shell command failed (0x{hr:X8})");
        }
    }

    private static nint QueryInterface(nint unknown, Guid iid)
    {
        nint result;
        var query = (delegate* unmanaged[Stdcall]<nint, Guid*, nint*, int>)(*(nint**)unknown)[0];
        return query(unknown, &iid, &result) >= 0 ? result : 0;
    }

    /// <summary>The owner's subclass while the menu is open: owner-drawn and self-filling submenus go to the shell.</summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(System.Runtime.CompilerServices.CallConvStdcall)])]
    private static nint Forward(nint window, uint message, nuint wParam, nint lParam, nuint id, nuint data)
    {
        try
        {
            if (message is WmInitMenuPopup or WmDrawItem or WmMeasureItem or WmMenuChar)
            {
                if (_menu3 != 0)
                {
                    nint result = 0;

                    // IContextMenu3::HandleMenuMsg2 is slot 7.
                    var handle2 = (delegate* unmanaged[Stdcall]<nint, uint, nuint, nint, nint*, int>)(*(nint**)_menu3)[7];

                    if (handle2(_menu3, message, wParam, lParam, &result) >= 0)
                    {
                        return message == WmMenuChar ? result : 0;
                    }
                }
                else if (_menu2 != 0 && message != WmMenuChar)
                {
                    // IContextMenu2::HandleMenuMsg is slot 6.
                    var handle = (delegate* unmanaged[Stdcall]<nint, uint, nuint, nint, int>)(*(nint**)_menu2)[6];

                    if (handle(_menu2, message, wParam, lParam) >= 0)
                    {
                        return 0;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "forwarding a menu message failed", ex);
        }

        return DefSubclassProc(window, message, wParam, lParam);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct InvokeInfo
    {
        public uint Size;
        public uint Mask;
        public nint Window;
        public nint Verb;
        public nint Parameters;
        public nint Directory;
        public int Show;
        public uint HotKey;
        public nint Icon;
    }

    /// <summary>A point in a window's client area (physical pixels) on the screen.</summary>
    public static (int X, int Y) ToScreen(nint window, int x, int y)
    {
        var point = new Point32 { X = x, Y = y };

        if (window != 0)
        {
            ClientToScreen(window, &point);
        }

        return (point.X, point.Y);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point32
    {
        public int X;
        public int Y;
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ClientToScreen(nint window, Point32* point);

    [LibraryImport("user32.dll")]
    private static partial nint CreatePopupMenu();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyMenu(nint menu);

    [LibraryImport("user32.dll")]
    private static partial int TrackPopupMenuEx(nint menu, uint flags, int x, int y, nint window, nint parameters);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(nint window);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool PostMessageW(nint window, uint message, nuint wParam, nint lParam);

    [LibraryImport("comctl32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowSubclass(nint window, delegate* unmanaged[Stdcall]<nint, uint, nuint, nint, nuint, nuint, nint> proc, nuint id, nuint data);

    [LibraryImport("comctl32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RemoveWindowSubclass(nint window, delegate* unmanaged[Stdcall]<nint, uint, nuint, nint, nuint, nuint, nint> proc, nuint id);

    [LibraryImport("comctl32.dll")]
    private static partial nint DefSubclassProc(nint window, uint message, nuint wParam, nint lParam);
}
