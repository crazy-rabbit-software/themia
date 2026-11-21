using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Tasks;
using Themia.Core.Widgets;
using Themia.Modules.Tests.Modules;
using Themia.Sdk;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-2438]..[S-2449] Every kind states its sizes, and they hold: over the registry this build
/// generates, for the default config and for every style a styled kind offers.
/// </summary>
public sealed class WidgetSizingTests
{
    public static TheoryData<string> Kinds() => [.. ModuleRegistry.WidgetIds];

    /// <summary>The configs a kind's sizing is checked over: its defaults, then each of its styles.</summary>
    private static IEnumerable<WidgetConfig> Configs(WidgetDescriptor kind)
    {
        yield return kind.CreateDefault();
        if (kind.CreateDefault() is IStyled styled)
        {
            foreach (var style in styled.Styles)
            {
                var config = kind.CreateDefault();
                ((IStyled)config).StyleValue = style.Value;
                yield return config;
            }
        }
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public void S2441_every_kind_states_a_sizing_that_holds_for_its_defaults_and_every_style(string id)
    {
        var kind = ModuleCatalog.Of(new WidgetId(id));
        foreach (var config in Configs(kind))
        {
            var sizing = kind.Sizing(config);
            Assert.True(sizing.Problems().Count == 0, $"{id}: {string.Join("; ", sizing.Problems())}");
            Assert.Equal(sizing, kind.Sizing(config.Clone()));
        }
    }

    /// <summary>Displays the resize is checked on: whole DIP cells, 1536 × 864's cells of 9.974 × 10.047, 1366 × 768's.</summary>
    private static readonly ScreenMetrics[] Screens = [new(1920, 1080), new(1536, 864), new(1366, 768)];

    private static readonly ScreenMetrics Screen = new(1000, 1000);

    [Theory]
    [MemberData(nameof(Kinds))]
    public void S14500_every_kind_resizes_by_one_grid_cell_on_both_axes(string id)
    {
        // Owner, 2026-11-20: "the widgets should not have the size increments anymore but should be incrementable in steps
        // of one": over every kind and style, on displays of whole and of part DIP cells, each axis that is not fixed lands
        // a resize on every whole cell from its least, none skipped.
        var kind = ModuleCatalog.Of(new WidgetId(id));
        foreach (var config in Configs(kind))
        {
            foreach (var screen in Screens)
            {
                var limits = SizeFit.Limits(kind.Sizing(config), screen, 1)!;
                var (cols, rows) = GridGeometry.GridDims(screen);
                foreach (var (axis, lines, name) in new[] { (limits.Width, cols, "width"), (limits.Height, rows, "height") })
                {
                    if (axis.Fixed)
                    {
                        continue;
                    }

                    var least = (int)Math.Round(axis.Min * lines);
                    var most = Math.Min((int)Math.Round(axis.Max * lines), least + 24);
                    for (var cells = least; cells <= most; cells++)
                    {
                        Assert.True(Math.Abs(axis.Range(cells / (double)lines) - (cells / (double)lines)) < 1e-9, $"{id} on {screen.Width:0}: {name} of {cells} cells lands on {axis.Range(cells / (double)lines) * lines:0.##}");
                    }
                }
            }
        }
    }

    [Fact]
    public void S13018_a_widget_added_into_a_lassoed_area_takes_the_box_its_kind_allows_there()
    {
        // A tall area: a task list as tall as the area (its rows the card holds, [S-14502]), as wide as it allows, anchored
        // at its corner; its config as it was.
        var area = Themia.Core.Desktop.LassoArea.Offered(new FracRect(0.2, 0.1, 0.3, 0.5), caught: 0, Screen)!.Value;
        var tasks = WidgetInstance.Create(new WidgetId("tasks"), new FracRect(0, 0, 0.1, 0.1));
        Themia.Core.Desktop.LassoArea.Fill(tasks, area, Screen, 1);

        Assert.Equal(6, Assert.IsType<TasksConfig>(tasks.Config).Rows);
        Assert.Equal(area.H, tasks.Rect.H, 9);
        Assert.Equal(area.X, tasks.Rect.X, 9);
        Assert.Equal(area.Y, tasks.Rect.Y, 9);
        Assert.True(tasks.Rect.W <= area.W + 1e-9 && tasks.Rect.H <= area.H + 1e-9);

        // An area larger than a kind's maximum: the widget stops at its maximum, inside the area.
        var toggle = WidgetInstance.Create(new WidgetId("wifiToggle"), new FracRect(0, 0, 0.1, 0.1));
        var max = SizeFit.Limits(toggle, Screen, 1)!;
        Themia.Core.Desktop.LassoArea.Fill(toggle, area, Screen, 1);
        Assert.True(toggle.Rect.W <= max.Width.Max + 1e-9 && toggle.Rect.W <= area.W + 1e-9);
    }

    [Fact]
    public void S2447_S14503_a_config_change_of_rows_puts_the_card_at_its_new_natural_at_its_top_left()
    {
        // [S-14503] Rows decide a list's natural height: set to three, a card of any height stands at three rows and the add
        // row ((3 * 30) + 30), its top-left kept; a second refit with nothing changed leaves it.
        var widget = WidgetInstance.Create(new WidgetId("tasks"), new FracRect(0.2, 0.2, 0.3, 0.27));
        var was = SizeFit.Of(widget);
        ((TasksConfig)widget.Config).Rows = 3;

        Assert.True(SizeFit.Refit(widget, Screen, 1, was));
        Assert.Equal(0.2, widget.Rect.X, 9);
        Assert.Equal(0.2, widget.Rect.Y, 9);
        Assert.Equal(0.12, widget.Rect.H, 9);
        Assert.False(SizeFit.Refit(widget, Screen, 1, SizeFit.Of(widget)));

        // A change that moved the least too (two lines a row) only fits the card into its new range: a taller one stays.
        var tall = WidgetInstance.Create(new WidgetId("tasks"), new FracRect(0.2, 0.2, 0.3, 0.4));
        var before = SizeFit.Of(tall);
        ((TasksConfig)tall.Config).Lines = 2;
        ((TasksConfig)tall.Config).ListId = TaskRules.TodayListId;
        SizeFit.Refit(tall, Screen, 1, before);
        Assert.Equal(0.4, tall.Rect.H, 9);
    }

    [Fact]
    public void S2447_S855_a_card_that_grows_stays_clear_of_a_taskbar_it_was_clear_of_and_one_hung_over_it_stays()
    {
        // A taskbar from 0.9 down. A list flush on its edge that grows by its Rows is pushed up clear of it; one the user
        // hung over it ([S-6608], owner 2026-11-21: "the widgets should be able to go over the screen") stays where it is.
        var screen = Screen with { Allowed = new FracRect(0, 0, 1, 0.9) };
        WidgetInstance List(double y)
        {
            var widget = WidgetInstance.Create(new WidgetId("tasks"), new FracRect(0.2, y, 0.3, 0.12));
            ((TasksConfig)widget.Config).Rows = 3;
            return widget;
        }

        var flush = List(0.78);
        var hung = List(0.85);
        foreach (var widget in new[] { flush, hung })
        {
            var was = SizeFit.Of(widget);
            ((TasksConfig)widget.Config).Rows = 6;
            Assert.True(SizeFit.Refit(widget, screen, 1, was));
            Assert.True(widget.Rect.H > 0.12 + 1e-9);
        }

        Assert.Equal(0.9, flush.Rect.Bottom, 9);
        Assert.Equal(0.85, hung.Rect.Y, 9);
    }

    [Fact]
    public void S2440_tasks_draws_two_lines_only_where_a_row_has_a_second_line()
    {
        var local = new TasksConfig { Source = TaskSource.Local, Lines = 2 };
        var today = new TasksConfig { ListId = TaskRules.TodayListId, Lines = 2 };
        var compact = new TasksConfig { ListId = TaskRules.TodayListId, Lines = 2, Style = TasksStyle.Compact };

        Assert.Equal(1, local.RowsShown.Lines);
        Assert.Equal(2, today.RowsShown.Lines);
        Assert.Equal(1, compact.RowsShown.Lines);
        Assert.Equal((6 * 40) + 30, ModuleCatalog.Of(new WidgetId("tasks")).Sizing(today).Natural.Height);
    }

    [Fact]
    public void S2440_rows_out_of_range_are_repaired_and_a_copy_keeps_them()
    {
        var config = new TasksConfig { Rows = 99, Lines = 7 };
        Assert.NotEmpty(config.Repair());
        Assert.Equal((20, 2), (config.Rows, config.Lines));

        var copy = (TasksConfig)config.Clone();
        Assert.Equal((20, 2), (copy.Rows, copy.Lines));
    }

    [Fact]
    public void S2449_retired_the_clocks_size_is_its_cards_its_natural_the_one_readout_step()
    {
        // Owner, 2026-11-20: "the size should not be settable, it is set by the size of the widget".
        var kind = ModuleCatalog.Of(new WidgetId("clock"));
        WidgetSizing At(ClockStyle style) => kind.Sizing(new ClockConfig { Style = style });

        var digital = At(ClockStyle.Digital);
        Assert.Equal(ClockFace.Step.LineHeight + (2 * Themia.Sdk.Kit.FamilySpace.Inset) + ClockFace.Slack, digital.Natural.Height);
        // [S-12833] It scales down to its smallest text's floor: its least height under its natural one.
        Assert.True(digital.Min.Height < digital.Natural.Height && !digital.FixedWidth);

        // [S-12411] Relaxed: no practical Max; the face scales up with the card.
        Assert.Equal(SizeStretch.Ceiling, digital.Max.Height);
        var dial = At(ClockStyle.Analog);
        // [S-12833] The dial scales down too, to its floor in the inset.
        Assert.Equal(new SizeDip(ClockFace.DialFloor + 24, ClockFace.DialFloor + 24), dial.Min);
        Assert.Equal(ClockFace.DialSide + (2 * Themia.Sdk.Kit.FamilySpace.Inset) + ClockFace.Slack, dial.Natural.Height);
        Assert.Equal(ClockFace.DialSide + 24 + ClockFace.Slack, dial.Natural.Width);
        Assert.Equal(new SizeDip(SizeStretch.Ceiling, SizeStretch.Ceiling), dial.Max);
        Assert.Null(typeof(ClockConfig).GetProperty("Size"));
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public void S12411_every_kind_has_no_practical_max_on_every_axis(string id)
    {
        var kind = ModuleCatalog.Of(new WidgetId(id));
        foreach (var config in Configs(kind))
        {
            var sizing = kind.Sizing(config);
            if (sizing.Room == StretchRoom.None)
            {
                // Only a size that is not the content's to give (the desktop icon's cell).
                Assert.Equal("desktopItem", id);
                continue;
            }

            void Axis(string name, double max)
                => Assert.True(max >= SizeStretch.Ceiling - 0.01, $"{id}: {name} max {max} under the ceiling {SizeStretch.Ceiling}");

            Axis("width", sizing.Max.Width);
            if (!sizing.KeepsHeight)
            {
                Axis("height", sizing.Max.Height);
            }
        }
    }
}

