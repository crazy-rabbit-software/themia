using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.Devices;
using Themia.Sdk.Items;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-16630]..[S-16637] [S-16940]..[S-16956] The item base: a drive mapped onto its item (its figures, or no reading and why), the
/// items' one size in every arrangement, how a list of them is laid (down, across, in a grid, in a table under its titles),
/// touching or with the block gap, scrolled to by whole lines, the last cut by the card's edge; the table's aligned columns
/// and the parts' order.
/// </summary>
public sealed class ItemBaseTests
{
    private const ulong Gb = 1024UL * 1024 * 1024;

    private static readonly ItemGrid Grid = ItemGrid.Default();

    /// <summary>A device item with its bar under the row: the base's measures of a bar under (the meetings', the forecast's).</summary>
    private static readonly ItemShape BarUnder = ItemShape.Device with { BarAt = ItemBarAt.Under, Even = false };

    private static ItemLay Lay(ItemStyle style, int count, double width, double height, int gap = 0, ItemShape? shape = null, ItemCells? cells = null, int first = 0, bool titles = false)
        => ItemLayout.Lay(style, count, width, height, gap, shape ?? BarUnder, cells ?? ItemCells.Base, Grid, first, titles);

    [Fact]
    public void S16632_a_drive_is_an_item_with_its_name_share_line_and_bar()
    {
        var item = DeviceItems.Drive(Locale.En, new DriveEntry("C:\\", VolumeKind.Fixed, DriveState.Ready, "System", "NTFS", 931 * Gb, 419 * Gb));

        Assert.Equal(("C:\\", "System (C:)", "55", "%", "419 GB free of 931 GB"), (item.Id, item.Title, item.End!.Value, item.End.Unit, item.Subtitle));
        Assert.False(item.Absent);
        Assert.Equal(ColourRole.Accent, item.Bar!.Role);
        Assert.InRange(item.Bar.To, 0.54, 0.56);
        Assert.Equal("C:\\", item.Lead!.Drive!.Root);
    }

    [Fact]
    public void S16670_S16632_a_drive_without_figures_has_no_percentage_and_no_bar_and_says_why()
    {
        // Owner, 2026-11-23: "if elements are not shown like the battery percentage for example, it should also not show
        // them in the design, not just grey them out."
        var stick = DeviceItems.Drive(Locale.En, new DriveEntry("E:\\", VolumeKind.Removable, DriveState.NoMedia));
        Assert.True(stick.Absent);
        Assert.Equal(((ItemEnd?)null, (ItemBar?)null), (stick.End, stick.Bar));
        Assert.False(stick.Has(ItemPart.Bar));
    }

    [Fact]
    public void S16631_a_network_drive_not_connected_keeps_its_empty_bar()
    {
        // Owner, 2026-11-23: "the network drives should show the progress bar even if they are not connected. otherwise
        // they have empty looking space."
        var share = DeviceItems.Drive(Locale.En, new DriveEntry("Z:\\", VolumeKind.Network, DriveState.Disconnected, Share: "\\\\nas\\media"));
        Assert.True(share.Absent);
        Assert.Equal(((ItemEnd?)null, "Disconnected network drive", 0.0), (share.End, share.Subtitle, share.Bar!.To));

        var pending = DeviceItems.Drive(Locale.En, new DriveEntry("Y:\\", VolumeKind.Network, DriveState.Pending));
        Assert.Equal(((ItemEnd?)null, "Connecting…", 0.0), (pending.End, pending.Subtitle, pending.Bar!.To));
    }

    [Fact]
    public void S16632_a_full_drive_is_critical()
    {
        var full = DeviceItems.Drive(Locale.En, new DriveEntry("D:\\", VolumeKind.Fixed, DriveState.Ready, "Games", "NTFS", 100 * Gb, 5 * Gb));
        Assert.Equal(ColourRole.Critical, full.Bar!.Role);
    }

    [Fact]
    public void S16632_a_battery_is_critical_at_its_low_and_never_cautious_for_being_full()
    {
        Assert.Equal(ColourRole.Critical, DeviceItems.BatteryRole(20, 20));
        Assert.Equal(ColourRole.Accent, DeviceItems.BatteryRole(21, 20));
        Assert.Equal(ColourRole.Accent, DeviceItems.BatteryRole(100, 20));
    }

    [Fact]
    public void S16633_a_list_item_is_its_row_and_its_bar_with_the_inset_under_it_in_whole_cells()
    {
        // Owner, 2026-11-23: "there is no proper spacing between the bottom border and the progress bar".
        Assert.Equal(12 + 36 + 12 + 4 + 12, ItemLayout.ListNatural(BarUnder));
        Assert.Equal((240.0, 80.0), ItemLayout.Size(ItemStyle.List, BarUnder, ItemCells.Base, Grid));
    }

