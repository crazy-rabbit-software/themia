using Themia.Core.Geometry;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// The edit bar's add menu as a tree ([S-3510..S-3515]): every kind at one home, the folders'
/// shapes, what the catalogue's kinds resolve to, and the keys and the pointer through the cascade.
/// Here, not in Core's tests, because the menu is built from every widget of the build, the module
/// projects' included, and only this host has them all.
/// </summary>
public sealed class AddMenuTests
{
    private static readonly WidgetId[] Every = ModuleCatalog.Widgets.Select(w => w.Id).ToArray();

    private static IReadOnlyList<AddColumn> Menu => AddTree.On(Every);

    private static IEnumerable<AddNode> Descend(AddShelf shelf)
    {
        foreach (var row in shelf.Rows)
        {
            yield return row;
            if (row is AddShelf inner)
            {
                foreach (var deeper in Descend(inner))
                {
                    yield return deeper;
                }
            }
        }
    }

    private static IEnumerable<AddShelf> Shelves() =>
        AddTree.Top.Concat(AddTree.Top.SelectMany(Descend).OfType<AddShelf>());

    [Fact]
    public void S3511_every_kind_has_exactly_one_home_in_the_tree()
    {
        var homes = AddTree.Top.SelectMany(Descend).OfType<AddLeaf>().Where(leaf => !leaf.Again).Select(leaf => leaf.Kind).ToList();

        var missing = Every.Except(homes).ToList();
        var twice = homes.GroupBy(kind => kind).Where(group => group.Count() > 1).Select(group => group.Key).ToList();

        Assert.True(missing.Count == 0, "kinds with no home in AddTree (give each one in its subject): " + string.Join(", ", missing));
        Assert.True(twice.Count == 0, "kinds at home twice (mark the second Again): " + string.Join(", ", twice));
    }

    [Fact]
    public void S3511_a_row_shown_again_has_its_home_elsewhere_and_only_popular_shows_rows_again()
    {
        var homes = AddTree.Top.SelectMany(Descend).OfType<AddLeaf>().Where(leaf => !leaf.Again).Select(leaf => leaf.Kind).ToHashSet();

        foreach (var shelf in Shelves())
        {
            foreach (var leaf in shelf.Rows.OfType<AddLeaf>().Where(leaf => leaf.Again))
            {
                Assert.Equal(AddFolderId.Popular, shelf.Id);
                Assert.Contains(leaf.Kind, homes);
            }
        }

        Assert.All(AddTree.Popular.Rows, row => Assert.True(row is AddLeaf { Again: true }, "Popular holds widgets shown again, nothing else"));
    }

    [Fact]
    public void S3512_folders_are_short_shallow_and_never_one_row()
    {
        foreach (var shelf in Shelves())
        {
            if (shelf.Id != AddFolderId.QuickSettings)
            {
                Assert.True(shelf.Rows.Count <= AddTree.MostRows, $"{shelf.Id} has {shelf.Rows.Count} rows");
            }

            Assert.True(shelf.Rows.Count(row => row is not AddLine) >= 2, $"{shelf.Id} is a folder of one row");
            Assert.False(shelf.Rows[0] is AddLine || shelf.Rows[^1] is AddLine, $"{shelf.Id} starts or ends with a line");
        }

        // Browse's rows are subjects; a subject's folders are families; a family holds widgets only.
        Assert.All(AddTree.Browse.Rows, row => Assert.IsType<AddShelf>(row));
        foreach (var subject in AddTree.Browse.Rows.OfType<AddShelf>())
        {
            foreach (var family in subject.Rows.OfType<AddShelf>())
            {
                Assert.All(family.Rows, row => Assert.IsType<AddLeaf>(row));
            }
        }
    }

    [Fact]
    public void S3510_every_folder_has_its_own_id_and_words()
    {
        var shelves = Shelves().ToList();
        Assert.Equal(shelves.Count, shelves.Select(shelf => shelf.Id).Distinct().Count());
        Assert.All(shelves, shelf => Assert.False(string.IsNullOrWhiteSpace(shelf.Title.Text(Locale.En))));
    }

    [Fact]
    public void S3511_the_whole_catalogue_resolves_to_popular_and_browse_with_nothing_in_more()
    {
        var menu = Menu;

        Assert.Equal([AddFolderId.Popular, AddFolderId.Browse], menu.Select(column => column.Shelf.Id));
        Assert.DoesNotContain(menu[1].Rows, row => row is AddFolder { Shelf.Id: AddFolderId.More });
        Assert.Equal(Every.Length, AddTree.Homes(menu).Count);
        Assert.Equal(Every.Order(), AddTree.Homes(menu).Select(home => home.Kind).Order());
    }

