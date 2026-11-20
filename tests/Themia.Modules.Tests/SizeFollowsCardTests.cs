using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Themia.App.Widgets;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Modules.Tests.Modules;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-2449] retired (owner, 2026-11-20: "the size should not be settable, it is set by the size of the widget. do this
/// for the other widgets too"): no widget has a Size of its own, in its config, its menu or the shared settings; a
/// state file that still carries one loads with it dropped loudly ([S-1050]).
/// </summary>
public sealed class SizeFollowsCardTests
{
    /// <summary>
    /// The kinds whose menus read a list only the running host installs (as <c>SettingOwnershipTests</c>): built with
    /// stand-ins by their own menu tests.
    /// </summary>
    private static readonly string[] NeedsTheHost = ["brightness", "desktopItem", "disk", "diskHistory", "drive", "driveCapacity", "driveFileSystem", "driveFree", "driveType", "driveUsed", "gpu", "gpuClock", "gpuDedicatedMemory", "gpuEngines", "gpuFan", "gpuHistory", "gpuLoad", "gpuMemoryClock", "gpuSharedMemory", "gpuTemperature"];

    /// <summary>The words of the size lines that are gone (the kit's Size and its Small, Medium, Large), in English.</summary>
    private static readonly string[] SizeWords = ["Size", "Cell size"];

    /// <summary>Every kind's default config and, for a styled kind, one per style: what its menu is built over.</summary>
    private static IEnumerable<(string Kind, WidgetConfig Config)> Configs()
    {
        foreach (var module in ModuleRegistry.Widgets)
        {
            var descriptor = module.Descriptor;
            var config = descriptor.CreateDefault();
            yield return (descriptor.Id.Value, config);
            if (config is IStyled styled)
            {
                foreach (var style in styled.Styles.Skip(1))
                {
                    var other = descriptor.CreateDefault();
                    ((IStyled)other).StyleValue = style.Value;
                    yield return ($"{descriptor.Id.Value} ({style.Name})", other);
                }
            }
        }
    }

    /// <summary>Every setting line of every kind's menu (its Style lines and its own), submenus walked, as "kind › words".</summary>
    internal static List<string> MenuLines(out int built) => [.. MenuEntries(out built).Select(e => $"{e.Kind} › {e.Line.Text}")];

    /// <summary>Every setting line of every kind's menu (its Style lines and its own), submenus walked, with its kind.</summary>
    private static List<(string Kind, ItemMenuEntry Line)> MenuEntries(out int built)
    {
        var lines = new List<(string Kind, ItemMenuEntry Line)>();
        built = 0;
        foreach (var (kind, config) in Configs())
        {
            var module = ModuleRegistry.Widgets.Single(m => kind == m.Descriptor.Id.Value || kind.StartsWith(m.Descriptor.Id.Value + " (", StringComparison.Ordinal));
            var menu = new WidgetMenuLines(() => { }, _ => { }) { OpenSetting = _ => { } };
            try
            {
                module.MenuLines(menu, config);
            }
            catch (InvalidOperationException) when (NeedsTheHost.Contains(module.Descriptor.Id.Value))
            {
                continue;
            }

            built++;
            lines.AddRange(Walk(menu.StyleLines.Concat(menu.Lines)).Select(l => (kind, l)));
        }

        return lines;

        // A submenu's lines are settings too; a choice's options (checked or not: a sort's "Size", a file's) are not.
        static IEnumerable<ItemMenuEntry> Walk(IEnumerable<ItemMenuEntry> entries)
            => entries.Where(e => !e.IsSeparator).SelectMany(e => new[] { e }.Concat(e.Children is { } children ? Walk(children.Where(c => c.Checked is null)) : []));
    }

    /// <summary>
    /// [S-14410] The words of the count lines that are gone, in English: a forecast's and a calendar week's days, a week
    /// planner's days, a calendar's hours and its month's events a day, a habit's history, a stopwatch's laps, a
    /// gallery's and a list's columns and a gallery's pictures shown.
    /// </summary>
    private static readonly string[] CountWords = ["Days shown", "Days", "Hours shown", "Events a day", "History", "Laps", "Columns", "Pictures shown"];

    [Fact]
    public void S14410_no_widget_menu_sets_how_many_of_the_same_item_it_shows()
    {
        // Owner, 2026-11-20: "Anything that's just 'how many of the same item' (days, hours, weeks, processes) follows the
        // card's size. What the items are (which facts, which span of time a chart covers) stays a setting." A count line
        // is a number box or a choice of numbers; a submenu of which columns a table shows (what the items are) is not.
        var found = MenuEntries(out var built)
            .Where(e => CountWords.Contains(e.Line.Text) && (e.Line.Slider is not null || (e.Line.Children is { Count: > 0 } options && options.All(o => o.Checked is not null))))
            .Select(e => $"{e.Kind} › {e.Line.Text}")
            .ToList();

        Assert.True(built > 100, $"only {built} menus built");
        Assert.True(found.Count == 0, "a widget's menu sets how many of the same item it shows (the card's size does): " + string.Join("; ", found));
    }

    [Fact]
    public void S14410_a_lists_rows_stay_a_setting()
    {
        // Owner, 2026-11-20, on the follow-ups: the list "Rows" setting stays; it decides the natural height.
        var rows = MenuEntries(out _).Where(e => e.Line.Text == "Rows" && e.Line.Slider is not null).Select(e => e.Kind).Distinct().ToList();
        Assert.True(rows.Count >= 10, "a list lost its Rows line: " + string.Join(", ", rows));
    }

    [Fact]
    public void S2449_no_widget_config_holds_a_size_of_its_own()
    {
        var sized = ModuleRegistry.Widgets
            .Select(m => m.Descriptor.ConfigType)
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite && (p.Name is "Size" or "CellSize" or "YearSize" || p.PropertyType.Name == "ReadoutSize"))
                .Select(p => $"{t.Name}.{p.Name}"))
            .ToList();

        Assert.True(sized.Count == 0, "a widget keeps a size of its own (its card's size is the user's): " + string.Join(", ", sized));
        Assert.DoesNotContain(SharedSettings.All, s => s.Key == "readoutSize");
        Assert.Null(typeof(WidgetSizing).Assembly.GetType("Themia.Core.Widgets.ReadoutSize"));
        Assert.Null(typeof(WidgetSizing).Assembly.GetType("Themia.Core.Widgets.IReadoutSized"));
    }

    [Fact]
    public void S2449_no_widget_menu_has_a_size_line()
    {
        var lines = MenuLines(out var built);

        // The walk reads the widgets' menus at all: a vocabulary change that hid them would pass empty.
        Assert.True(built > 100, $"only {built} menus built");
        var found = lines.Where(l => SizeWords.Any(w => l.EndsWith(" › " + w, StringComparison.Ordinal))).ToList();
        Assert.True(found.Count == 0, "a widget's menu sets its size (the card's size is the user's): " + string.Join("; ", found));
    }

    [Fact]
    public void S2449_every_kinds_config_writes_no_size_and_reads_back_the_same()
    {
        foreach (var module in ModuleRegistry.Widgets)
        {
            var config = module.Descriptor.CreateDefault();
            var json = JsonSerializer.Serialize(config, StateJson.WidgetConfig);
            var keys = JsonNode.Parse(json)!.AsObject().Select(p => p.Key).ToList();

            Assert.DoesNotContain("size", keys);
            Assert.DoesNotContain("cellSize", keys);
            Assert.DoesNotContain("yearSize", keys);
            var back = JsonSerializer.Deserialize(json, StateJson.WidgetConfig)!;
            Assert.Equal(json, JsonSerializer.Serialize(back, StateJson.WidgetConfig));
        }
    }

    [Theory]
    [InlineData("clock")]
    [InlineData("date")]
    [InlineData("alarm")]
    [InlineData("countdown")]
    [InlineData("stopwatch")]
    [InlineData("focusSession")]
    [InlineData("quote")]
    [InlineData("musicCover")]
    [InlineData("screenTimeDonut")]
    public void S2449_S1050_a_stored_size_is_dropped_loudly_and_the_widget_loads(string kind)
    {
        // The schema changed in place: a file from before still has the widget's "size"; it loads, the key gone, one
        // line saying so. No migration reads it.
        var widget = WidgetInstance.Create(new WidgetId(kind), new FracRect(0.1, 0.1, 0.2, 0.2));
        widget.Id = "w";
        var view = ViewState.Create("v");
        view.Id = "v";
        view.Widgets.Add(widget);
        var state = new AppState { Views = [view], ActiveViewId = view.Id };
        var root = JsonNode.Parse(JsonSerializer.Serialize(state, StateJson.AppState))!;
        root["views"]![0]!["widgets"]![0]!["config"]!.AsObject()["size"] = "Large";

        var read = AppStateStore.Parse(root.ToJsonString());

        Assert.NotNull(read.State);
        Assert.Single(read.State!.Views[0].Widgets);
        Assert.Contains(read.Repairs, r => r.Contains("unknown config key 'size'", StringComparison.Ordinal) && r.Contains("'w'", StringComparison.Ordinal));
    }
}
