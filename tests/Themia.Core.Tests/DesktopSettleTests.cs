using Themia.Core.Desktop;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Sdk;
using Xunit;
using Xunit.Abstractions;

namespace Themia.Core.Tests;

/// <summary>
/// [S-11887] [S-11888] Desktop icons converted or settled on the widget grid keep their arrangement: order kept,
/// no two overlapping, none on a widget, all inside the work area, a valid layout untouched; a rule's placement
/// keeps one grid cell off the work area's edge.
/// </summary>
public sealed class DesktopSettleTests(ITestOutputHelper output)
{
    /// <summary>The owner's display: 2560 × 1440 DIPs (3840 × 2160 at 150 %), Karpo's appbar reserving 42 DIPs on the left.</summary>
    private static readonly ScreenMetrics Owner = new(2560, 1440) { Allowed = new FracRect(42 / 2560.0, 0, 2518 / 2560.0, 1) };

    /// <summary>A 100 × 100 DIP screen: a 10 × 10 grid.</summary>
    private static readonly ScreenMetrics Ten = new(100, 100);

    /// <summary>The owner's icon box: 80 × 100 DIPs (Explorer's 76 × 100 cell on the 10-DIP grid).</summary>
    private static readonly (double W, double H) Box = DesktopGrid.IconSize(new SizeDip(76, 100), Owner);

    private sealed class CardConfig : WidgetConfig
    {
        public override WidgetConfig Clone() => new CardConfig();
    }

    private static FracRect Dip(double x, double y, double w, double h) => new(x / 2560, y / 1440, w / 2560, h / 1440);

    private static (double X, double Y, double W, double H) InDip(FracRect r)
        => (Math.Round(r.X * 2560, 3), Math.Round(r.Y * 1440, 3), Math.Round(r.W * 2560, 3), Math.Round(r.H * 1440, 3));

    /// <summary>The owner's Home widgets as they stood (state.before-drag-look.json, view "Home"), in DIPs.</summary>
    private static readonly FracRect[] HomeWidgets =
    [
        Dip(200, 80, 370, 130),    // clock
        Dip(200, 230, 370, 250),   // calendar
        Dip(200, 500, 110, 110),   // musicCover
        Dip(310, 500, 260, 110),   // music
        Dip(200, 610, 370, 60),    // musicProgress
        Dip(2150, 80, 330, 120),   // weatherNow
        Dip(2150, 200, 330, 150),  // weatherFacts
        Dip(2150, 350, 330, 180),  // weatherForecast
        Dip(2150, 530, 330, 160),  // rainNextHour
        Dip(2200, 710, 70, 70),    // wifiToggle
        Dip(2270, 710, 70, 70),    // bluetoothToggle
        Dip(2340, 710, 70, 70),    // doNotDisturbToggle
        Dip(2410, 710, 70, 70),    // nightLightToggle
    ];

    /// <summary>The owner's seven Home icons as stored (that file's order), 48 × 64 at x = 60.</summary>
    private static readonly (string Id, double Y)[] HomeIcons =
    [
        ("d0f1d9", 84), ("1b1d88", 336), ("1939c5", 252), ("f8486c", 420), ("e4a75f", 168), ("6e6e8c", 504), ("9d05e4", 588),
    ];

    private static bool Overlap(FracRect a, FracRect b)
        => a.X < b.Right - 1e-9 && b.X < a.Right - 1e-9 && a.Y < b.Bottom - 1e-9 && b.Y < a.Bottom - 1e-9;

    private static void Valid(IReadOnlyList<FracRect> icons, IReadOnlyList<FracRect> widgets, ScreenMetrics screen, (double W, double H) box)
    {
        var a = screen.Allowed;
        for (var i = 0; i < icons.Count; i++)
        {
            var r = icons[i];
            Assert.Equal(box.W, r.W, 9);
            Assert.Equal(box.H, r.H, 9);
            Assert.True(r.X >= a.X - 1e-9 && r.Y >= a.Y - 1e-9 && r.Right <= a.Right + 1e-9 && r.Bottom <= a.Bottom + 1e-9, $"icon {i} {r} outside the work area");
            Assert.DoesNotContain(widgets, w => Overlap(r, w));
            for (var j = i + 1; j < icons.Count; j++)
            {
                Assert.False(Overlap(r, icons[j]), $"icons {i} and {j} overlap: {r} / {icons[j]}");
            }
        }
    }

    private static FracRect[] Applied(IReadOnlyList<FracRect> icons, IReadOnlyList<FracRect?> settled)
        => [.. icons.Select((r, i) => settled[i] ?? r)];