    [Fact]
    public void S3511_a_kind_the_tree_does_not_know_still_stands_in_more()
    {
        WidgetId[] unknown = [new("notBuiltA"), new("notBuiltB")];
        var more = Assert.IsType<AddFolder>(AddTree.On([.. Every, .. unknown])[1].Rows[^1]);
        Assert.Equal(AddFolderId.More, more.Shelf.Id);
        Assert.Equal(unknown, more.Rows.OfType<AddPick>().Select(pick => pick.Kind));

        // A folder of one row is that row: one unknown kind stands at Browse's end itself.
        Assert.Equal(new AddPick(unknown[0], false), AddTree.On([.. Every, unknown[0]])[1].Rows[^1]);
    }

    [Fact]
    public void S3511_a_folder_left_with_one_row_is_that_row_and_with_none_is_gone()
    {
        // Only the processor's meter, and nothing of Weather: the processor family folds to its one
        // row, the subject keeps its other rows, and Weather is not there at all.
        WidgetId[] left = [new WidgetId("cpuDetails"), new WidgetId("topProcesses"),
            new WidgetId("weatherNow"), new WidgetId("weatherForecast"), new WidgetId("rainNextHour"), new WidgetId("airQuality"), new WidgetId("sun"), new WidgetId("moon")];
        var offered = Every.Where(kind => !left.Contains(kind)).ToList();
        var browse = AddTree.On(offered)[1].Rows.OfType<AddFolder>().ToList();

        var system = browse.Single(folder => folder.Shelf.Id == AddFolderId.System);
        Assert.Equal(new AddPick(new WidgetId("cpu"), false), system.Rows[0]);
        Assert.DoesNotContain(browse, folder => folder.Shelf.Id == AddFolderId.Weather);
    }

    [Fact]
    public void S3511_lines_only_ever_stand_between_two_rows()
    {
        // Without the year progress, Time's last line would end the folder.
        var offered = Every.Where(kind => kind.Value != "yearProgress").ToList();
        var time = AddTree.On(offered)[1].Rows.OfType<AddFolder>().Single(folder => folder.Shelf.Id == AddFolderId.Time);

        Assert.IsNotType<AddRule>(time.Rows[^1]);
        for (var i = 1; i < time.Rows.Count; i++)
        {
            Assert.False(time.Rows[i] is AddRule && time.Rows[i - 1] is AddRule);
        }
    }

    [Fact]
    public void S3513_a_member_of_a_family_is_at_home_under_its_subject_and_its_family()
    {
        var homes = AddTree.Homes(Menu);

        var details = homes.Single(home => home.Kind == new WidgetId("cpuDetails"));
        Assert.Equal([AddFolderId.System, AddFolderId.Processor], details.Path.Select(shelf => shelf.Id));

        var clock = homes.Single(home => home.Kind == new WidgetId("clock"));
        Assert.Equal([AddFolderId.Time], clock.Path.Select(shelf => shelf.Id));
    }

    // ---- the keys ---------------------------------------------------------------------------

    private static AddSpot SpotOf(AddFolderId id) =>
        new(1, Menu[1].Rows.Select((row, index) => (row, index)).Single(pair => pair.row is AddFolder folder && folder.Shelf.Id == id).index);

    private static AddMenuStep Press(AddMenuNav nav, WidgetKey key) => AddMenuKeys.Move(Menu, nav, key);

    [Fact]
    public void S3514_down_from_the_box_goes_to_popular_and_up_from_its_first_row_back()
    {
        var down = Press(AddMenuNav.Start, WidgetKey.Down);
        Assert.Equal(new AddSpot(0, 0), down.Nav.Cursor);

        var up = Press(down.Nav, WidgetKey.Up);
        Assert.Null(up.Nav.Cursor);
        Assert.Equal(AddMenuAction.Moved, up.Action);
    }

    [Fact]
    public void S3514_in_the_box_left_right_home_end_and_space_are_the_texts()
    {
        foreach (var key in new[] { WidgetKey.Left, WidgetKey.Right, WidgetKey.Home, WidgetKey.End, WidgetKey.Space, WidgetKey.Enter })
        {
            Assert.Equal(AddMenuAction.Unhandled, Press(AddMenuNav.Start, key).Action);
        }
    }

    [Fact]
    public void S3514_right_goes_from_popular_to_the_nearest_subject()
    {
        var nav = new AddMenuNav([], new AddSpot(0, 2));
        var step = Press(nav, WidgetKey.Right);

        Assert.Equal(new AddSpot(1, 2), step.Nav.Cursor);
        Assert.Empty(step.Nav.Open);
    }

