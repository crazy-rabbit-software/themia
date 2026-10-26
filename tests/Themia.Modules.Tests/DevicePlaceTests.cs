using System.Text.Json;
using Themia.Core.Geometry;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Modules.Tests.Modules;
using Themia.Sdk;
using Themia.App.Widgets;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-2850]..[S-2855] Use my location over the real place widgets: the menu's lines in each state,
/// what the sync is asked for, whether Windows is asked at all, and the switch in the state file.
/// </summary>
public sealed class DevicePlaceTests
{
    public static TheoryData<string> Places() => ["weatherNow", "weatherForecast", "airQuality", "rainNextHour", "sun", "moon"];

    private static readonly DeviceLocation Zurich = DeviceLocation.At(47.3769, 8.5417);

    private static WidgetInstance Widget(string kind, bool device)
    {
        var widget = WidgetInstance.Create(new WidgetId(kind), new FracRect(0, 0, 0.2, 0.2));
        ((IPlaced)widget.Config).UseDeviceLocation = device;
        return widget;
    }

    private static List<ItemMenuEntry> Lines(WidgetInstance widget, DeviceLocation device, Action? settings = null)
    {
        var menu = new WidgetMenuLines(() => { }, _ => { }) { Device = device, OpenLocationSettings = settings };
        ModuleRegistry.Widgets.Single(m => m.Descriptor.Id == widget.Kind).MenuLines(menu, widget.Config);
        return menu.Lines;
    }

    private static AppState State(params WidgetInstance[] widgets)
    {
        var view = ViewState.Create("Places");
        view.Widgets.AddRange(widgets);
        var state = new AppState();
        state.Views.Clear();
        state.Views.Add(view);
        return state;
    }

    [Theory]
    [MemberData(nameof(Places))]
    public void S2850_every_place_widget_offers_the_switch_before_its_location(string kind)
    {
        var lines = Lines(Widget(kind, device: false), DeviceLocation.Off);
        var use = lines.FindIndex(l => l.Text == Str.Shell_Place_UseDevice.Text());
        var search = lines.FindIndex(l => l.Text == Str.Shell_Place_Location.Text());

        Assert.True(use >= 0 && search == use + 1, "Use my location stands right before the Location search");
        Assert.False(lines[use].Checked);
    }

    [Theory]
    [MemberData(nameof(Places))]
    public void S2850_while_on_the_search_is_not_there_and_a_click_turns_it_off(string kind)
    {
        var widget = Widget(kind, device: true);
        var lines = Lines(widget, Zurich);

        Assert.DoesNotContain(lines, l => l.Text == Str.Shell_Place_Location.Text());
        var use = Assert.Single(lines, l => l.Text == Str.Shell_Place_UseDevice.Text());
        Assert.True(use.Checked);
        Assert.DoesNotContain(lines, l => l.Note && Enum.GetValues<DeviceLocationState>().Any(s => l.Text == Themia.App.Shell.Config.DevicePlaceRows.Sentence(s)));

        use.Action!();
        Assert.False(((IPlaced)widget.Config).UseDeviceLocation);
    }

    [Theory]
    [InlineData(DeviceLocationState.Locating)]
    [InlineData(DeviceLocationState.Unavailable)]
    public void S2851_while_windows_gives_no_position_the_menu_says_why(DeviceLocationState state)
    {
        var lines = Lines(Widget("weatherNow", device: true), new DeviceLocation(state));

        Assert.Single(lines, l => l.Note && l.Text == Themia.App.Shell.Config.DevicePlaceRows.Sentence(state));
        Assert.DoesNotContain(lines, l => l.Text == Str.Shell_Place_DeviceSettings.Text());
    }

    [Fact]
    public void S2851_turned_off_in_windows_the_menu_opens_its_settings()
    {
        var opened = 0;
        var lines = Lines(Widget("sun", device: true), DeviceLocation.Denied, () => opened++);

        Assert.Single(lines, l => l.Note && l.Text == Str.Shell_Place_DeviceDenied.Text());
        Assert.Single(lines, l => l.Text == Str.Shell_Place_DeviceSettings.Text()).Action!();
        Assert.Equal(1, opened);

        // Where the host has no settings page to open (a render), the line is greyed.
        Assert.True(Assert.Single(Lines(Widget("sun", device: true), DeviceLocation.Denied), l => l.Text == Str.Shell_Place_DeviceSettings.Text()).Disabled);
    }

    [Fact]
    public void S2853_the_sync_asks_for_the_weather_at_the_device_only_while_it_has_a_position()
    {
        var state = State(Widget("weatherNow", device: true), Widget("rainNextHour", device: true));

        Assert.Empty(SyncInterests.From(state, DeviceLocation.Locating).Weather);
        Assert.Empty(SyncInterests.From(state, DeviceLocation.Denied).Rain);

        var located = SyncInterests.From(state, Zurich);
        var weather = Assert.Single(located.Weather);
        Assert.Equal((47.38, 8.54), (weather.Latitude, weather.Longitude));
        Assert.Equal((47.38, 8.54), (Assert.Single(located.Rain).Latitude, Assert.Single(located.Rain).Longitude));
    }

    [Fact]
    public void S2851_windows_is_asked_only_while_a_widget_uses_its_location()
    {
        Assert.False(DeviceLocationInterest.Wanted(State(Widget("weatherNow", device: false), Widget("sun", device: false))));
        Assert.True(DeviceLocationInterest.Wanted(State(Widget("weatherNow", device: false), Widget("moon", device: true))));
    }

    [Theory]
    [MemberData(nameof(Places))]
    public void S2850_the_state_file_holds_the_switch_and_never_the_devices_coordinates(string kind)
    {
        var widget = Widget(kind, device: true);
        var json = JsonSerializer.Serialize(widget.Config, StateJson.WidgetConfig);

        Assert.Contains("\"useDeviceLocation\":true", json.Replace(" ", "", StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.DoesNotContain("47.38", json, StringComparison.Ordinal);
        Assert.True(((IPlaced)JsonSerializer.Deserialize(json, StateJson.WidgetConfig)!).UseDeviceLocation);
    }

    [Theory]
    [MemberData(nameof(Places))]
    public void S2850_with_the_switch_on_a_widget_without_a_place_is_set_up(string kind)
    {
        var widget = Widget(kind, device: true);
        var placed = (IPlaced)widget.Config;
        var use = placed.UseDeviceLocation;
        placed.ClearLocation();
        placed.UseDeviceLocation = use;

        Assert.False(WidgetSetup.Needed(widget.Config, FeatureSet.None));
    }
}