    [Fact]
    public void S11887_the_owners_column_of_seven_icons_stays_a_column_in_order_at_the_new_box()
    {
        // Regression (owner, 2026-11-15): the Home view's seven icons, a clean column at x = 60 of 48 × 64 boxes
        // 84 apart, came out of the first start on the widget grid at 80 × 100 boxes 70 apart, overlapping, at
        // the work area's very edge (x = 42), and This PC thrown to (570, 0). Each icon was fitted at its own
        // corner, none looking at the others. Through the settle pass the start runs (DesktopDisplays.Plan).
        var home = ViewState.Create("Home");
        var state = new AppState { Views = [home], ActiveViewId = home.Id };
        state.MonitorViews["main"] = home.Id;
        home.Widgets.AddRange(HomeWidgets.Select((r, k) => new WidgetInstance { Id = "card" + k, Config = new CardConfig(), Rect = r }));
        home.Widgets.AddRange(HomeIcons.Select(i => new WidgetInstance { Id = i.Id, Config = new DeskIconFake { Target = "shell:" + i.Id }, Rect = Dip(60, i.Y, 48, 64) }));

        var shown = new DesktopShownView(home.Id, Owner);
        var plan = DesktopDisplays.Plan(state, ["main"], [shown], shown, [], (76, 100));
        DesktopDisplays.Apply(state, plan, (_, _) => throw new InvalidOperationException("no stand-in here"));

        // One column at x = 60, in the order the icons stood, each 100 under the one above.
        var column = home.Widgets.Where(w => w.Config is IDesktopItemConfig).OrderBy(w => w.Rect.Y).ToList();
        Assert.Equal(["d0f1d9", "e4a75f", "1939c5", "1b1d88", "f8486c", "6e6e8c", "9d05e4"], column.Select(w => w.Id));
        for (var k = 0; k < column.Count; k++)
        {
            Assert.Equal((60, 80 + (k * 100), 80, 100), InDip(column[k].Rect));
        }

        Valid([.. column.Select(w => w.Rect)], HomeWidgets, Owner, Box);

        // Settled: the next start writes nothing.
        Assert.False(DesktopDisplays.Plan(state, ["main"], [shown], shown, [], (76, 100)).Changes(state));
    }

    [Fact]
    public void S11887_a_valid_layout_is_left_alone_even_against_the_edge_or_on_a_widget()
    {
        // An icon the user put at the work area's edge, and one on the clock: valid, no write.
        FracRect[] icons = [Dip(42, 0, 80, 100), Dip(42, 100, 80, 100), Dip(250, 100, 80, 100)];
        Assert.All(DesktopGrid.Settle(icons, HomeWidgets, Box, Owner), r => Assert.Null(r));
    }

    [Fact]
    public void S11887_overlapping_icons_are_invalid_and_the_later_one_moves_while_the_valid_ones_stay()
    {
        // The owner's Home icons as they are now (state.json, 2026-11-15 18:27): 80 × 100 at x = 42, 70 to 110 apart.
        var widgets = HomeWidgets;
        (string Id, FracRect Rect)[] now =
        [
            ("d0f1d9", new(0.22265625, 0, 0.03125, 0.06944444444444445)),
            ("1b1d88", new(0.01640625, 0.16663578817353714, 0.03125, 0.06944444444444445)),
            ("1939c5", new(0.01640625, 0.09718619731357109, 0.03125, 0.06944444444444445)),
            ("f8486c", new(0.01640625000000001, 0.3680909371622665, 0.03125, 0.06944444444444445)),
            ("e4a75f", new(0.01640625, 0.04857958931604138, 0.03125, 0.06944444444444445)),
            ("6e6e8c", new(0.01640625000000001, 0.29163964798517833, 0.03125, 0.06944444444444445)),
            ("9d05e4", new(0.01640625000000001, 0.22908368071638102, 0.03125, 0.06944444444444445)),
        ];
        var icons = now.Select(n => n.Rect).ToArray();
        var settled = DesktopGrid.Settle(icons, widgets, Box, Owner);
        var after = Applied(icons, settled);

        for (var i = 0; i < now.Length; i++)
        {
            output.WriteLine($"{now[i].Id}: {InDip(icons[i])} -> {(settled[i] is null ? "stays" : InDip(after[i]).ToString())}");
        }

        Valid(after, widgets, Owner, Box);

        // The two that overlapped an icon above them move (Documents, New folder); every other stays as it is.
        Assert.Equal(["1939c5", "9d05e4"], now.Where((_, i) => settled[i] is not null).Select(n => n.Id).Order());

        // Settled once: settling again moves nothing.
        Assert.All(DesktopGrid.Settle(after, widgets, Box, Owner), r => Assert.Null(r));
    }

    [Fact]
    public void S11887_a_row_stays_a_row_in_order()
    {
        // Four 48-wide icons 76 apart in a row grow to 80 wide: pushed along the row, never stacked.
        FracRect[] icons = [Dip(100, 100, 48, 64), Dip(176, 100, 48, 64), Dip(252, 100, 48, 64), Dip(328, 100, 48, 64)];
        var after = Applied(icons, DesktopGrid.Settle(icons, [], Box, Owner));

        Assert.Equal([(100.0, 100.0), (180, 100), (260, 100), (340, 100)], after.Select(r => (InDip(r).X, InDip(r).Y)));
        Valid(after, [], Owner, Box);
    }

