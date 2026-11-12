using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Tasks;
using Themia.Core.Widgets;
using Themia.Modules.Tests.Modules;
using Themia.Sdk;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-2438]..[S-2443] Every kind states its sizes, and they hold: over the registry this build
/// generates, for the default config and for every style a styled kind offers. The kinds not yet
/// converted are listed here, shrink-only: a later agent converts a kind and takes it off.
/// </summary>
public sealed class WidgetSizingTests
{
    /// <summary>
    /// [S-2443] The kinds still on the old way (legacy sizing, the size ladder, the seven tiers). This
    /// list only shrinks: a converted kind is taken off, and no new kind may join it.
    /// </summary>
    private static readonly string[] Legacy =
    [
        "audioVisualizer", "calendar", "habitTracker", "music", "screenTimeApps", "screenTimeChart",
        "screenTimeDonut",
    ];

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
        var five = new FracRect(0.1, 0.1, 0.3, (36 + (5 * 28) + 28 + 12) / 1000.0);

        var next = SizeFit.ConfigFor(config, five, Screen, scale: 1, gapPx: 0, HeaderPlacement.Top);

        Assert.Equal(5, Assert.IsType<TasksConfig>(next).Rows);
        Assert.Equal(6, config.Rows);
        Assert.Same(config, SizeFit.ConfigFor(config, five with { H = (36 + (6 * 28) + 28 + 12) / 1000.0 }, Screen, 1, 0, HeaderPlacement.Top));
    }

    [Fact]
    public void S2442_a_hidden_header_is_taken_off_before_the_kind_is_asked()
    {
        var config = new TasksConfig { Rows = 6 };
        var fiveHeaderless = new FracRect(0.1, 0.1, 0.3, (12 + (5 * 28) + 28 + 12) / 1000.0);

        var next = SizeFit.ConfigFor(config, fiveHeaderless, Screen, 1, 0, HeaderPlacement.Hidden);
        Assert.Equal(5, Assert.IsType<TasksConfig>(next).Rows);
    }

    [Fact]
    public void S2447_a_config_change_of_rows_refits_the_stored_rect_at_its_top_left()
    {
        var widget = WidgetInstance.Create(new WidgetId("tasks"), new FracRect(0.2, 0.2, 0.3, (36 + (6 * 28) + 28 + 12) / 1000.0));
        ((TasksConfig)widget.Config).Rows = 3;

        Assert.True(SizeFit.Refit(widget, Screen, 1, 0));
        Assert.Equal(0.2, widget.Rect.X, 9);
        Assert.Equal(0.2, widget.Rect.Y, 9);
        Assert.Equal((36 + (3 * 28) + 28 + 12) / 1000.0, widget.Rect.H, 9);
        Assert.False(SizeFit.Refit(widget, Screen, 1, 0));
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
        Assert.Equal(36 + (6 * 40) + 28 + 12, ModuleCatalog.Of(new WidgetId("tasks")).Sizing(today).Natural.Height);
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
    public void S2449_the_clocks_card_follows_its_size_setting_and_never_the_other_way()
    {
        var kind = ModuleCatalog.Of(new WidgetId("clock"));
        WidgetSizing At(ReadoutSize size, ClockStyle style) => kind.Sizing(new ClockConfig { Size = size, Style = style });

        var (small, medium, large) = (At(ReadoutSize.Small, ClockStyle.Digital), At(ReadoutSize.Medium, ClockStyle.Digital), At(ReadoutSize.Large, ClockStyle.Digital));
        Assert.True(small.Natural.Height < medium.Natural.Height && medium.Natural.Height < large.Natural.Height);
        Assert.True(small.Natural.Width < medium.Natural.Width && medium.Natural.Width < large.Natural.Width);
        Assert.True(medium.FixedHeight && !medium.FixedWidth);

        var dial = At(ReadoutSize.Medium, ClockStyle.Analog);
        Assert.True(dial.FixedHeight && dial.FixedWidth);
        Assert.Equal(ReadoutSize.Medium, new ClockConfig().Size);
        Assert.Equal(ReadoutSize.Large, ((ClockConfig)new ClockConfig { Size = ReadoutSize.Large }.Clone()).Size);
    }

    [Fact]
    public void S2443_the_legacy_kinds_are_exactly_the_list_and_the_list_only_shrinks()
    {
        var legacy = ModuleRegistry.WidgetIds.Where(id => ModuleCatalog.Of(new WidgetId(id)).SizingOf().IsLegacy).Order(StringComparer.Ordinal).ToList();

        Assert.Empty(legacy.Except(Legacy, StringComparer.Ordinal));
        Assert.Empty(Legacy.Except(legacy, StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(Kinds))]
    public void S2443_a_legacy_kind_keeps_the_old_floor_no_maximum_and_lands_at_its_old_default_size(string id)
    {
        var sizing = ModuleCatalog.Of(new WidgetId(id)).SizingOf();
        if (!sizing.IsLegacy)
        {
            return;
        }

        Assert.Equal(new SizeDip(WidgetSizing.LegacyFloor, WidgetSizing.LegacyFloor), sizing.Min);
        Assert.Equal(SizeDip.Unbounded, sizing.Max);
        Assert.Null(sizing.HeightSteps);
        Assert.Null(sizing.WidthSteps);
    }
}
