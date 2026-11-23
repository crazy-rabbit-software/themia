using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.Devices;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-16630]..[S-16634] The device family's one item: a drive mapped onto it (its figures, or "--" and why), its one size,
/// and how a list of them is laid down or across, touching or with the block gap, the last cut by the card's edge.
/// </summary>
public sealed class DeviceItemTests
{
    private const ulong Gb = 1024UL * 1024 * 1024;

    private static readonly ItemGrid Grid = ItemGrid.Default();

    [Fact]
    public void S16632_a_drive_is_an_item_with_its_name_share_line_and_bar()
    {
        var item = DeviceItems.Drive(Locale.En, new DriveEntry("C:\\", VolumeKind.Fixed, DriveState.Ready, "System", "NTFS", 931 * Gb, 419 * Gb));

        Assert.Equal(("C:\\", "System (C:)", "55", "%", "419 GB free of 931 GB"), (item.Id, item.Name, item.Reading!.Value.Value, item.Reading.Value.Unit, item.Line));
        Assert.False(item.Absent);
        Assert.Equal(ColourRole.Accent, item.FillRole);
        Assert.InRange(item.Fill!.Value, 0.54, 0.56);
        Assert.Equal("C:\\", item.Icon.Drive!.Root);
    }

    [Fact]
    public void S16670_S16632_a_drive_without_figures_has_no_percentage_and_no_bar_and_says_why()
    {
        // Owner, 2026-11-23: "if elements are not shown like the battery percentage for example, it should also not show
        // them in the design, not just grey them out."
        var share = DeviceItems.Drive(Locale.En, new DriveEntry("Z:\\", VolumeKind.Network, DriveState.Disconnected, Share: "\\\\nas\\media"));
        Assert.True(share.Absent);
        Assert.Equal(((DeviceReading?)null, "Disconnected network drive", (double?)null), (share.Reading, share.Line, share.Fill));

        var pending = DeviceItems.Drive(Locale.En, new DriveEntry("Y:\\", VolumeKind.Network, DriveState.Pending));
        Assert.Equal(((DeviceReading?)null, "Connecting…"), (pending.Reading, pending.Line));

        var full = DeviceItems.Drive(Locale.En, new DriveEntry("D:\\", VolumeKind.Fixed, DriveState.Ready, "Games", "NTFS", 100 * Gb, 5 * Gb));
        Assert.Equal(ColourRole.Critical, full.FillRole);
    }

    [Fact]
    public void S16632_a_battery_is_critical_at_its_low_and_never_cautious_for_being_full()
    {
        Assert.Equal(ColourRole.Critical, DeviceItems.BatteryRole(20, 20));
        Assert.Equal(ColourRole.Accent, DeviceItems.BatteryRole(21, 20));
        Assert.Equal(ColourRole.Accent, DeviceItems.BatteryRole(100, 20));
    }

    [Fact]
    public void S16633_an_item_is_its_row_and_its_bar_with_the_inset_under_it_in_whole_cells()
    {
        // Owner, 2026-11-23: "there is no proper spacing between the bottom border and the progress bar".
        Assert.Equal(12 + 36 + 12 + 4 + 12, DeviceLayout.Natural);
        Assert.Equal(80, DeviceLayout.Height(Grid));
        Assert.Equal(240, DeviceLayout.Width(Grid));
    }

    [Fact]
    public void S16633_items_stand_down_from_the_top_edge_touching_the_last_cut()
    {
        var lay = DeviceLayout.Lay(5, 300, 200, gap: 0, Grid);

        Assert.False(lay.Across);
        Assert.Equal(new DeviceSlot[] { new(0, 0, 300, 80), new(0, 80, 300, 80), new(0, 160, 300, 80) }, lay.Slots);
    }

