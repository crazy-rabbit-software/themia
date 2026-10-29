using System.Runtime.InteropServices;
using Themia.Core.Desktop;
using Themia.Core.Diagnostics;

namespace Themia.App.Services.Files;

/// <summary>
/// [S-5551] [S-5552] What came of the desktop's background menu: a shell command ran (and whether it
/// was one of New ›'s), or one of Themia's own lines was chosen for the caller to run, or nothing.
/// </summary>
internal readonly record struct DesktopMenuChoice(bool Ran, DesktopMenuCommand? Themia, bool New)
{
    public static DesktopMenuChoice Nothing => default;
}

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
    private const uint GcsVerbW = 0x4;
    private const uint CmicMaskShiftDown = 0x10000000;

    private static readonly Guid BhidSfUiObject = new("3981e225-f559-11d3-8e3a-00c04f6837d5");
    private static readonly Guid BhidSfObject = new("3981e224-f559-11d3-8e3a-00c04f6837d5");
    private static readonly Guid IidContextMenu = new("000214e4-0000-0000-c000-000000000046");
    private static readonly Guid IidContextMenu2 = new("000214f4-0000-0000-c000-000000000046");
    private static readonly Guid IidContextMenu3 = new("bcfce0a0-ec17-11d0-8d10-00a0c90f2719");
    private static readonly Guid IidShellFolder = new("000214E6-0000-0000-C000-000000000046");

    /// <summary>The open menu's IContextMenu2/3 while it is tracked, for the subclass to forward to.</summary>
    private static nint _menu2;
    private static nint _menu3;

    /// <summary>
    /// The shell's menu for <paramref name="paths"/> at a screen point; the chosen command runs.
    /// [S-5548] <paramref name="intercept"/>, when given, sees the chosen command's canonical verb first
    /// and takes it instead of the shell when it answers true (the desktop's Rename is its own box).
    /// </summary>
    public static void ShowForItems(IReadOnlyList<string> paths, int screenX, int screenY, nint owner, Func<string, bool>? intercept = null)
    {
        using var owned = ItemsMenu(paths);

        if (!owned.IsNull)
        {
            Track(owned.Pointer, CmfNormal | CmfExplore | CmfCanRename, screenX, screenY, owner, intercept);
        }
    }

    /// <summary>
    /// [S-5548] The desktop's own background menu: the desktop root's (<c>SHGetDesktopFolder</c>,
    /// <c>IShellFolder::CreateViewObject</c>), so New, Paste, Display settings, Personalize and every
    /// third-party line are there as Explorer's desktop shows them. View, Sort by, Group by and Refresh
    /// are the shell view's own (they act on Explorer's icon list, which Themia hides) and do not come
    /// with it. [S-5551] Themia's own lines (<paramref name="themia"/>, each a command and its words) go
    /// in as one group where <see cref="DesktopBackground.Slot"/> says. Returns what was chosen.
    /// </summary>
    public static DesktopMenuChoice ShowForDesktop(int screenX, int screenY, nint owner, IReadOnlyList<(DesktopMenuCommand Command, string Text)> themia)
    {
        ArgumentNullException.ThrowIfNull(themia);
        nint root;

        if (SHGetDesktopFolder(&root) < 0 || root == 0)
        {
            Log.Warn(Scope, "no desktop folder; the desktop's background menu is not shown");
            return DesktopMenuChoice.Nothing;
        }

        using var ownedRoot = new DragDropInterop.ComRef(root);
        nint menu;
        var iid = IidContextMenu;

        // IShellFolder::CreateViewObject is slot 8.
        var create = (delegate* unmanaged[Stdcall]<nint, nint, Guid*, nint*, int>)(*(nint**)root)[8];
        var hr = create(root, owner, &iid, &menu);

        if (hr < 0 || menu == 0)
        {
            Log.Warn(Scope, $"no background menu for the desktop (0x{hr:X8})");
            return DesktopMenuChoice.Nothing;
        }

        using var owned = new DragDropInterop.ComRef(menu);
        return Track(menu, CmfNormal, screenX, screenY, owner, intercept: null, themia);
    }

    /// <summary>
    /// [S-5551] Debug (<c>--desktop-menu-check</c>): the desktop's background menu as it would open, with
    /// Themia's lines in, filled and read back but never shown or invoked: one line per item, its depth,
    /// words and verb. New ›'s lines fill themselves only when it opens, so they are not read.
    /// </summary>
    public static IReadOnlyList<string> DescribeDesktop(nint owner, IReadOnlyList<(DesktopMenuCommand Command, string Text)> themia)
    {
        var lines = new List<string>();
        nint root;
        if (SHGetDesktopFolder(&root) < 0 || root == 0)
        {
            return ["(no desktop folder)"];
        }

        using var ownedRoot = new DragDropInterop.ComRef(root);
        nint menu;
        var iid = IidContextMenu;
        var create = (delegate* unmanaged[Stdcall]<nint, nint, Guid*, nint*, int>)(*(nint**)root)[8];
        var hr = create(root, owner, &iid, &menu);
        if (hr < 0 || menu == 0)
        {
            return [$"(no background menu: 0x{hr:X8})"];
        }

        using var owned = new DragDropInterop.ComRef(menu);
        var popup = CreatePopupMenu();
        if (popup == 0)
        {
            return ["(no popup menu)"];
        }

        try
        {
            var query = (delegate* unmanaged[Stdcall]<nint, nint, uint, uint, uint, uint, int>)(*(nint**)menu)[3];
            hr = query(menu, popup, 0, FirstCommand, LastCommand, CmfNormal);
            if (hr < 0)
            {
                return [$"(QueryContextMenu failed: 0x{hr:X8})"];
            }

            AddThemiaLines(menu, popup, themia);
            Describe(menu, popup, 0, lines);
            return lines;
        }
        finally
        {
            DestroyMenu(popup);
        }
    }

    private static void Describe(nint menu, nint popup, int depth, List<string> lines)
    {
        var count = GetMenuItemCount(popup);
        for (var i = 0; i < count; i++)
        {
            var item = ItemAt(popup, i);
            var indent = new string(' ', depth * 2);
            if (item.Separator)
            {
                lines.Add(indent + "----");
                continue;
            }

            var text = ItemText(popup, i);
            var verb = item.Id is >= FirstCommand and <= LastCommand && item.Submenu == 0 ? Verb(menu, (int)item.Id - FirstCommand) : null;
            var themia = item.Id >= ThemiaFirst ? " [Themia]" : string.Empty;
            lines.Add($"{indent}{text}{(verb is null ? string.Empty : $"  ({verb})")}{(item.Submenu != 0 ? "  ›" : string.Empty)}{themia}");
            if (item.Submenu != 0)
            {
                Describe(menu, item.Submenu, depth + 1, lines);
            }
        }
    }

    /// <summary>
    /// [S-6416] Run one of the shell's own commands on <paramref name="paths"/> by its canonical verb
    /// ("pintohome", "pintostartscreen", "Windows.ModernShare"), as its line in the full menu would.
    /// False (logged) when the shell has no such command for these items.
    /// </summary>
    public static bool InvokeVerb(IReadOnlyList<string> paths, string verb, nint owner) => InvokeVerb(paths, verb, owner, shift: false);

    /// <summary>
    /// [S-5545] <see cref="InvokeVerb(IReadOnlyList{string}, string, nint)"/> as with Shift held, as
    /// Explorer invokes a command the user chose with Shift down: the shell's "delete" then deletes
    /// for good, after the shell's own prompt.
    /// </summary>
    public static bool InvokeVerb(IReadOnlyList<string> paths, string verb, nint owner, bool shift)
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
                Mask = shift ? CmicMaskShiftDown : 0,
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

    private static DesktopMenuChoice Track(
        nint menu, uint flags, int x, int y, nint owner, Func<string, bool>? intercept = null,
        IReadOnlyList<(DesktopMenuCommand Command, string Text)>? themia = null)
    {
        var popup = CreatePopupMenu();

        if (popup == 0)
        {
            return DesktopMenuChoice.Nothing;
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
                return DesktopMenuChoice.Nothing;
            }

            if (themia is { Count: > 0 })
            {
                AddThemiaLines(menu, popup, themia);
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

            if (command < FirstCommand)
            {
                return DesktopMenuChoice.Nothing;
            }

            // [S-5551] One of Themia's own: the caller runs it; the shell never sees the id.
            if (command >= ThemiaFirst)
            {
                return themia is not null && command - ThemiaFirst < themia.Count
                    ? new DesktopMenuChoice(Ran: false, themia[command - ThemiaFirst].Command, New: false)
                    : DesktopMenuChoice.Nothing;
            }

            if (intercept is not null && Verb(menu, command - FirstCommand) is { } verb && intercept(verb))
            {
                return new DesktopMenuChoice(Ran: true, null, New: false);
            }

            // [S-5552] Read before the command runs: New › is the submenu that holds New › Folder.
            var isNew = DesktopBackground.IsNewCommand(SiblingVerbs(menu, popup, (uint)command));
            Invoke(menu, command - FirstCommand, owner);
            return new DesktopMenuChoice(Ran: true, null, isNew);
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

    /// <summary>[S-5551] The first id of Themia's own lines: past every id the shell's handlers were given.</summary>
    private const int ThemiaFirst = LastCommand + 1;

    private const uint MiimId = 0x2;
    private const uint MiimSubmenu = 0x4;
    private const uint MiimFtype = 0x100;
    private const uint MftSeparator = 0x800;
    private const uint MfByPosition = 0x400;
    private const uint MfString = 0x0;
    private const uint MfSeparator = 0x800;

    /// <summary>[S-5551] Themia's group, inserted into the filled menu where <see cref="DesktopBackground.Slot"/> says.</summary>
    private static void AddThemiaLines(nint menu, nint popup, IReadOnlyList<(DesktopMenuCommand Command, string Text)> themia)
    {
        var count = GetMenuItemCount(popup);
        var lines = new List<ShellMenuLine>(Math.Max(0, count));
        for (var i = 0; i < count; i++)
        {
            var item = ItemAt(popup, i);
            var verb = !item.Separator && item.Submenu == 0 && item.Id is >= FirstCommand and <= LastCommand
                ? Verb(menu, (int)item.Id - FirstCommand)
                : null;
            lines.Add(new ShellMenuLine(item.Separator, verb));
        }

        var slot = DesktopBackground.Slot(lines);
        var at = (uint)slot.Index;
        if (slot.SeparatorBefore)
        {
            InsertMenuW(popup, at++, MfByPosition | MfSeparator, 0, null);
        }

        for (var i = 0; i < themia.Count; i++)
        {
            if (!InsertMenuW(popup, at++, MfByPosition | MfString, (nuint)(ThemiaFirst + i), themia[i].Text))
            {
                Log.Warn(Scope, $"could not add Themia's line {themia[i].Command} to the desktop's menu (win32 {Marshal.GetLastPInvokeError()})");
            }
        }

        if (slot.SeparatorAfter)
        {
            InsertMenuW(popup, at, MfByPosition | MfSeparator, 0, null);
        }
    }

    /// <summary>[S-5552] The verbs of the lines beside <paramref name="command"/> in the submenu that holds it, or null at the top level.</summary>
    private static List<string?>? SiblingVerbs(nint menu, nint popup, uint command)
    {
        var count = GetMenuItemCount(popup);
        for (var i = 0; i < count; i++)
        {
            var item = ItemAt(popup, i);
            if (item.Submenu == 0)
            {
                continue;
            }

            var sub = GetMenuItemCount(item.Submenu);
            var verbs = new List<string?>(Math.Max(0, sub));
            var holds = false;
            for (var j = 0; j < sub; j++)
            {
                var line = ItemAt(item.Submenu, j);
                holds |= line.Id == command && line.Submenu == 0;
                verbs.Add(!line.Separator && line.Submenu == 0 && line.Id is >= FirstCommand and <= LastCommand ? Verb(menu, (int)line.Id - FirstCommand) : null);
            }

            if (holds)
            {
                return verbs;
            }

            if (SiblingVerbs(menu, item.Submenu, command) is { } deeper)
            {
                return deeper;
            }
        }

        return null;
    }

    /// <summary>One item of a Win32 menu by position: a separator, its id, its submenu (0 for none).</summary>
    private static (bool Separator, uint Id, nint Submenu) ItemAt(nint popup, int position)
    {
        var info = new MenuItemInfo { Size = (uint)sizeof(MenuItemInfo), Mask = MiimId | MiimSubmenu | MiimFtype };
        return GetMenuItemInfoW(popup, (uint)position, true, &info)
            ? ((info.Type & MftSeparator) != 0, info.Id, info.Submenu)
            : (true, 0, 0);
    }

    /// <summary>An item's words by position (its accelerator's ampersand kept), for the debug description.</summary>
    private static string ItemText(nint popup, int position)
    {
        const int Capacity = 256;
        var buffer = stackalloc char[Capacity];
        var length = GetMenuStringW(popup, (uint)position, buffer, Capacity, MfByPosition);
        return length > 0 ? new string(buffer, 0, length) : "(owner-drawn)";
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MenuItemInfo
    {
        public uint Size;
        public uint Mask;
        public uint Type;
        public uint State;
        public uint Id;
        public nint Submenu;
        public nint Checked;
        public nint Unchecked;
        public nuint Data;
        public nint TypeData;
        public uint Cch;
        public nint Item;
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

    /// <summary>The canonical verb of the command at <paramref name="offset"/> (<c>GCS_VERBW</c>), or null when it has none.</summary>
    private static string? Verb(nint menu, int offset)
    {
        const int Capacity = 256;
        var buffer = stackalloc char[Capacity];
        buffer[0] = (char)0;

        // IContextMenu::GetCommandString is slot 5.
        var get = (delegate* unmanaged[Stdcall]<nint, nuint, uint, uint*, byte*, uint, int>)(*(nint**)menu)[5];
        return get(menu, (nuint)offset, GcsVerbW, null, (byte*)buffer, Capacity) >= 0 && buffer[0] != (char)0
            ? new string(buffer)
            : null;
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

    [LibraryImport("shell32.dll")]
    private static partial int SHGetDesktopFolder(nint* folder);

    [LibraryImport("user32.dll")]
    private static partial nint CreatePopupMenu();

    [LibraryImport("user32.dll")]
    private static partial int GetMenuItemCount(nint menu);

    [LibraryImport("user32.dll", EntryPoint = "GetMenuItemInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetMenuItemInfoW(nint menu, uint item, [MarshalAs(UnmanagedType.Bool)] bool byPosition, MenuItemInfo* info);

    [LibraryImport("user32.dll", EntryPoint = "GetMenuStringW")]
    private static partial int GetMenuStringW(nint menu, uint item, char* text, int max, uint flags);

    [LibraryImport("user32.dll", EntryPoint = "InsertMenuW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool InsertMenuW(nint menu, uint position, uint flags, nuint id, string? text);

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