    [Fact]
    public void S3514_right_on_a_subject_opens_it_and_goes_to_its_first_row()
    {
        var subject = SpotOf(AddFolderId.CalendarMail);
        var step = Press(new AddMenuNav([], subject), WidgetKey.Right);

        Assert.Equal([subject], step.Nav.Open);
        Assert.Equal(new AddSpot(2, 0), step.Nav.Cursor);
        Assert.Equal(3, AddMenuKeys.Columns(Menu, step.Nav).Count);
    }

    [Fact]
    public void S3514_enter_on_a_family_opens_a_second_submenu_and_left_walks_back()
    {
        var subject = SpotOf(AddFolderId.System);
        var inSubject = Press(new AddMenuNav([], subject), WidgetKey.Enter).Nav;

        // The processor's family is the subject's first row.
        var inFamily = Press(inSubject, WidgetKey.Enter).Nav;
        Assert.Equal([subject, new AddSpot(2, 0)], inFamily.Open);
        Assert.Equal(new AddSpot(3, 0), inFamily.Cursor);
        Assert.Equal(AddFolderId.Processor, AddMenuKeys.OpenFolders(Menu, inFamily)[^1].Shelf.Id);

        var back = Press(inFamily, WidgetKey.Left).Nav;
        Assert.Equal([subject], back.Open);
        Assert.Equal(new AddSpot(2, 0), back.Cursor);

        var top = Press(back, WidgetKey.Left).Nav;
        Assert.Empty(top.Open);
        Assert.Equal(subject, top.Cursor);
    }

    [Fact]
    public void S3514_escape_closes_the_innermost_submenu_then_the_menu()
    {
        var subject = SpotOf(AddFolderId.Time);
        var open = Press(new AddMenuNav([], subject), WidgetKey.Right).Nav;

        var closed = Press(open, WidgetKey.Escape);
        Assert.Equal(AddMenuAction.Moved, closed.Action);
        Assert.Empty(closed.Nav.Open);
        Assert.Equal(subject, closed.Nav.Cursor);

        Assert.Equal(AddMenuAction.Close, Press(closed.Nav, WidgetKey.Escape).Action);
        Assert.Equal(AddMenuAction.Close, Press(AddMenuNav.Start, WidgetKey.Escape).Action);
    }

    [Fact]
    public void S3514_enter_or_space_on_a_widget_picks_it()
    {
        var subject = SpotOf(AddFolderId.Time);
        var inTime = Press(new AddMenuNav([], subject), WidgetKey.Right).Nav;

        var enter = Press(inTime, WidgetKey.Enter);
        Assert.Equal(AddMenuAction.Pick, enter.Action);
        Assert.Equal(new WidgetId("clock"), enter.Kind);

        var popular = Press(new AddMenuNav([], new AddSpot(0, 0)), WidgetKey.Space);
        Assert.Equal(AddMenuAction.Pick, popular.Action);
        Assert.Equal(new WidgetId("clock"), popular.Kind);
    }

    [Fact]
    public void S3514_up_and_down_skip_lines_and_a_move_in_a_column_closes_what_it_opened()
    {
        // Time: Clock, Alarm, Countdown, Stopwatch, a line, Pomodoro.
        var subject = SpotOf(AddFolderId.Time);
        var nav = Press(new AddMenuNav([], subject), WidgetKey.Right).Nav with { Cursor = new AddSpot(2, 3) };
        Assert.Equal(new AddSpot(2, 5), Press(nav, WidgetKey.Down).Nav.Cursor);

        // Down in Browse with Time open closes it: the cascade hangs off the row the highlight is on.
        var moved = Press(new AddMenuNav([subject], subject), WidgetKey.Down).Nav;
        Assert.Empty(moved.Open);
        Assert.Equal(subject with { Row = subject.Row + 1 }, moved.Cursor);
    }

    [Fact]
    public void S3514_up_from_a_submenus_first_row_stays_in_it()
    {
        var subject = SpotOf(AddFolderId.Time);
        var nav = Press(new AddMenuNav([], subject), WidgetKey.Right).Nav;

        Assert.Equal(new AddSpot(2, 0), Press(nav, WidgetKey.Up).Nav.Cursor);
    }

    [Fact]
    public void S3514_the_pointer_on_a_folder_opens_it_and_on_a_row_beside_closes_it()
    {
        var subject = SpotOf(AddFolderId.Devices);
        var opened = AddMenuKeys.Point(Menu, AddMenuNav.Start, subject);
        Assert.Equal([subject], opened.Open);
        Assert.Equal(subject, opened.Cursor);

        // Into the subject's power buttons, then onto a widget of the subject: the family closes.
        var family = AddMenuKeys.Point(Menu, opened, new AddSpot(2, 1));
        Assert.Equal(2, family.Open.Count);
        var beside = AddMenuKeys.Point(Menu, family, new AddSpot(2, 3));
        Assert.Equal([subject], beside.Open);

        // A row of Popular closes the whole cascade.
        Assert.Empty(AddMenuKeys.Point(Menu, family, new AddSpot(0, 0)).Open);
    }