    [Fact]
    public void S16673_the_gap_is_whole_grid_blocks_from_none_to_four()
    {
        Assert.Equal(new[] { 0.0, 90.0, 180.0 }, DeviceLayout.Lay(3, 300, 400, gap: 1, Grid).Slots.Select(s => s.Y));
        Assert.Equal(new[] { 0.0, 110.0, 220.0 }, DeviceLayout.Lay(3, 300, 400, gap: 3, Grid).Slots.Select(s => s.Y));
        Assert.All(DeviceLayout.Lay(3, 300, 400, gap: 2, Grid).Slots, s => Assert.Equal(80, s.Height));
        Assert.Equal(40, DeviceLayout.GapOf(9, Grid));
        Assert.Equal(0, DeviceLayout.GapOf(-1, Grid));

        var notes = new List<string>();
        Assert.Equal((4, 0), (DeviceOrder.Repaired(7, "drives", notes), DeviceOrder.Repaired(-2, "drives", notes)));
        Assert.Equal(2, notes.Count);
    }

    [Fact]
    public void S16633_a_card_one_item_tall_and_wide_lays_them_across_the_last_cut_by_its_right_edge()
    {
        var lay = DeviceLayout.Lay(3, 600, 100, gap: 0, Grid);
        Assert.True(lay.Across);
        Assert.Equal(new DeviceSlot[] { new(0, 0, 240, 80), new(240, 0, 240, 80), new(480, 0, 240, 80) }, lay.Slots);

        var gapped = DeviceLayout.Lay(3, 600, 100, gap: 1, Grid);
        Assert.Equal(new[] { 0.0, 250.0, 500.0 }, gapped.Slots.Select(s => s.X));

        // One item, or a card too narrow for two, or tall enough for two: down.
        Assert.False(DeviceLayout.Lay(1, 600, 100, gap: 0, Grid).Across);
        Assert.False(DeviceLayout.Lay(3, 470, 100, gap: 0, Grid).Across);
        Assert.False(DeviceLayout.Lay(3, 600, 170, gap: 0, Grid).Across);
    }

    [Fact]
    public void S16633_a_list_shows_one_item_at_least_and_none_past_its_edge()
    {
        Assert.Single(DeviceLayout.Lay(4, 300, 40, gap: 0, Grid).Slots);
        Assert.Empty(DeviceLayout.Lay(0, 300, 400, gap: 0, Grid).Slots);
        Assert.Equal(2, DeviceLayout.Lay(4, 300, 160, gap: 0, Grid).Slots.Count);
    }

    [Fact]
    public void S16634_a_lists_sizing_is_its_items_down_with_the_gap_between()
    {
        var plain = DeviceLayout.Sizing(3, gap: 0);
        Assert.Equal((220.0, 80.0), (plain.Min.Width, plain.Min.Height));
        Assert.Equal((300.0, 240.0), (plain.Natural.Width, plain.Natural.Height));

        var gapped = DeviceLayout.Sizing(3, gap: 2);
        Assert.Equal(280, gapped.Natural.Height);
        Assert.Equal(80, gapped.Min.Height);
    }

    [Fact]
    public void S16636_a_device_list_hides_by_id_and_puts_the_users_order_first()
    {
        string[] devices = ["a", "b", "c", "d"];
        Assert.Equal(["c", "a", "d"], DeviceOrder.Arrange(devices, d => d, ["B"], ["c", "x"]));

        // A device not there keeps its place after the shown ones.
        Assert.Equal(["d", "a", "x"], DeviceOrder.Reordered(["a", "x", "d"], ["d", "a"]));

        var notes = new List<string>();
        Assert.Equal(["a"], DeviceOrder.Repaired(["a", "A", " "], "bluetooth", "hidden", notes));
        Assert.Equal(2, notes.Count);
    }

    [Fact]
    public void S16637_a_single_device_face_is_the_items_height_and_never_narrower_than_its_line()
    {
        var face = DeviceLayout.Face(DeviceLayout.WidestLine(["419 GB free of 931 GB"]));
        Assert.Equal(76, face.Natural.Height);
        Assert.True(face.Min.Height < 76);
        Assert.True(DeviceLayout.Face(400).Min.Width > face.Min.Width);
    }
}
