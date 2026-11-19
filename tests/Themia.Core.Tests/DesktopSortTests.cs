using Themia.Core.Desktop;
using Themia.Core.Model;
using Themia.Core.Geometry;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-5555] [S-5556] The desktop's Sort by: the order per criterion and the cells it takes.</summary>
public sealed class DesktopSortTests
{
    private static readonly DesktopItem ThisPc = new("::{20D04FE0-3AEA-1069-A2D8-08002B30309D}", "This PC", DesktopItemKind.ShellItem);
    private static readonly DesktopItem Bin = new("::{645FF040-5081-101B-9F08-00AA002F954E}", "Recycle Bin", DesktopItemKind.ShellItem);
    private static readonly DesktopItem Work = new(@"C:\d\Work", "Work", DesktopItemKind.Folder);
    private static readonly DesktopItem Archive = new(@"C:\d\Archive", "Archive", DesktopItemKind.Folder);
    private static readonly DesktopItem Notes = new(@"C:\d\notes.txt", "notes.txt", DesktopItemKind.File);
    private static readonly DesktopItem Budget = new(@"C:\d\budget.xlsx", "budget.xlsx", DesktopItemKind.File);
    private static readonly DesktopItem Photo = new(@"C:\d\photo.jpg", "photo.jpg", DesktopItemKind.File);
    private static readonly DesktopItem Link = new(@"C:\d\Editor.lnk", "Editor", DesktopItemKind.Shortcut);

    private static readonly DesktopItem[] All = [Photo, Work, Link, Bin, Notes, Archive, ThisPc, Budget];

    private static readonly Dictionary<string, DesktopSortFacts> Facts = new()
    {
        [Work.Key] = new(0, string.Empty, new DateTime(2026, 11, 1)),
        [Archive.Key] = new(0, string.Empty, new DateTime(2026, 11, 9)),
        [Notes.Key] = new(300, ".txt", new DateTime(2026, 11, 12)),
        [Budget.Key] = new(9000, ".xlsx", new DateTime(2026, 10, 2)),
        [Photo.Key] = new(300, ".jpg", new DateTime(2026, 11, 13)),
        [Link.Key] = new(2, ".lnk", new DateTime(2026, 9, 1)),
    };

    private static DesktopSortFacts Read(DesktopItem item) => Facts.TryGetValue(item.Key, out var facts) ? facts : DesktopSortFacts.None;

    private static string[] Names(DesktopSortBy by) => [.. DesktopSort.Order(All, by, Read).Select(item => item.Name)];

    [Fact]
    public void S5555_by_name_is_the_order_new_items_are_laid_out_in()
        => Assert.Equal(DesktopItemOrder.Sort(All), DesktopSort.Order(All, DesktopSortBy.Name, Read));

    [Fact]
    public void S5555_by_size_keeps_shell_items_and_folders_first_then_files_smallest_first_ties_by_name()
        => Assert.Equal(["Recycle Bin", "This PC", "Archive", "Work", "Editor", "notes.txt", "photo.jpg", "budget.xlsx"], Names(DesktopSortBy.Size));

    [Fact]
    public void S5555_by_item_type_orders_files_by_extension_then_name()
        => Assert.Equal(["Recycle Bin", "This PC", "Archive", "Work", "photo.jpg", "Editor", "notes.txt", "budget.xlsx"], Names(DesktopSortBy.ItemType));

    [Fact]
    public void S5555_by_date_modified_is_newest_first_inside_folders_and_inside_files()
        => Assert.Equal(["Recycle Bin", "This PC", "Archive", "Work", "photo.jpg", "notes.txt", "budget.xlsx", "Editor"], Names(DesktopSortBy.DateModified));

    [Theory]
    [InlineData(DesktopSortBy.Name)]
    [InlineData(DesktopSortBy.Size)]
    [InlineData(DesktopSortBy.ItemType)]
    [InlineData(DesktopSortBy.DateModified)]
    public void S5555_sorting_again_and_from_any_start_gives_the_same_order(DesktopSortBy by)
    {
        var once = DesktopSort.Order(All, by, Read);

        Assert.Equal(once, DesktopSort.Order(once, by, Read));
        Assert.Equal(once, DesktopSort.Order(Enumerable.Reverse(All), by, Read));
    }