    [Fact]
    public void S3515_the_results_are_walked_as_a_list()
    {
        Assert.Equal(0, AddMenuKeys.MoveInList(3, -1, WidgetKey.Down));
        Assert.Equal(2, AddMenuKeys.MoveInList(3, 2, WidgetKey.Down));
        Assert.Equal(-1, AddMenuKeys.MoveInList(3, 0, WidgetKey.Up));
        Assert.Equal(2, AddMenuKeys.MoveInList(3, 0, WidgetKey.End));
        Assert.Null(AddMenuKeys.MoveInList(3, -1, WidgetKey.Home));
        Assert.Null(AddMenuKeys.MoveInList(3, 1, WidgetKey.Left));
        Assert.Equal(-1, AddMenuKeys.MoveInList(0, -1, WidgetKey.Down));
    }

    // ---- [S-3512] where a submenu stands ------------------------------------------------------

    private static readonly PixelRect Room = new(0, 0, 1920, 1032);

    [Fact]
    public void S3512_a_submenu_opens_beside_its_row_lapping_the_column_with_its_first_row_level()
    {
        var spot = AddCascade.Beside(columnLeft: 100, columnRight: 340, rowTop: 200, lead: 5, width: 250, height: 300, backward: false, Room);

        Assert.Equal(new PixelRect(340 - AddCascade.Overlap, 195, 250, 300), spot.Box);
        Assert.False(spot.Backward);
    }

    [Fact]
    public void S3512_a_submenu_with_no_room_forward_opens_backward_and_the_cascade_keeps_that_way()
    {
        var first = AddCascade.Beside(1600, 1840, 200, 5, 250, 300, backward: false, Room);
        Assert.True(first.Backward);
        Assert.Equal(1600 + AddCascade.Overlap - 250, first.Box.X);

        // The next level hangs from the first and keeps running backward, though forward would fit.
        var second = AddCascade.Beside(first.Box.X, first.Box.X + 250, 240, 5, 250, 300, first.Backward, Room);
        Assert.True(second.Backward);
        Assert.Equal(first.Box.X + AddCascade.Overlap - 250, second.Box.X);
    }

    [Fact]
    public void S3512_a_backward_cascade_turns_forward_again_at_the_other_edge()
    {
        var spot = AddCascade.Beside(100, 340, 200, 5, 250, 300, backward: true, Room);

        Assert.False(spot.Backward);
        Assert.Equal(340 - AddCascade.Overlap, spot.Box.X);
    }

    [Fact]
    public void S3512_a_submenu_stays_inside_the_work_area()
    {
        // Near the bottom it rises until it fits; taller than the work area it is cut to it.
        var low = AddCascade.Beside(100, 340, 1000, 5, 250, 300, backward: false, Room);
        Assert.Equal(1032 - 300, low.Box.Y);

        var tall = AddCascade.Beside(100, 340, 200, 5, 250, 2000, backward: false, Room);
        Assert.Equal(0, tall.Box.Y);
        Assert.Equal(1032, tall.Box.Height);

        // Neither side fits: the side with more room, pushed in.
        var wide = AddCascade.Beside(800, 1040, 200, 5, 1000, 300, backward: false, new PixelRect(0, 0, 1500, 1032));
        Assert.InRange(wide.Box.X, 0, 1500 - 1000);
    }

    [Fact]
    public void S3511_the_merged_kinds_are_single_rows_and_popular_lists_the_calendar()
    {
        // [S-3640] [S-3216] [S-4123] The calendar, the visualizer and the folder are one widget each,
        // their former variants styles: a row of their subject, never a family folder.
        var homes = AddTree.Homes(Menu);
        Assert.Equal([AddFolderId.CalendarMail], homes.Single(home => home.Kind == CalendarWidgetConfig.Descriptor.Id).Path.Select(shelf => shelf.Id));
        Assert.Equal([AddFolderId.Media], homes.Single(home => home.Kind == AudioVisualizerConfig.Descriptor.Id).Path.Select(shelf => shelf.Id));
        Assert.Equal([AddFolderId.Files], homes.Single(home => home.Kind == FolderConfig.Descriptor.Id).Path.Select(shelf => shelf.Id));

        var popular = AddTree.Popular.Rows.OfType<AddLeaf>().Select(leaf => leaf.Kind).ToList();
        Assert.Contains(CalendarWidgetConfig.Descriptor.Id, popular);
        Assert.Equal(popular.Count, popular.Distinct().Count());
    }
}