    [Fact]
    public void S16946_a_part_left_out_takes_no_room_the_items_close_up()
    {
        // No bar: the inset, the lead's line, the inset.
        var noBar = ItemShape.Of(DeviceItems.Parts, [ItemPart.Bar], reading: true);
        Assert.False(noBar.Bar);
        Assert.Equal(60, ItemLayout.Size(ItemStyle.List, noBar, ItemCells.Base, Grid).Height);

        // Neither a lead nor a reading: the title over its subtitle; an action's button never under its 28.
        var mail = ItemShape.Of([ItemPart.Title, ItemPart.Subtitle, ItemPart.End], []);
        Assert.Equal(12 + 20 + 16 + 12, ItemLayout.ListNatural(mail));
        var meeting = ItemShape.Of([ItemPart.Title, ItemPart.End], [], action: true);
        Assert.Equal(12 + 28 + 12, ItemLayout.ListNatural(meeting));

        // A compact row is the family's row of one or two lines, the bar's 8 under it.
        Assert.Equal(30, ItemLayout.Size(ItemStyle.ColumnList, ItemShape.Of([ItemPart.Title], []), ItemCells.Base, Grid).Height);
        Assert.Equal(40, ItemLayout.Size(ItemStyle.ColumnList, ItemShape.Of([ItemPart.Title, ItemPart.Subtitle], []), ItemCells.Base, Grid).Height);
        Assert.Equal(50, ItemLayout.Size(ItemStyle.ColumnList, BarUnder, ItemCells.Base, Grid).Height);
    }

    [Fact]
    public void S16633_items_stand_down_from_the_top_edge_touching_the_last_cut()
    {
        var lay = Lay(ItemStyle.List, 5, 300, 200);

        Assert.False(lay.Across);
        Assert.Equal(new ItemPlace[] { new(0, 0, 0, 300, 80), new(1, 0, 80, 300, 80), new(2, 0, 160, 300, 80) }, lay.Boxes);
    }

    [Fact]
    public void S16673_the_gap_is_whole_grid_blocks_from_none_to_four()
    {
        Assert.Equal(new[] { 0.0, 90.0, 180.0 }, Lay(ItemStyle.List, 3, 300, 400, gap: 1).Boxes.Select(s => s.Y));
        Assert.Equal(new[] { 0.0, 110.0, 220.0 }, Lay(ItemStyle.List, 3, 300, 400, gap: 3).Boxes.Select(s => s.Y));
        Assert.All(Lay(ItemStyle.List, 3, 300, 400, gap: 2).Boxes, s => Assert.Equal(80, s.Height));
        Assert.Equal(40, ItemLayout.GapOf(9, Grid));
        Assert.Equal(0, ItemLayout.GapOf(-1, Grid));

        var notes = new List<string>();
        Assert.Equal((4, 0), (ItemOrder.Repaired(7, "drives", notes), ItemOrder.Repaired(-2, "drives", notes)));
        Assert.Equal(2, notes.Count);
    }

    [Fact]
    public void S16633_a_card_one_item_tall_and_wide_lays_list_items_across_the_last_cut_by_its_right_edge()
    {
        var lay = Lay(ItemStyle.List, 3, 600, 100);
        Assert.True(lay.Across);
        Assert.Equal(new ItemPlace[] { new(0, 0, 0, 240, 80), new(1, 240, 0, 240, 80), new(2, 480, 0, 240, 80) }, lay.Boxes);
        Assert.Equal(new[] { 0.0, 250.0, 500.0 }, Lay(ItemStyle.List, 3, 600, 100, gap: 1).Boxes.Select(s => s.X));

        // One item, or a card too narrow for two, or tall enough for two: down.
        Assert.False(Lay(ItemStyle.List, 1, 600, 100).Across);
        Assert.False(Lay(ItemStyle.List, 3, 470, 100).Across);
        Assert.False(Lay(ItemStyle.List, 3, 600, 170).Across);
    }

    [Fact]
    public void S16633_a_list_shows_one_item_at_least_and_none_past_its_edge()
    {
        Assert.Single(Lay(ItemStyle.List, 4, 300, 40).Boxes);
        Assert.Empty(Lay(ItemStyle.List, 0, 300, 400).Boxes);
        Assert.Equal(2, Lay(ItemStyle.List, 4, 300, 160).Boxes.Count);
    }

