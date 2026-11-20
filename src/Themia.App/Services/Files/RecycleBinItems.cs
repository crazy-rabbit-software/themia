using System.Runtime.InteropServices;
using Themia.Core.Desktop;
using Themia.Core.Diagnostics;
using Themia.App.Interop;

namespace Themia.App.Services.Files;

/// <summary>
/// [S-5597] The Recycle Bin's items, and putting one back as Explorer's Restore does: the bin's own namespace
/// (<c>SHGetKnownFolderItem(FOLDERID_RecycleBinFolder)</c>, its items through <c>IEnumShellItems</c>), each item's
/// original path and deletion time read from the bin's own record beside it (<see cref="RecycleBinInfo"/>), and a
/// restore through the item's own shell menu, its <c>undelete</c> verb. UI thread (STA); every COM object and string is
/// released on every path, and nothing throws out.
/// </summary>
internal static unsafe partial class RecycleBinItems
{
    private const string Scope = "recycle-bin";

    /// <summary>The bin's record is a few hundred bytes; anything far larger is not one.</summary>
    private const int MostInfo = 70_000;

    private const uint SigdnFileSysPath = 0x80058000;

    private static readonly Guid FolderIdRecycleBin = new("B7534046-3ECB-4C18-BE4E-64CD4CB7D6AC");
    private static readonly Guid IidShellItem = new("43826d1e-e718-42ee-bc55-a1e261c37bfe");
    private static readonly Guid IidShellItemArray = new("b63ea76d-1f85-456f-a19c-48159efa858b");
    private static readonly Guid IidEnumShellItems = new("70629033-e363-4a28-a567-0db78006e6d7");
    private static readonly Guid IidContextMenu = new("000214e4-0000-0000-c000-000000000046");
    private static readonly Guid BhidEnumItems = new("94f60519-2850-4924-aa5a-d15e84868039");
    private static readonly Guid BhidSfUiObject = new("3981e225-f559-11d3-8e3a-00c04f6837d5");

    /// <summary>[S-5597] Every item the bin holds that its record names (logged and left out where it cannot be read).</summary>
    public static IReadOnlyList<RecycledItem> List()
    {
        var items = new List<RecycledItem>();
        try
        {
            Walk((path, _) =>
            {
                if (Read(path) is { } item)
                {
                    items.Add(item);
                }

                return false;
            });
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "the Recycle Bin could not be read", ex);
        }

