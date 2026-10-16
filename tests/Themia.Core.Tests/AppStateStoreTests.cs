using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Storage;
using Xunit;

namespace Themia.Core.Tests;

public sealed class AppStateStoreTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;

    public AppStateStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "themia-tests", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(_dir);
        _path = Path.Combine(_dir, "state.json");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
            // ignore: temp cleanup is best effort and must not fail a test run
        }
    }

    private AppStateStore NewStore() => new(_path, TimeSpan.Zero);

    [Fact]
    public void A_missing_file_is_Fresh_not_Recovered()
    {
        var result = NewStore().Load();

        Assert.Equal(LoadOutcome.Fresh, result.Outcome);
        Assert.Equal(AppState.CreateDefault().Views.Count, result.State.Views.Count);
    }

    [Fact]
    public void An_empty_file_is_corrupt_not_absent()
    {
        File.WriteAllText(_path, string.Empty);

        var result = NewStore().Load();

        Assert.Equal(LoadOutcome.Recovered, result.Outcome);
    }

    [Fact]
    public void Corrupt_json_is_set_aside_rather_than_deleted()
    {
        File.WriteAllText(_path, "{ this is not json");

        var result = NewStore().Load();

        Assert.Equal(LoadOutcome.Recovered, result.Outcome);
        Assert.NotEmpty(Directory.GetFiles(_dir, "state.json.corrupt-*"));
    }

    [Fact]
    public void An_older_schema_is_refused_not_upgraded()
    {
        File.WriteAllText(_path, $$"""{"schemaVersion": {{AppState.CurrentSchemaVersion - 1}}, "views": []}""");

        var result = NewStore().Load();

        Assert.Equal(LoadOutcome.Recovered, result.Outcome);
        Assert.Contains("not supported", result.Detail);
    }

    [Fact]
    public void A_schema_from_the_future_is_refused_rather_than_half_read()
    {
        File.WriteAllText(_path, $$"""{"schemaVersion": {{AppState.CurrentSchemaVersion + 1}}, "views": []}""");

        var result = NewStore().Load();

        Assert.Equal(LoadOutcome.Recovered, result.Outcome);
        Assert.Contains("newer than this build", result.Detail);
    }

    [Fact]
    public async Task A_round_trip_preserves_geometry_config_and_the_active_view()
    {
        var state = AppState.CreateDefault();
        var view = state.ActiveView();
        var widget = view.Widgets[0];
        widget.Rect = new FracRect(0.11, 0.22, 0.33, 0.44);
        widget.Label = "Bedside";
        widget.Surface = WidgetSurface.Transparent;
        ((ClockConfig)widget.Config).ShowSeconds = true;
        ((ClockConfig)widget.Config).ShowDate = false;
        state.Preferences.Region.Clock = ClockHours.Twelve;
        state.Preferences.Refresh.WeatherMinutes = 60;
        state.Design.WidgetGapPx = 12;
        state.Design.GridSnap = false;

        var store = NewStore();
        store.SaveSoon(state);
        await store.FlushAsync();

        var loaded = NewStore().Load();

        Assert.Equal(LoadOutcome.Loaded, loaded.Outcome);
        var reloaded = loaded.State.ActiveView().Widgets[0];
        Assert.Equal(new FracRect(0.11, 0.22, 0.33, 0.44), reloaded.Rect);
        Assert.Equal("Bedside", reloaded.Label);
        Assert.Equal(WidgetSurface.Transparent, reloaded.Surface);
        var config = Assert.IsType<ClockConfig>(reloaded.Config);
        Assert.True(config.ShowSeconds);
        Assert.False(config.ShowDate);
        Assert.Equal(ClockHours.Twelve, loaded.State.Preferences.Region.Clock);
        Assert.Equal(60, loaded.State.Preferences.Refresh.WeatherMinutes);
        Assert.Equal(12, loaded.State.Design.WidgetGapPx);
        Assert.False(loaded.State.Design.GridSnap);
    }

    [Fact]
    public async Task A_save_leaves_no_temp_file_behind()
    {
        var store = NewStore();
        store.SaveSoon(AppState.CreateDefault());
        await store.FlushAsync();

        Assert.Empty(Directory.GetFiles(_dir, "*.tmp"));
    }

    [Fact]
    public async Task Coalesced_saves_write_the_latest_snapshot()
    {
        var store = new AppStateStore(_path, TimeSpan.FromMilliseconds(50));

        for (var i = 1; i <= 20; i++)
        {
            var state = AppState.CreateDefault();
            state.Design.WidgetGapPx = i;
            store.SaveSoon(state);
        }

        await store.FlushAsync();

        Assert.Equal(20, NewStore().Load().State.Design.WidgetGapPx);
    }

    [Fact]
    public void Validate_repairs_a_dangling_active_view_id()
    {
        var state = AppState.CreateDefault();
        state.ActiveViewId = "does-not-exist";

        var repairs = AppStateStore.Validate(state);

        Assert.Contains(repairs, r => r.Contains("active view"));
        Assert.Equal(state.Views[0].Id, state.ActiveViewId);
    }

    [Fact]
    public void Validate_drops_a_monitor_assignment_pointing_at_a_deleted_view()
    {
        var state = AppState.CreateDefault();
        state.MonitorViews["\\\\.\\DISPLAY1"] = "deleted-view";

        AppStateStore.Validate(state);

        Assert.Empty(state.MonitorViews);
    }

    [Fact]
    public void Validate_regenerates_duplicate_widget_ids()
    {
        var state = AppState.CreateDefault();
        var view = state.ActiveView();
        var clone = view.Widgets[0].Clone();
        clone.Id = view.Widgets[0].Id;
        view.Widgets.Add(clone);

        AppStateStore.Validate(state);

        Assert.Equal(view.Widgets.Count, view.Widgets.Select(w => w.Id).Distinct().Count());
    }

    [Fact]
    public void Validate_replaces_a_config_that_does_not_match_its_widget_kind()
    {
        var state = AppState.CreateDefault();
        var widget = state.ActiveView().Widgets[0];
        widget.Config = null!;

        AppStateStore.Validate(state);

        Assert.NotNull(widget.Config);
        Assert.Equal(widget.Kind, widget.Config.Kind);
    }

    [Fact]
    public void Validate_pulls_an_off_screen_widget_back_into_bounds()
    {
        var state = AppState.CreateDefault();
        var widget = state.ActiveView().Widgets[0];
        widget.Rect = new FracRect(5, 5, 0.2, 0.2);

        AppStateStore.Validate(state);

        Assert.True(widget.Rect.Right <= 1.0000001);
        Assert.True(widget.Rect.Bottom <= 1.0000001);
    }

    [Fact]
    public void Validate_clamps_nonsense_design_values()
    {
        var state = AppState.CreateDefault();
        state.Design.WidgetGapPx = double.NaN;
        state.Design.SurfaceOpacity = -4;
        state.Design.CornerRadiusPx = 9999;

        AppStateStore.Validate(state);

        Assert.True(double.IsFinite(state.Design.WidgetGapPx));
        Assert.InRange(state.Design.SurfaceOpacity, 0.2, 1.0);
        Assert.InRange(state.Design.CornerRadiusPx, 0, 32);
    }

    [Fact]
    public void S6482_a_corner_radius_that_is_not_a_number_loads_as_the_4px_default()
    {
        var state = AppState.CreateDefault();
        state.Design.CornerRadiusPx = double.NaN;

        AppStateStore.Validate(state);

        Assert.Equal(4, state.Design.CornerRadiusPx);
    }

    [Fact]
    public void ViewForMonitor_falls_back_to_the_active_view_when_unassigned()
    {
        var state = AppState.CreateDefault();

        var view = state.ViewForMonitor("\\\\.\\DISPLAY9");

        Assert.Equal(state.ActiveViewId, view.Id);
    }

    [Fact]
    public void ViewForMonitor_honours_an_explicit_assignment()
    {
        var state = AppState.CreateDefault();
        var second = ViewState.Create("Work");
        state.Views.Add(second);
        state.MonitorViews["\\\\.\\DISPLAY2"] = second.Id;

        Assert.Equal(second.Id, state.ViewForMonitor("\\\\.\\DISPLAY2").Id);
        Assert.Equal(state.ActiveViewId, state.ViewForMonitor("\\\\.\\DISPLAY1").Id);
    }

    /// <summary>
    /// A hand-written state file from before the calculator and the containers were removed: one
    /// widget whose kind this build does not have, a group, and a clock that was inside the group.
    /// </summary>
    private const string RemovedKindsDocument = """
        {
          "schemaVersion": 1,
          "activeViewId": "v",
          "views": [
            {
              "id": "v",
              "name": "Desk",
              "widgets": [
                { "id": "calc", "kind": "Calculator", "x": 0.1, "y": 0.1, "w": 0.2, "h": 0.2, "config": { "kind": "calculator", "decimals": 4 } },
                { "id": "g", "kind": "Group", "x": 0.4, "y": 0.1, "w": 0.4, "h": 0.5, "header": "Left", "config": { "kind": "group", "collapsed": true } },
                { "id": "member", "kind": "Clock", "parentId": "g", "x": 0.45, "y": 0.2, "w": 0.2, "h": 0.15, "config": { "kind": "clock" } }
              ]
            }
          ]
        }
        """;

    [Fact]
    public void S1900_a_widget_of_a_kind_this_build_lacks_is_dropped_loudly_and_the_rest_is_kept()
    {
        var result = AppStateStore.Parse(RemovedKindsDocument);

        Assert.Equal(ParseRefusal.None, result.Refusal);
        var kept = Assert.Single(result.State!.Views[0].Widgets);
        Assert.Equal("member", kept.Id);
        Assert.Equal(WidgetKind.Clock, kept.Kind);
        Assert.Equal(new FracRect(0.45, 0.2, 0.2, 0.15), kept.Rect);

        Assert.Contains(result.Repairs, r => r.Contains("'calc'", StringComparison.Ordinal) && r.Contains("Calculator", StringComparison.Ordinal));
        Assert.Contains(result.Repairs, r => r.Contains("'g'", StringComparison.Ordinal) && r.Contains("Group", StringComparison.Ordinal));
    }

    [Fact]
    public void S1900_a_file_with_a_removed_kind_loads_rather_than_being_set_aside()
    {
        File.WriteAllText(_path, RemovedKindsDocument);

        var result = new AppStateStore(_path).Load();

        Assert.Equal(LoadOutcome.Loaded, result.Outcome);
        Assert.Contains("Calculator", result.Detail, StringComparison.Ordinal);
        Assert.True(File.Exists(_path));
    }
}