    [Fact]
    public void S16942_a_row_list_stands_its_columns_side_by_side_from_the_left_edge_the_card_tall()
    {
        var cells = new ItemCells(AcrossWidth: 56);
        var lay = Lay(ItemStyle.RowList, 14, 200, 190, cells: cells);
        Assert.True(lay.Across);
        Assert.Equal(new[] { 0.0, 60.0, 120.0, 180.0 }, lay.Boxes.Select(b => b.X));
        Assert.All(lay.Boxes, b => Assert.Equal((60.0, 190.0), (b.Width, b.Height)));

        // The forecast's column: the day, the 32 sky, the high, the standing bar, the low and the rain, 4 apart, the insets.
        var day = ItemShape.Of([ItemPart.Lead, ItemPart.Title, ItemPart.Subtitle, ItemPart.End, ItemPart.Bar, ItemPart.Note], []);
        Assert.Equal(184, ItemLayout.ColumnNatural(day));
    }

    [Fact]
    public void S16944_a_grid_holds_the_whole_columns_its_width_does_the_room_past_them_empty()
    {
        var cells = new ItemCells(CellWidth: 80, CellHeight: 100);
        var lay = Lay(ItemStyle.Grid, 7, 250, 250, cells: cells);
        Assert.Equal((3, 3), (lay.Columns, lay.Lines));
        Assert.Equal(new[] { 0.0, 80.0, 160.0, 0.0, 80.0, 160.0, 0.0 }, lay.Boxes.Select(b => b.X));
        Assert.Equal(new[] { 0.0, 0.0, 0.0, 100.0, 100.0, 100.0, 200.0 }, lay.Boxes.Select(b => b.Y));

        // The gap between the cards both ways; fewer columns where it takes their room.
        var gapped = Lay(ItemStyle.Grid, 4, 250, 250, gap: 1, cells: cells);
        Assert.Equal(2, gapped.Columns);
        Assert.Equal(new[] { 0.0, 90.0, 0.0, 90.0 }, gapped.Boxes.Select(b => b.X));
        Assert.Equal(new[] { 0.0, 0.0, 110.0, 110.0 }, gapped.Boxes.Select(b => b.Y));
    }

    [Fact]
    public void S16945_a_tables_rows_stand_under_its_column_titles_and_take_no_gap()
    {
        var shape = ItemShape.Of([ItemPart.Title, ItemPart.End], []);
        var lay = Lay(ItemStyle.Table, 5, 300, 120, gap: 2, shape: shape, titles: true);
        Assert.Equal(30, lay.Header);
        Assert.Equal(0, lay.Gap);
        Assert.Equal(new[] { 30.0, 60.0, 90.0 }, lay.Boxes.Select(b => b.Y));
        Assert.Equal(90, lay.Viewport);
        Assert.False(ItemLayout.TakesGap(ItemStyle.Table));
    }

    [Fact]
    public void S16955_a_scrolled_list_starts_at_its_first_line_whole_and_never_past_its_last()
    {
        var shape = ItemShape.Of([ItemPart.Title], []);

        // Ten rows of 30 in 100: the last first line is the one that stands the last row at the foot.
        var lay = Lay(ItemStyle.ColumnList, 10, 300, 100, shape: shape, first: 3);
        Assert.Equal(3, lay.First);
        Assert.Equal((3, 0.0), (lay.Boxes[0].Index, lay.Boxes[0].Y));
        Assert.Equal(7, lay.LastFirst);
        Assert.Equal(7, Lay(ItemStyle.ColumnList, 10, 300, 100, shape: shape, first: 99).First);
        Assert.Equal(0, Lay(ItemStyle.ColumnList, 3, 300, 100, shape: shape, first: 2).First);

        // A wheel notch is a line; a page the whole lines the card shows.
        Assert.Equal((1, 0), ItemScroll.Wheel(0, 0, -120, 10, 30, 100));
        Assert.Equal(3, ItemScroll.Page(30, 100));
    }

