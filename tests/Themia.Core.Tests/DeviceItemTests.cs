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
        Assert.InRange(item.Fill, 0.54, 0.56);
        Assert.Equal("C:\\", item.Icon.Drive!.Root);
    }

    [Fact]
    public void S16631_S16632_a_drive_without_figures_keeps_its_face_and_says_why()
    {
        var share = DeviceItems.Drive(Locale.En, new DriveEntry("Z:\\", VolumeKind.Network, DriveState.Disconnected, Share: "\\\\nas\\media"));
        Assert.True(share.Absent);
        Assert.Equal(("--", "Disconnected network drive", 0.0), (share.Reading!.Value.Value, share.Line, share.Fill));

        var pending = DeviceItems.Drive(Locale.En, new DriveEntry("Y:\\", VolumeKind.Network, DriveState.Pending));
        Assert.Equal(("--", "Connecting…"), (pending.Reading!.Value.Value, pending.Line));

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
    public void S16633_an_item_is_the_drive_face_in_whole_cells()
    {
        Assert.Equal(88, DeviceLayout.Natural);
        Assert.Equal(90, DeviceLayout.Height(Grid));
        Assert.Equal(240, DeviceLayout.Width(Grid));
    }

    [Fact]
    public void S16633_items_stand_down_from_the_top_edge_touching_the_last_cut()
    {
        var lay = DeviceLayout.Lay(5, 300, 200, gap: false, Grid);

        Assert.False(lay.Across);
        Assert.Equal(new DeviceSlot[] { new(0, 0, 300, 90), new(0, 90, 300, 90), new(0, 180, 300, 90) }, lay.Slots);
    }

    [Fact]
    public void S16634_the_block_gap_puts_one_cell_between_two_items()
    {
        var lay = DeviceLayout.Lay(3, 300, 400, gap: true, Grid);

        Assert.Equal(10, lay.Gap);
        Assert.Equal(new[] { 0.0, 100.0, 200.0 }, lay.Slots.Select(s => s.Y));
        Assert.All(lay.Slots, s => Assert.Equal(90, s.Height));
    }

    [Fact]
    public void S16633_a_card_one_item_tall_and_wide_lays_them_across_the_last_cut_by_its_right_edge()
    {
        var lay = DeviceLayout.Lay(3, 600, 100, gap: false, Grid);
        Assert.True(lay.Across);
        Assert.Equal(new DeviceSlot[] { new(0, 0, 240, 90), new(240, 0, 240, 90), new(480, 0, 240, 90) }, lay.Slots);

        var gapped = DeviceLayout.Lay(3, 600, 100, gap: true, Grid);
        Assert.Equal(new[] { 0.0, 250.0, 500.0 }, gapped.Slots.Select(s => s.X));

        // One item, or a card too narrow for two: down.
        Assert.False(DeviceLayout.Lay(1, 600, 100, gap: false, Grid).Across);
        Assert.False(DeviceLayout.Lay(3, 470, 100, gap: false, Grid).Across);
        Assert.False(DeviceLayout.Lay(3, 600, 180, gap: false, Grid).Across);
    }

    [Fact]
    public void S16633_a_list_shows_one_item_at_least_and_none_past_its_edge()
    {
        Assert.Single(DeviceLayout.Lay(4, 300, 40, gap: false, Grid).Slots);
        Assert.Empty(DeviceLayout.Lay(0, 300, 400, gap: false, Grid).Slots);
        Assert.Equal(2, DeviceLayout.Lay(4, 300, 180, gap: false, Grid).Slots.Count);
    }

    [Fact]
    public void S16634_a_lists_sizing_is_its_items_down_with_the_gap_between()
    {
        var plain = DeviceLayout.Sizing(3, gap: false);
        Assert.Equal((220.0, 90.0), (plain.Min.Width, plain.Min.Height));
        Assert.Equal((300.0, 270.0), (plain.Natural.Width, plain.Natural.Height));

        var gapped = DeviceLayout.Sizing(3, gap: true);
        Assert.Equal(290, gapped.Natural.Height);
        Assert.Equal(90, gapped.Min.Height);
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
        Assert.Equal(88, face.Natural.Height);
        Assert.True(face.Min.Height < 88);
        Assert.True(DeviceLayout.Face(400).Min.Width > face.Min.Width);
    }
}