    [Fact]
    public void S11887_a_column_pushed_onto_a_widget_takes_the_nearest_free_spot_and_none_overlaps()
    {
        // A widget under the column: the icons that would be pushed onto it take the nearest free spots instead,
        // never across the screen; the ones above keep the column.
        var card = Dip(50, 380, 150, 100);
        FracRect[] icons = [.. Enumerable.Range(0, 6).Select(k => Dip(60, 84 + (k * 84), 48, 64))];
        var after = Applied(icons, DesktopGrid.Settle(icons, [card], Box, Owner));

        Valid(after, [card], Owner, Box);
        Assert.Equal([80.0, 180, 280], after.Take(3).Select(r => InDip(r).Y));
        Assert.All(after.Take(3), r => Assert.Equal(60, InDip(r).X));

        // Every icon within a few of its own boxes of where it stood.
        for (var k = 0; k < icons.Length; k++)
        {
            Assert.InRange(Math.Abs(after[k].X - icons[k].X) * 2560, 0, 3 * 80);
            Assert.InRange(Math.Abs(after[k].Y - icons[k].Y) * 1440, 0, 3 * 100);
        }
    }

    [Fact]
    public void S11887_icons_under_a_widget_or_outside_the_work_area_move_to_free_spots_round_the_rest()
    {
        // Mixed: one valid icon, one under the taskbar, one of the old box on top of the clock.
        var clock = Dip(200, 80, 370, 130);
        FracRect[] icons = [Dip(60, 80, 80, 100), Dip(60, 1400, 80, 100), Dip(220, 90, 48, 64)];
        var settled = DesktopGrid.Settle(icons, [clock], Box, Owner);
        var after = Applied(icons, settled);

        Assert.Null(settled[0]);
        Valid(after, [clock], Owner, Box);
        Assert.Equal((60.0, 1330.0), (InDip(after[1]).X, InDip(after[1]).Y));
    }

    [Theory]
    [InlineData(DesktopIconSize.Small)]
    [InlineData(DesktopIconSize.Medium)]
    [InlineData(DesktopIconSize.Large)]
    public void S5593_S11887_an_icon_size_change_and_back_keeps_the_column_and_the_row_in_order(DesktopIconSize start)
    {
        var sizes = new[] { DesktopIconSize.Small, DesktopIconSize.Medium, DesktopIconSize.Large };
        (double W, double H) BoxOf(DesktopIconSize s) => DesktopGrid.IconSize(DesktopCell.Grid(s), Owner);

        // A column of seven and a row of four, settled at the start size round the owner's widgets.
        var first = BoxOf(start);
        FracRect[] laid = [.. Enumerable.Range(0, 7).Select(k => Dip(60, 84 + (k * 84), 48, 64)), .. Enumerable.Range(0, 4).Select(k => Dip(700 + (k * 76), 900, 48, 64))];
        laid = Applied(laid, DesktopGrid.Settle(laid, HomeWidgets, first, Owner));
        Valid(laid, HomeWidgets, Owner, first);
        var origin = laid;

        var from = start;
        foreach (var to in sizes.Where(s => s != start).Append(start))
        {
            laid = [.. DesktopGrid.Rescale(laid, HomeWidgets, BoxOf(from), BoxOf(to), Owner)];
            output.WriteLine($"{from} -> {to}: {string.Join(" ", laid.Select(InDip))}");
            Valid(laid, HomeWidgets, Owner, BoxOf(to));

            // The column is one column top to bottom in its order, the row one row left to right in its order.
            Assert.All(laid.Take(7), r => Assert.Equal(laid[0].X, r.X, 9));
            Assert.True(laid.Take(7).Zip(laid.Skip(1).Take(6)).All(p => p.First.Y < p.Second.Y));
            Assert.All(laid.Skip(7), r => Assert.Equal(laid[7].Y, r.Y, 9));
            Assert.True(laid.Skip(7).Zip(laid.Skip(8)).All(p => p.First.X < p.Second.X));
            from = to;
        }

        // Back at the start size: where they began.
        Assert.Equal(origin.Select(InDip), laid.Select(InDip));

        // The same size both ways writes nothing new.
        Assert.Equal(laid.Select(InDip), DesktopGrid.Rescale(laid, HomeWidgets, first, first, Owner).Select(InDip));
    }

    [Fact]
    public void S11888_a_rules_placement_keeps_one_grid_cell_off_the_work_areas_edge()
    {
        // The first free spot: one cell in from the work area's corner (x = 42 rounds up to the line at 50, plus one cell).
        var first = DesktopGrid.FirstFree(Box, [], Owner);
        Assert.Equal((60.0, 10.0), (InDip(first!.Value).X, InDip(first.Value).Y));

        // Nothing is placed against the far edges either: the last spot of a row ends a cell before the right edge.
        var right = DesktopGrid.NearestFree(Box, 1, 1, [], Owner);
        Assert.Equal((2470.0, 1330.0), (InDip(right!.Value).X, InDip(right.Value).Y));

        // A screen too small for the inset still takes the box at its edge.
        Assert.NotNull(DesktopGrid.FirstFree((0.9, 0.9), [], Ten));
    }
}
