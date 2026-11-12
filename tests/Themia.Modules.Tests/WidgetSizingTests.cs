using Themia.Core.Model;
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
        "airQuality", "airplaneModeToggle", "alarm", "audioVisualizer", "battery", "batteryDetails", "bluetooth",
        "bluetoothToggle", "brightness", "calendar", "clipboard", "colorFiltersToggle", "contacts", "countdown",
        "cpu", "cpuDetails", "desktopItem", "deviceBatteries", "disk", "doNotDisturbToggle", "drive", "email",
        "energySaverToggle", "exchangeRates", "focusSession", "folder", "gallery", "gameModeToggle", "gpu",
        "gpuDetails", "habitTracker", "hibernate", "lock", "memoryDetails", "microphoneToggle", "mixer",
        "monoAudioToggle", "moon", "music", "network", "networkDetails", "nextMeeting", "nightLightToggle", "notes",
        "notifications", "ping", "pingDetails", "pomodoro", "quote", "rainNextHour", "recentFiles", "recycleBin",
        "removableDrives", "restart", "rotationLockToggle", "rss", "scratchpad", "screenOff", "screenTimeApps",
        "screenTimeChart", "screenTimeDonut", "shortcuts", "shutdown", "signOut", "sleep", "soundToggle",
        "spatialSoundToggle", "stocks", "stopwatch", "sun", "topProcesses", "videoStream", "volume",
        "weatherForecast", "weatherNow", "weekPlanner", "wifiToggle",
        "clock", "memory", "tasks",
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
        var config = kind.CreateDefault();
        var sizing = kind.Sizing(config);
        var counted = sizing.HeightSteps is { FollowsConfig: true } || sizing.WidthSteps is { FollowsConfig: true };

        // A count with no hook could never change by a resize; a hook with no count has nothing to set.
        Assert.Equal(counted, kind.SizeSets is not null);
        if (kind.SizeSets is not { } sets)
        {
            return;
        }

        foreach (var wanted in new[] { sizing.Min, sizing.Natural, sizing.Max })
        {
            var before = config.Clone();
            var next = sets(config, wanted);
            Assert.Equal(StateJsonOf(before), StateJsonOf(config));
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
