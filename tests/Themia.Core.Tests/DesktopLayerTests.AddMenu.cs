using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-5523] Add widget's "On the desktop": the item kind's row becomes the view's unplaced items.</summary>
public sealed partial class DesktopLayerTests
{
    private static readonly WidgetId ItemKind = new("desktopItem");
    private static readonly WidgetId FolderKind = new("folder");

    private static IReadOnlyList<AddColumn> FilesMenu()
    {
        var files = new AddShelf(AddFolderId.Files, Str.Desktop_Add_Folder.Local(), FolderKind, []);
        var browse = new AddShelf(AddFolderId.Browse, Str.Desktop_Add_Folder.Local(), FolderKind, []);
        return [new AddColumn(browse, [new AddFolder(files, [new AddPick(FolderKind, false), new AddRule(), new AddPick(ItemKind, false)])])];
    }

    [Fact]
    public void S5523_the_item_kinds_row_is_a_folder_of_the_unplaced_items()
    {
        DesktopItem[] unplaced = [new("::{645FF040-5081-101B-9F08-00AA002F954E}", "Recycle Bin", DesktopItemKind.ShellItem), File("a.txt")];

        var menu = AddTree.WithDesktopItems(FilesMenu(), ItemKind, unplaced, Str.Desktop_Add_Folder.Local());

        var files = Assert.IsType<AddFolder>(Assert.Single(Assert.Single(menu).Rows));
        Assert.IsType<AddPick>(files.Rows[0]);
        var desktop = Assert.IsType<AddFolder>(files.Rows[^1]);
        Assert.Equal(AddFolderId.DesktopItems, desktop.Shelf.Id);
        Assert.Equal(unplaced, desktop.Rows.Cast<AddDesktopItem>().Select(r => r.Item));

        // The kind is never a pick of its own, so search never lists it.
        Assert.DoesNotContain(AddTree.Homes(menu), home => home.Kind == ItemKind);
    }

    [Fact]
    public void S5523_with_nothing_unplaced_the_row_goes_and_its_folder_folds()
    {
        var menu = AddTree.WithDesktopItems(FilesMenu(), ItemKind, [], Str.Desktop_Add_Folder.Local());

        // Files kept one row: it is that row, as a folder of one is ([S-3511]).
        var only = Assert.IsType<AddPick>(Assert.Single(Assert.Single(menu).Rows));
        Assert.Equal(FolderKind, only.Kind);
    }

    [Fact]
    public void S5523_enter_on_an_item_row_picks_that_item()
    {
        var item = File("a.txt");
        var menu = AddTree.WithDesktopItems(FilesMenu(), ItemKind, [item], Str.Desktop_Add_Folder.Local());

        // Files is column 0's row 0, its "On the desktop" row 2 in it (after the line), the item row 0 in that.
        var nav = new AddMenuNav([new AddSpot(0, 0), new AddSpot(1, 2)], new AddSpot(2, 0));
        var step = AddMenuKeys.Move(menu, nav, WidgetKey.Enter);

        Assert.Equal(AddMenuAction.Pick, step.Action);
        Assert.Equal(item, step.Item);
    }
}