    [Fact]
    public void S16956_a_tables_columns_line_up_the_bar_takes_what_the_row_leaves()
    {
        // The forecast's row: the sky, the day, the range bar, "low high"; the bar starts and ends at one x on every row.
        var columns = ItemTable.Columns([(ItemPart.Lead, 0), (ItemPart.Title, 40), (ItemPart.Bar, 0), (ItemPart.End, 52)], 276);
        Assert.Equal(new[] { ItemPart.Lead, ItemPart.Title, ItemPart.Bar, ItemPart.End }, columns.Select(c => c.Part));
        Assert.Equal((0.0, 16.0), (columns[0].X, columns[0].Width));
        Assert.Equal(24, columns[1].X);
        Assert.Equal(276, columns[^1].Right);
        Assert.Equal(276 - 16 - 8 - 40 - 52 - 24, columns[2].Width);

        // Never under its floor: the title gives way.
        var narrow = ItemTable.Columns([(ItemPart.Title, 200), (ItemPart.Bar, 0), (ItemPart.End, 52)], 260);
        Assert.Equal(ItemTable.BarFloor(260), narrow[1].Width);
        Assert.Equal(260, narrow[^1].Right);
        Assert.True(narrow[0].Width < 200);

        // The widest text gives way first: a short day's name stays whole beside a long sentence.
        var squeezed = ItemTable.Columns([(ItemPart.Title, 40), (ItemPart.Subtitle, 300), (ItemPart.End, 52)], 260);
        Assert.Equal((40.0, 260.0), (squeezed[0].Width, squeezed[^1].Right));

        // No bar: the title takes what is left, so the end stands on the row's end.
        var plain = ItemTable.Columns([(ItemPart.Title, 50), (ItemPart.End, 40)], 300);
        Assert.Equal((248.0, 300.0), (plain[0].Width, plain[1].Right));
    }

    [Fact]
    public void S16950_the_parts_stand_in_the_users_order_and_never_all_left_out()
    {
        IReadOnlyList<ItemPart> offered = [ItemPart.Lead, ItemPart.Title, ItemPart.End, ItemPart.Bar];
        Assert.Equal([ItemPart.End, ItemPart.Lead, ItemPart.Title, ItemPart.Bar], ItemOrder.Parts(offered, [ItemPart.End, ItemPart.Note]));

        var config = new Parts { PartOrder = [ItemPart.Bar, ItemPart.Title], HiddenParts = [ItemPart.Lead] };
        Assert.Equal([ItemPart.Bar, ItemPart.Title, ItemPart.End], ItemOrder.Shown(offered, config));
        Assert.Equal([ItemPart.Title], ItemOrder.Shown(offered, new Parts { HiddenParts = [.. offered] }));

        var notes = new List<string>();
        Assert.Equal([ItemPart.Bar], ItemOrder.Repaired([ItemPart.Bar, ItemPart.Bar, (ItemPart)42], "x", notes));
        Assert.Equal(2, notes.Count);
    }

    [Fact]
    public void S16634_S16954_a_lists_sizing_is_its_items_with_the_gap_between_in_its_arrangement()
    {
        var plain = ItemLayout.Sizing(ItemStyle.List, 3, gap: 0, BarUnder);
        Assert.Equal((220.0, 80.0), (plain.Min.Width, plain.Min.Height));
        Assert.Equal((300.0, 240.0), (plain.Natural.Width, plain.Natural.Height));
        Assert.Equal(280, ItemLayout.Sizing(ItemStyle.List, 3, gap: 2, BarUnder).Natural.Height);

        var row = ItemLayout.Sizing(ItemStyle.RowList, 3, gap: 0, BarUnder);
        Assert.Equal(row.Min.Height, row.Max.Height);
        Assert.Equal(3 * 140, row.Natural.Width);

        var grid = ItemLayout.Sizing(ItemStyle.Grid, 6, gap: 0, BarUnder);
        Assert.Equal(3 * 140, grid.Natural.Width);

        var table = ItemLayout.Sizing(ItemStyle.Table, 4, gap: 3, ItemShape.Of([ItemPart.Title], []), titles: true);
        Assert.Equal(30 + (4 * 30), table.Natural.Height);
    }

    [Fact]
    public void S16636_a_list_hides_by_id_and_puts_the_users_order_first()
    {
        string[] items = ["a", "b", "c", "d"];
        Assert.Equal(["c", "a", "d"], ItemOrder.Arrange(items, d => d, ["B"], ["c", "x"]));

        // An item not there keeps its place after the shown ones.
        Assert.Equal(["d", "a", "x"], ItemOrder.Reordered(["a", "x", "d"], ["d", "a"]));

        var notes = new List<string>();
        Assert.Equal(["a"], ItemOrder.Repaired(["a", "A", " "], "bluetooth", "hidden", notes));
        Assert.Equal(2, notes.Count);
    }

    [Fact]
    public void S16637_a_single_items_face_is_one_item_and_never_narrower_than_its_line()
    {
        var face = ItemLayout.Face(BarUnder, ItemLayout.WidestLine(["419 GB free of 931 GB"]), ItemLayout.ReadingWidth);
        Assert.Equal((80.0, 80.0, 80.0), (face.Min.Height, face.Natural.Height, face.Max.Height));
        Assert.True(face.Min.Width is >= 220 and <= 260, $"{face.Min.Width}");
        Assert.Equal(300, face.Natural.Width);
        Assert.Equal(face.Min.Width, ItemLayout.Face(BarUnder, 400, ItemLayout.ReadingWidth).Min.Width);
    }