        return items;
    }

    /// <summary>
    /// [S-5597] Put the bin's item <paramref name="id"/> (its recycled copy's path, <see cref="RecycledItem.Id"/>) back
    /// where it was deleted from, as the bin's Restore does; <paramref name="owner"/> owns what the shell shows. Whether
    /// the shell ran it.
    /// </summary>
    public static bool Restore(string id, nint owner)
    {
        ArgumentException.ThrowIfNullOrEmpty(id);
        var ran = false;
        try
        {
            Walk((path, item) =>
            {
                if (!string.Equals(path, id, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                ran = Undelete(item, owner);
                return true;
            });
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "an item could not be put back from the Recycle Bin", ex);
        }

        return ran;
    }

    /// <summary>The bin's record of the recycled copy at <paramref name="path"/>, or null (logged).</summary>
    private static RecycledItem? Read(string path)
    {
        if (RecycleBinInfo.InfoPathOf(path) is not { } info)
        {
            return null;
        }

        try
        {
            var file = new FileInfo(info);
            if (!file.Exists || file.Length > MostInfo)
            {
                return null;
            }

            if (RecycleBinInfo.Parse(File.ReadAllBytes(info)) is not { } record)
            {
                Log.Warn(Scope, "a Recycle Bin record could not be read; its item is left out");
                return null;
            }

            return new RecycledItem(path, record.OriginalPath, record.DeletedAt);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warn(Scope, $"a Recycle Bin record could not be opened ({ex.GetType().Name}); its item is left out");
            return null;
        }
    }

    /// <summary>
    /// Every item of the bin with its recycled copy's path, in turn, to <paramref name="visit"/> (its IShellItem lent for
    /// the call), till it answers true.
    /// </summary>
    private static void Walk(Func<string, nint, bool> visit)
    {
        nint bin;
        var folder = FolderIdRecycleBin;
        var iid = IidShellItem;
        var hr = SHGetKnownFolderItem(&folder, 0, 0, &iid, &bin);
        if (hr < 0 || bin == 0)
        {
            Log.Warn(Scope, $"no Recycle Bin (0x{hr:X8})");
            return;
        }

        using var ownedBin = new ComPtr(bin);
        nint list;
        var bhid = BhidEnumItems;
        var iidEnum = IidEnumShellItems;

        // IShellItem::BindToHandler is slot 3.
        var bind = (delegate* unmanaged[Stdcall]<nint, nint, Guid*, Guid*, nint*, int>)ComApi.Slot(bin, 3);
        hr = bind(bin, 0, &bhid, &iidEnum, &list);
        if (hr < 0 || list == 0)
        {
            Log.Warn(Scope, $"the Recycle Bin's items could not be listed (0x{hr:X8})");
            return;
        }

        using var ownedList = new ComPtr(list);

        // IEnumShellItems::Next is slot 3.
        var next = (delegate* unmanaged[Stdcall]<nint, uint, nint*, uint*, int>)ComApi.Slot(list, 3);
        while (true)
        {
            nint item;
            uint fetched;
            if (next(list, 1, &item, &fetched) != 0 || fetched == 0 || item == 0)
            {
                return;
            }

            using var ownedItem = new ComPtr(item);
            if (PathOf(item) is { } path && visit(path, item))
            {
                return;
            }
        }
    }

    /// <summary>An item's file system path (the recycled copy's), or null.</summary>
    private static string? PathOf(nint item)
    {
        char* text;

        // IShellItem::GetDisplayName is slot 5.
        var name = (delegate* unmanaged[Stdcall]<nint, uint, char**, int>)ComApi.Slot(item, 5);
        if (name(item, SigdnFileSysPath, &text) < 0 || text is null)
        {
            return null;
        }

        try
        {
            return new string(text);
        }
        finally
        {
            ComApi.Free(text);
        }
    }

    /// <summary>The bin's Restore on <paramref name="item"/>: its shell menu's <c>undelete</c>.</summary>
    private static bool Undelete(nint item, nint owner)
    {
        nint array;
        var iidArray = IidShellItemArray;
        var hr = SHCreateShellItemArrayFromShellItem(item, &iidArray, &array);
        if (hr < 0 || array == 0)
        {
            Log.Warn(Scope, $"no item array for a Recycle Bin item (0x{hr:X8})");
            return false;
        }

        using var ownedArray = new ComPtr(array);
        nint menu;
        var bhid = BhidSfUiObject;
        var iidMenu = IidContextMenu;

        // IShellItemArray::BindToHandler is slot 3.
        var bind = (delegate* unmanaged[Stdcall]<nint, nint, Guid*, Guid*, nint*, int>)ComApi.Slot(array, 3);
        hr = bind(array, 0, &bhid, &iidMenu, &menu);
        if (hr < 0 || menu == 0)
        {
            Log.Warn(Scope, $"no shell menu for a Recycle Bin item (0x{hr:X8})");
            return false;
        }

        using var ownedMenu = new ComPtr(menu);
        return ShellContextMenu.InvokeMenuVerb(menu, "undelete", owner, "a Recycle Bin item");
    }

    [LibraryImport("shell32.dll")]
    private static partial int SHGetKnownFolderItem(Guid* folder, uint flags, nint token, Guid* iid, nint* item);

    [LibraryImport("shell32.dll")]
    private static partial int SHCreateShellItemArrayFromShellItem(nint item, Guid* iid, nint* array);

}
