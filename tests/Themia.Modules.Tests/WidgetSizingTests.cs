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

    [Theory]
    [MemberData(nameof(Kinds))]
    public void S2442_a_kind_whose_size_follows_a_count_answers_with_a_config_whose_sizing_is_that_size(string id)
    {
        var kind = ModuleCatalog.Of(new WidgetId(id));
        static bool Counted(WidgetSizing s) => s.HeightSteps is { FollowsConfig: true } || s.WidthSteps is { FollowsConfig: true };

        // A count with no hook could never change by a resize; a hook with no count (in any style) has nothing to set.
        Assert.Equal(Configs(kind).Any(c => Counted(kind.Sizing(c))), kind.SizeSets is not null);
        if (kind.SizeSets is not { } sets)
        {
            return;
        }

        foreach (var config in Configs(kind).Where(c => Counted(kind.Sizing(c))))
        {
            Answers(kind, config, sets);
        }
    }

    private static void Answers(WidgetDescriptor kind, WidgetConfig config, Func<WidgetConfig, SizeDip, WidgetConfig> sets)
    {
        var sizing = kind.Sizing(config);
        foreach (var wanted in new[] { sizing.Min, sizing.Natural, sizing.Max })
        {
            var before = StateJsonOf(config);
            var next = sets(config, wanted);
            Assert.Equal(before, StateJsonOf(config));
            var after = kind.Sizing(next);
            if (sizing.HeightSteps is { FollowsConfig: true } rows)
            {
                Assert.Equal(rows.Snap(wanted.Height), after.Natural.Height);
            }

            if (sizing.WidthSteps is { FollowsConfig: true } columns)
            {
                Assert.Equal(columns.Snap(wanted.Width), after.Natural.Width);
            }
        }
    }

    private static string StateJsonOf(WidgetConfig config)
        => System.Text.Json.JsonSerializer.Serialize(config, Themia.Core.Storage.StateJson.WidgetConfig);

    private static readonly ScreenMetrics Screen = new(1000, 1000);

    [Fact]
    public void S2442_a_task_list_dragged_to_five_rows_tall_has_five_rows_and_the_given_config_stays()
    {
        var config = new TasksConfig { Rows = 6 };
        var five = new FracRect(0.1, 0.1, 0.3, (12 + (5 * 28) + 28 + 12) / 1000.0);

        var next = SizeFit.ConfigFor(config, five, Screen, scale: 1, limits: null);

        Assert.Equal(5, Assert.IsType<TasksConfig>(next).Rows);
        Assert.Equal(6, config.Rows);
        Assert.Same(config, SizeFit.ConfigFor(config, five with { H = (12 + (6 * 28) + 28 + 12) / 1000.0 }, Screen, 1, limits: null));
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public void S11601_a_render_s_min_and_max_boxes_are_drawn_with_the_config_that_size_means(string id)
    {
        var kind = new WidgetId(id);
        if (ModuleCatalog.Of(kind).SizeSets is null)
        {
            return;
        }

        {
            var config = WidgetConfig.CreateDefault(kind);
            var sizing = SizeFit.Of(config);
            foreach (var box in new[] { sizing.Min, sizing.Natural, sizing.Max })
            {
                var (drawn, painted) = SizeFit.Resized(config, box);
                var laidOut = SizeFit.Of(drawn);

                // Painted where its config is laid out (no row cut off, no row's room left empty), and on an
                // axis that follows a count, at the size asked: the min is the fewest, the max the most.
                Assert.Equal(painted, laidOut.Fit(painted));
                if (laidOut.HeightSteps is { FollowsConfig: true })
                {
                    Assert.True(painted.Height == box.Height, $"{id} asked {box}, painted {painted}");
                }

                if (laidOut.WidthSteps is { FollowsConfig: true })
                {
                    Assert.True(painted.Width == box.Width, $"{id} asked {box}, painted {painted}");
                }
            }
        }
    }

    [Fact]
    public void S13018_a_widget_added_into_a_lassoed_area_takes_the_box_its_kind_allows_there()
    {
        // A tall area: a task list takes the rows it holds, as wide as the area, anchored at its corner.
        var area = Themia.Core.Desktop.LassoArea.Offered(new FracRect(0.2, 0.1, 0.3, 0.5), caught: 0, Screen)!.Value;
        var tasks = WidgetInstance.Create(new WidgetId("tasks"), new FracRect(0, 0, 0.1, 0.1));
        Themia.Core.Desktop.LassoArea.Fill(tasks, area, Screen, 1);

        Assert.True(Assert.IsType<TasksConfig>(tasks.Config).Rows > 6);
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
    public void S2447_a_config_change_of_rows_refits_the_stored_rect_at_its_top_left()
    {
        var widget = WidgetInstance.Create(new WidgetId("tasks"), new FracRect(0.2, 0.2, 0.3, (12 + (6 * 28) + 28 + 12) / 1000.0));
        ((TasksConfig)widget.Config).Rows = 3;

        Assert.True(SizeFit.Refit(widget, Screen, 1));
        Assert.Equal(0.2, widget.Rect.X, 9);
        Assert.Equal(0.2, widget.Rect.Y, 9);
        Assert.Equal(0.12, widget.Rect.H, 9); // [S-14100] (3 * 30) + 30: three rows and the add row, whole grid cells each
        Assert.False(SizeFit.Refit(widget, Screen, 1));
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
    public void S12411_every_kind_has_no_practical_max_on_every_axis_its_config_does_not_step(string id)
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

            void Axis(string name, double max, SizeSteps? steps)
            {
                if (steps is not null)
                {
                    return;
                }

                Assert.True(max >= SizeStretch.Ceiling - 0.01, $"{id}: {name} max {max} under the ceiling {SizeStretch.Ceiling}");
            }

            Axis("width", sizing.Max.Width, sizing.WidthSteps);
            Axis("height", sizing.Max.Height, sizing.HeightSteps);
        }
    }
}