    [Theory]
    [InlineData(DesktopSortBy.Name)]
    [InlineData(DesktopSortBy.Size)]
    [InlineData(DesktopSortBy.ItemType)]
    [InlineData(DesktopSortBy.DateModified)]
    public void S5555_an_own_icon_sorts_with_the_files_after_the_folders(DesktopSortBy by)
    {
        // An own icon is handed to the sort as a file under its key and name, with no facts unless its target has some.
        var own = new DesktopItem(DesktopItemKeys.Own("abc"), "Aardvark", DesktopItemKind.File);
        var order = DesktopSort.Order([own, .. All], by, Read);

        var at = order.ToList().IndexOf(own);
        Assert.True(at > order.ToList().IndexOf(Work));
        Assert.True(at > order.ToList().IndexOf(Archive));
        Assert.True(at > order.ToList().IndexOf(Bin));
        Assert.Equal(All.Length + 1, order.Count);
    }

    [Fact]
    public void S5555_an_own_icon_takes_its_place_among_the_files_by_name()
    {
        var own = new DesktopItem(DesktopItemKeys.Own("abc"), "Mission Planner", DesktopItemKind.File);

        Assert.Equal(
            ["Recycle Bin", "This PC", "Archive", "Work", "budget.xlsx", "Editor", "Mission Planner", "notes.txt", "photo.jpg"],
            DesktopSort.Order([own, .. All], DesktopSortBy.Name, Read).Select(item => item.Name));

        // No facts (a web address, a layout): size 0 and no type put it first of the files, the oldest date last.
        Assert.Equal("Mission Planner", DesktopSort.Order([own, .. All], DesktopSortBy.Size, Read)[4].Name);
        Assert.Equal("Mission Planner", DesktopSort.Order([own, .. All], DesktopSortBy.ItemType, Read)[4].Name);
        Assert.Equal("Mission Planner", DesktopSort.Order([own, .. All], DesktopSortBy.DateModified, Read)[^1].Name);
    }

    [Fact]
    public void S5555_a_shell_item_is_never_asked_for_its_facts()
    {
        var asked = new List<string>();
        DesktopSort.Order(All, DesktopSortBy.Size, item =>
        {
            asked.Add(item.Key);
            return Read(item);
        });

        Assert.DoesNotContain(ThisPc.Key, asked);
        Assert.DoesNotContain(Bin.Key, asked);
    }





    /// <summary>The lines the background menu opens with: its first group, up to the first separator.</summary>
    private static List<DesktopMenuEntry> Top()
        => [.. DesktopBackground.Menu(new DesktopBackgroundFacts(ClipboardHasFiles: false, HasTerminal: false)).TakeWhile(entry => !entry.IsSeparator)];

    [Fact]
    public void S5554_the_menu_opens_with_view_sort_by_and_refresh_and_every_sort_line_has_its_criterion()
    {
        var top = Top();

        Assert.Equal([DesktopMenuGroup.View, DesktopMenuGroup.SortBy, null], top.Select(entry => entry.Group));
        var sort = top[1];
        Assert.Null(sort.Command);
        Assert.Equal([DesktopSortBy.Name, DesktopSortBy.Size, DesktopSortBy.ItemType, DesktopSortBy.DateModified], sort.Children.Select(c => DesktopBackground.SortOf(c.Command!.Value)!.Value));
        Assert.All(sort.Children, child => Assert.False(child.Checked));

        // [S-5596] One of a set: the last sort (Name in a new state) carries the dot.
        Assert.Equal([true, false, false, false], sort.Children.Select(c => c.Chosen));
        Assert.Equal(DesktopMenuCommand.Refresh, top[^1].Command);
        Assert.Null(DesktopBackground.SortOf(DesktopMenuCommand.Refresh));
        Assert.Null(DesktopBackground.SortOf(DesktopMenuCommand.EditMode));
        Assert.Null(DesktopBackground.SortOf(DesktopMenuCommand.AutoArrangeIcons));
    }

