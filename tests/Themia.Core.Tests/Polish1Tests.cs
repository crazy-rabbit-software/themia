using Themia.Core.Habits;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-1560]..[S-1579] The widget polish pass: keyboard lease in the remaining lists, layout gaps.</summary>
public sealed class Polish1Tests
{
    // ---- [S-1569] Bluetooth detail panel -----------------------------------------------------------

    private static BluetoothRowPlan Bluetooth(double width, double height, int count)
        => BluetoothLayout.Plan(new WidgetArea(width, height), height - 14, count);

    [Theory]
    [InlineData(560, 376, 1, true)]
    [InlineData(560, 376, 4, true)]
    [InlineData(560, 376, 6, false)]
    [InlineData(800, 516, 1, true)]
    [InlineData(800, 516, 14, false)]
    [InlineData(320, 196, 1, false)]
    [InlineData(200, 356, 1, true)]
    [InlineData(200, 356, 6, false)]
    [InlineData(480, 116, 1, false)]
    [InlineData(560, 376, 0, false)]
    public void S1569_a_short_list_on_a_large_tile_gets_the_detail_panel(double width, double height, int count, bool detail)
        => Assert.Equal(detail, Bluetooth(width, height, count).Detail);

    [Fact]
    public void S1569_the_panel_never_shows_on_an_unmeasured_tile()
        => Assert.False(BluetoothLayout.Plan(default, 0, 1).Detail);

    [Fact]
    public void S1569_the_address_is_written_like_windows_settings_writes_it()
    {
        Assert.Equal("A4:C3:F0:85:AC:2E", BluetoothRules.AddressText("BluetoothLE#BluetoothLE00:1a:7d:da:71:13-a4:c3:f0:85:ac:2e"));
        Assert.Equal("00:00:00:00:00:0F", BluetoothRules.AddressText("Bluetooth#x-00000000000f"));
        Assert.Null(BluetoothRules.AddressText("fixture:Pixel 8"));
        Assert.Null(BluetoothRules.AddressText(null));
    }

    [Fact]
    public void S1569_the_panel_shows_the_picked_device_while_it_is_listed_else_the_first()
    {
        BluetoothDeviceEntry[] devices =
        [
            new("Headphones", "a", true, BluetoothDeviceKind.Audio, 80),
            new("Mouse", "b", false, BluetoothDeviceKind.Mouse, null),
        ];

        Assert.Equal("b", BluetoothRules.Selected(devices, "b")?.Id);
        Assert.Equal("a", BluetoothRules.Selected(devices, "gone")?.Id);
        Assert.Equal("a", BluetoothRules.Selected(devices, null)?.Id);
        Assert.Null(BluetoothRules.Selected([], "a"));
    }

    // ---- [S-1571] Habits summary line ---------------------------------------------------------------

    private static HabitLayoutPlan Habits(double width, double height, int count)
        => HabitLayout.Plan(new WidgetArea(width, height), new HabitLayoutInput(count, true, true, 7), 40, 32);

    [Fact]
    public void S1571_m_gives_the_height_no_row_can_use_to_the_summary_line_and_never_overflows()
    {
        foreach (var count in new[] { 1, 2, 3, 6, 12 })
        {
            var plan = Habits(320, 196, count);
            var used = plan.RowHeight /* add row */ + (plan.Rows * plan.RowHeight)
                + (plan.Summary ? (plan.SummaryBar ? HabitLayout.SummaryHeight : HabitLayout.SummaryLineHeight) : 0);

            Assert.True(plan.Summary, $"{count} habits");
            Assert.False(plan.SummaryBar);
            Assert.True(used <= 196 - HabitLayout.ListBottomPadding + Disclosure.Tolerance, $"{count} habits use {used}");
        }
    }

    [Fact]
    public void S1571_the_full_strip_stays_for_expanded_heights_and_compact_heights_get_none()
    {
        Assert.True(Habits(560, 376, 6).SummaryBar);
        Assert.True(Habits(560, 376, 6).Summary);
        Assert.False(Habits(200, 106, 6).Summary);
        Assert.False(Habits(480, 116, 6).Summary);
    }
}