    [Fact]
    public void S17180_a_devices_bar_stands_at_its_end_and_the_item_is_its_row_alone()
    {
        // Owner, 2026-11-23: "the devices should actually get their percentage bar reoriented, it should be all the way on the
        // right side as a vertical bar and not horizontal anymore".
        var device = DeviceItems.Shape([]);
        Assert.Equal(ItemShape.Device, device);
        Assert.Equal((true, false, ItemBarAt.Side), (device.Side, device.Under, device.BarAt));

        // No bar row under it: the inset, the 36 line, the inset (60); a compact row its family row; a card its parts alone.
        Assert.Equal(12 + 36 + 12, ItemLayout.ListNatural(device));
        Assert.Equal((240.0, 60.0), ItemLayout.Size(ItemStyle.List, device, ItemCells.Base, Grid));
        Assert.Equal(40, ItemLayout.Size(ItemStyle.ColumnList, device, ItemCells.Base, Grid).Height);
        Assert.Equal(ItemLayout.CellNatural(BarUnder) - 4 - 4, ItemLayout.CellNatural(device));

        // A row list's bar stands in its column wherever the kind puts it.
        Assert.Equal(ItemLayout.ColumnNatural(BarUnder), ItemLayout.ColumnNatural(device));

        // The list and the face are rows of 60; the face leaves its bar its 16 across.
        Assert.Equal(new[] { 0.0, 60.0, 120.0, 180.0 }, Lay(ItemStyle.List, 5, 300, 200, shape: device).Boxes.Select(b => b.Y));
        var face = ItemLayout.Face(device, 400, ItemLayout.ReadingWidth);
        Assert.Equal((60.0, 60.0), (face.Min.Height, face.Max.Height));
        Assert.Equal(ItemLayout.Face(BarUnder, 400, ItemLayout.ReadingWidth).Min.Width + ItemLayout.SideBar, face.Min.Width);
        Assert.Equal(60, DeviceItems.Sizing(new DeviceList()).Min.Height);

        // A bar left out leaves the same row; the meetings and the forecast keep theirs under.
        Assert.False(DeviceItems.Shape([ItemPart.Bar]).Side);
        Assert.Equal(ItemBarAt.Under, ItemShape.Of([ItemPart.Title, ItemPart.End, ItemPart.Bar], [], action: true).BarAt);
    }

    [Fact]
    public void S17181_a_device_lists_columns_are_one_width()
    {
        // Owner, 2026-11-23: "the columns in the device view should also all get the same width."
        Assert.True(DeviceItems.Shape([]).Even);
        var columns = ItemTable.Columns([(ItemPart.Lead, 0), (ItemPart.Title, 120), (ItemPart.Subtitle, 30), (ItemPart.End, 40), (ItemPart.Bar, 0)], 300, even: true);
        Assert.Equal((0.0, 16.0), (columns[0].X, columns[0].Width));
        Assert.Equal(24, columns[1].X);
        Assert.All(columns.Skip(1).SkipLast(1), c => Assert.Equal(60, c.Width));
        Assert.Equal(300, columns[^1].Right);
        Assert.InRange(columns[^1].Width, 60, 63);

        // Without the lead every column shares the row; one column takes it whole.
        var plain = ItemTable.Columns([(ItemPart.Title, 10), (ItemPart.End, 200)], 212, even: true);
        Assert.Equal((100.0, 112.0, 212.0), (plain[0].Width, plain[1].X, plain[1].Right));

        // A List's end column: the widest end of its items, whole DIPs; none drawn, none held.
        Assert.Equal(57, ItemLayout.EvenEnd([40, 56.2, 12]));
        Assert.Equal(0, ItemLayout.EvenEnd([]));
    }

    private sealed class DeviceList : IItemList
    {
        public ItemStyle Arrangement => ItemStyle.List;

        public int Gap { get; set; }

        public bool ColumnTitles { get; set; }

        public int Rows { get; set; } = 3;

        public int Lines { get; set; } = 1;

        public List<ItemPart> HiddenParts { get; set; } = [];

        public List<ItemPart> PartOrder { get; set; } = [];
    }

    private sealed class Parts : IItemParts
    {
        public List<ItemPart> HiddenParts { get; set; } = [];

        public List<ItemPart> PartOrder { get; set; } = [];
    }
}