    [Fact]
    public void S5594_view_holds_the_sizes_then_auto_arrange_a_one_shot_and_no_show_or_align_line()
    {
        var view = Top()[0];

        // Changed 2026-11-19 ([S-13970], owner): the icon layouts stand between the sizes and Auto arrange.
        Assert.Equal(DesktopMenuGroup.View, view.Group);
        Assert.Null(view.Command);
        Assert.Equal(
            [DesktopMenuCommand.LargeIcons, DesktopMenuCommand.MediumIcons, DesktopMenuCommand.SmallIcons, null,
             DesktopMenuCommand.TextBelowIcons, DesktopMenuCommand.TextBesideIcons, DesktopMenuCommand.IconsOnly, null,
             DesktopMenuCommand.AutoArrangeIcons],
            view.Children.Select(c => c.Command));

        // A new state: medium icons carry the dot. Auto arrange acts once: no check mark, no dot.
        Assert.Equal([false, true, false], view.Children.Take(3).Select(c => c.Chosen));
        var arrange = view.Children[^1];
        Assert.False(arrange.Checked);
        Assert.Null(arrange.Chosen);
        Assert.Empty(arrange.Children);
        Assert.DoesNotContain("ShowDesktopIcons", Enum.GetNames<DesktopMenuCommand>());
        Assert.DoesNotContain("AlignIconsToGrid", Enum.GetNames<DesktopMenuCommand>());
    }

    [Theory]
    [InlineData(DesktopIconSize.Large, DesktopMenuCommand.LargeIcons)]
    [InlineData(DesktopIconSize.Medium, DesktopMenuCommand.MediumIcons)]
    [InlineData(DesktopIconSize.Small, DesktopMenuCommand.SmallIcons)]
    public void S5593_the_size_in_force_alone_carries_the_dot_and_each_size_line_sets_its_size(DesktopIconSize size, DesktopMenuCommand line)
    {
        var view = DesktopBackground.Menu(new DesktopBackgroundFacts(false, false, IconSize: size))[0];

        var chosen = Assert.Single(view.Children.Take(3), c => c.Chosen == true);
        Assert.Equal(line, chosen.Command);
        Assert.Equal(size, DesktopBackground.SizeOf(line));
        Assert.Null(DesktopBackground.SizeOf(DesktopMenuCommand.AutoArrangeIcons));
        Assert.Null(DesktopBackground.SortOf(line));
    }

    [Theory]
    [InlineData(DesktopSortBy.Name, DesktopMenuCommand.SortByName)]
    [InlineData(DesktopSortBy.Size, DesktopMenuCommand.SortBySize)]
    [InlineData(DesktopSortBy.ItemType, DesktopMenuCommand.SortByItemType)]
    [InlineData(DesktopSortBy.DateModified, DesktopMenuCommand.SortByDateModified)]
    public void S5596_the_last_sort_carries_the_dot(DesktopSortBy by, DesktopMenuCommand line)
    {
        var sort = DesktopBackground.Menu(new DesktopBackgroundFacts(false, false, Sort: by))[1];

        Assert.Equal(line, Assert.Single(sort.Children, c => c.Chosen == true).Command);
    }

    [Fact]
    public void S5596_the_menu_reads_the_icon_size_and_the_sort_from_the_state()
    {
        var state = new AppState();
        state.Preferences.DesktopIconSize = DesktopIconSize.Small;
        state.Desktop.Sort = DesktopSortBy.DateModified;

        var facts = DesktopBackgroundFacts.Of(state, clipboardHasFiles: true, hasTerminal: false);

        Assert.Equal(new DesktopBackgroundFacts(true, false, DesktopIconSize.Small, DesktopSortBy.DateModified), facts);
    }
}