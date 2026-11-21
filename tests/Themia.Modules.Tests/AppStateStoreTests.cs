using System.Reflection;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Sdk;
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

    private AppStateStore NewStore() => new(_path);

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
        widget.Background = FillChoice.Transparent();
        ((ClockConfig)widget.Config).ShowSeconds = true;
        ((ClockConfig)widget.Config).OffsetMode = ClockOffsetMode.Utc;
        state.Preferences.Region.Clock = ClockHours.Twelve;
        state.Preferences.Refresh["weather"] = 60;
        state.Design.CornerRadiusPx = 12;

        var store = NewStore();
        store.SaveSoon(StateSnapshot.Take(state));
        await store.FlushAsync();

        var loaded = NewStore().Load();

        Assert.Equal(LoadOutcome.Loaded, loaded.Outcome);
        var reloaded = loaded.State.ActiveView().Widgets[0];
        Assert.Equal(new FracRect(0.11, 0.22, 0.33, 0.44), reloaded.Rect);
        Assert.Equal(FillKind.Transparent, reloaded.Background.Kind);
        var config = Assert.IsType<ClockConfig>(reloaded.Config);
        Assert.True(config.ShowSeconds);
        Assert.Equal(ClockOffsetMode.Utc, config.OffsetMode);
        Assert.Equal(ClockHours.Twelve, loaded.State.Preferences.Region.Clock);
        Assert.Equal(60, loaded.State.Preferences.Refresh["weather"]);
        Assert.Equal(12, loaded.State.Design.CornerRadiusPx);
    }

    [Fact]
    public void S12480_a_stored_widget_title_is_dropped_loudly_and_the_widget_loads()
    {
        // [S-12480] A widget has no title of its own (the Header widget is the title): a file that still stores one
        // loads with the key dropped, said in the log (StateDocument.DropUnknownKeys), and never writes it again.
        var state = AppState.CreateDefault();
        var widget = state.ActiveView().Widgets[0];
        var json = System.Text.Json.Nodes.JsonNode.Parse(SettingsTransfer.Serialize(state))!;
        var stored = json["views"]![0]!["widgets"]![0]!.AsObject();
        stored["label"] = "Bedside";

        var parsed = StateDocument.Parse(json.ToJsonString());

        Assert.Equal(ParseRefusal.None, parsed.Refusal);
        Assert.Contains(parsed.Repairs, r => r.Contains($"widget '{widget.Id}'", StringComparison.Ordinal) && r.Contains("dropped the unknown key 'label'", StringComparison.Ordinal));
        Assert.Equal(widget.Rect, parsed.State!.ActiveView().Widgets[0].Rect);
        Assert.DoesNotContain("\"label\"", SettingsTransfer.Serialize(parsed.State), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_save_leaves_no_temp_file_behind()
    {
        var store = NewStore();
        store.SaveSoon(StateSnapshot.Take(AppState.CreateDefault()));
        await store.FlushAsync();

        Assert.Empty(Directory.GetFiles(_dir, "*.tmp"));
    }

    [Fact]
    public void S41_the_store_never_takes_or_keeps_the_live_state()
    {
        // [S-41] The UI thread changes the state while the store writes on the thread pool: the store is handed bytes
        // taken on the owner's thread, so nothing it takes or keeps is a state that thread goes on changing. A save of
        // the live object let a write read a widget list mid-change (one that threw lost the write; one that did not
        // could write a rect half moved).
        var members = typeof(AppStateStore).GetMembers(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

        Assert.Empty(members.OfType<MethodBase>()
            .Where(m => m.GetParameters().Any(p => p.ParameterType == typeof(AppState)))
            .Select(m => m.Name));
        Assert.Empty(members.OfType<FieldInfo>()
            .Where(f => f.FieldType == typeof(AppState))
            .Select(f => f.Name));
    }

    [Fact]
    public async Task S41_a_snapshot_is_the_state_as_it_was_taken()
    {
        var state = AppState.CreateDefault();
        state.Design.CornerRadiusPx = 4;
        var snapshot = StateSnapshot.Take(state);

        // Changed after it was taken: the write is the state as it was then, never what it became.
        state.Design.CornerRadiusPx = 16;
        state.ActiveView().Widgets.Clear();

        var store = NewStore();
        store.SaveSoon(snapshot);
        await store.FlushAsync();

        var loaded = NewStore().Load().State;
        Assert.Equal(4, loaded.Design.CornerRadiusPx);
        Assert.NotEmpty(loaded.ActiveView().Widgets);
    }

    [Fact]
    public async Task Coalesced_saves_write_the_latest_snapshot()
    {
        var store = NewStore();

        for (var i = 1; i <= 20; i++)
        {
            var state = AppState.CreateDefault();
            state.Design.CornerRadiusPx = i;
            store.SaveSoon(StateSnapshot.Take(state));
        }

        await store.FlushAsync();

        Assert.Equal(20, NewStore().Load().State.Design.CornerRadiusPx);
    }

    [Fact]
    public void Validate_repairs_a_dangling_active_view_id()
    {
        var state = AppState.CreateDefault();
        state.ActiveViewId = "does-not-exist";

        var repairs = StateRepair.Repair(state);

        Assert.Contains(repairs, r => r.Contains("active view"));
        Assert.Equal(state.Views[0].Id, state.ActiveViewId);
    }

    [Fact]
    public void Validate_drops_a_monitor_assignment_pointing_at_a_deleted_view()
    {
        var state = AppState.CreateDefault();
        state.MonitorViews["\\\\.\\DISPLAY1"] = "deleted-view";

        StateRepair.Repair(state);

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

        StateRepair.Repair(state);

        Assert.Equal(view.Widgets.Count, view.Widgets.Select(w => w.Id).Distinct().Count());
    }

    [Fact]
    public void S6908_Validate_drops_a_widget_without_a_config_since_it_has_no_kind()
    {
        var state = AppState.CreateDefault();
        var widget = state.ActiveView().Widgets[0];
        widget.Config = null!;

        var repairs = StateRepair.Repair(state);

        Assert.DoesNotContain(widget, state.ActiveView().Widgets);
        Assert.Contains(repairs, r => r.Contains($"'{widget.Id}'", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_S6608_pulls_an_off_screen_widget_back_into_reach()
    {
        var state = AppState.CreateDefault();
        var widget = state.ActiveView().Widgets[0];
        widget.Rect = new FracRect(5, -5, 0.2, 0.2);

        StateRepair.Repair(state);

        // Against the 1920 × 1080 reference screen: the visible minimum stays on it, no more.
        Assert.Equal(1 - (GridGeometry.MinVisiblePx / 1920), widget.Rect.X, 9);
        Assert.Equal(GridGeometry.MinVisiblePx / 1080, widget.Rect.Bottom, 9);
        Assert.Equal(0.2, widget.Rect.W, 9);
    }

    [Fact]
    public void Validate_S6608_leaves_a_widget_hanging_over_the_edge_where_it_is()
    {
        var state = AppState.CreateDefault();
        var widget = state.ActiveView().Widgets[0];
        var overhanging = new FracRect(-0.1, 0.9, 0.2, 0.2);
        widget.Rect = overhanging;

        StateRepair.Repair(state);

        Assert.Equal(overhanging, widget.Rect);
    }

    [Fact]
    public void S2330_an_over_the_edge_key_left_in_a_file_is_dropped_loudly_and_the_design_loads()
    {
        // [S-1050] The schema changed in place (owner, 2026-11-20: no Over the edge setting any more): the key goes, one
        // line, and everything else in the design is read as it was.
        var state = AppState.CreateDefault();
        state.Design.FontScale = 1.25;
        var root = System.Text.Json.Nodes.JsonNode.Parse(SettingsTransfer.Serialize(state))!;
        root["design"]!.AsObject()["overEdge"] = false;

        var read = StateDocument.Parse(root.ToJsonString());

        Assert.NotNull(read.State);
        Assert.Equal(1.25, read.State!.Design.FontScale);
        Assert.Contains(read.Repairs, r => r.Contains("design", StringComparison.Ordinal) && r.Contains("unknown key 'overEdge'", StringComparison.Ordinal));
        Assert.DoesNotContain("overEdge", SettingsTransfer.Serialize(read.State), StringComparison.Ordinal);
    }

    [Fact]
    public void S1050_a_keep_apart_key_left_in_a_file_is_dropped_loudly_and_the_design_loads()
    {
        // [S-1050] The schema changed in place (owner, 2026-11-21: "keep widgets from overlapping should not be a selectable
        // option anymore"): the key goes, one line, wherever it was left (the design, or the preferences it once moved to).
        var state = AppState.CreateDefault();
        state.Design.FontScale = 1.25;
        var root = System.Text.Json.Nodes.JsonNode.Parse(SettingsTransfer.Serialize(state))!;
        root["design"]!.AsObject()["widgetCollisions"] = true;
        root["preferences"]!.AsObject()["widgetCollisions"] = true;

        var read = StateDocument.Parse(root.ToJsonString());

        Assert.NotNull(read.State);
        Assert.Equal(1.25, read.State!.Design.FontScale);
        Assert.Contains(read.Repairs, r => r.Contains("design", StringComparison.Ordinal) && r.Contains("unknown key 'widgetCollisions'", StringComparison.Ordinal));
        Assert.Contains(read.Repairs, r => r.Contains("preferences", StringComparison.Ordinal) && r.Contains("unknown key 'widgetCollisions'", StringComparison.Ordinal));
        Assert.DoesNotContain("widgetCollisions", SettingsTransfer.Serialize(read.State), StringComparison.Ordinal);
    }

    [Fact]
    public void S1050_a_design_written_by_this_build_loads_with_no_repair()
        => Assert.Empty(StateDocument.Parse(SettingsTransfer.Serialize(AppState.CreateDefault())).Repairs);

    [Fact]
    public void Validate_clamps_nonsense_design_values()
    {
        var state = AppState.CreateDefault();
        state.Design.Background = FillChoice.Of("#202020", -4);
        state.Design.Background.Opacity = -4;
        state.Design.CornerRadiusPx = 9999;

        StateRepair.Repair(state);

        Assert.InRange(state.Design.Background.Opacity, 0, 1.0);
        Assert.InRange(state.Design.CornerRadiusPx, 0, 32);
    }

    [Fact]
    public void S6482_a_corner_radius_that_is_not_a_number_loads_as_the_8px_default()
    {
        var state = AppState.CreateDefault();
        state.Design.CornerRadiusPx = double.NaN;

        StateRepair.Repair(state);

        Assert.Equal(8, state.Design.CornerRadiusPx);
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
    /// [S-1055] A hand-edited file whose closed sets hold words this build has no member for: the design's border, the
    /// preferences' language, a view bar's position, a widget's alignment, a clock's offset.
    /// </summary>
    private const string UnknownNamesDocument = """
        {
          "schemaVersion": 1,
          "activeViewId": "v",
          "design": { "borderStyle": "wavy", "fontScale": 1.25 },
          "preferences": { "language": "klingon", "lockWidgets": true },
          "viewBars": { "A": { "anchor": "middle", "hidden": true } },
          "views": [
            {
              "id": "v",
              "name": "Desk",
              "widgets": [
                { "id": "c", "x": 0.1, "y": 0.1, "w": 0.2, "h": 0.2, "align": "diagonal", "config": { "kind": "clock" } },
                { "id": "d", "x": 0.4, "y": 0.1, "w": 0.2, "h": 0.2, "config": { "kind": "clock", "offsetMode": "sideways" } }
              ]
            }
          ]
        }
        """;

    [Fact]
    public void S1055_an_unknown_enum_name_is_its_default_loudly_and_the_rest_of_the_file_loads()
    {
        // Regression: one word a closed set does not have refused the whole file, and the user got a default desktop.
        File.WriteAllText(_path, UnknownNamesDocument);

        var result = new AppStateStore(_path).Load();

        Assert.Equal(LoadOutcome.Loaded, result.Outcome);
        var state = result.State;
        Assert.Equal(["c", "d"], state.Views.Single().Widgets.Select(w => w.Id));
        Assert.Equal(WidgetBorderStyle.Solid, state.Design.BorderStyle);
        Assert.Equal(1.25, state.Design.FontScale);
        Assert.Equal(AppLanguage.System, state.Preferences.Language);
        Assert.True(state.Preferences.LockWidgets);
        Assert.Equal(ViewBarAnchor.BottomLeft, state.ViewBars["A"].Anchor);
        Assert.True(state.ViewBars["A"].Hidden);
        Assert.Null(state.Views[0].Widgets[0].Align);
        Assert.Equal(ClockOffsetMode.None, Assert.IsType<ClockConfig>(state.Views[0].Widgets[1].Config).OffsetMode);

        var parsed = StateDocument.Parse(UnknownNamesDocument);
        foreach (var word in new[] { "wavy", "klingon", "middle", "diagonal", "sideways" })
        {
            Assert.Single(parsed.Repairs, r => r.Contains($"\"{word}\"", StringComparison.Ordinal));
        }
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
        var result = StateDocument.Parse(RemovedKindsDocument);

        Assert.Equal(ParseRefusal.None, result.Refusal);
        var kept = Assert.Single(result.State!.Views[0].Widgets);
        Assert.Equal("member", kept.Id);
        Assert.Equal(new WidgetId("clock"), kept.Kind);
        Assert.Equal(new FracRect(0.45, 0.2, 0.2, 0.15), kept.Rect);

        Assert.Contains(result.Repairs, r => r.Contains("'calc'", StringComparison.Ordinal) && r.Contains("'calculator'", StringComparison.Ordinal));
        Assert.Contains(result.Repairs, r => r.Contains("'g'", StringComparison.Ordinal) && r.Contains("'group'", StringComparison.Ordinal));
    }

    [Fact]
    public void S1900_a_file_with_a_removed_kind_loads_rather_than_being_set_aside()
    {
        File.WriteAllText(_path, RemovedKindsDocument);

        var result = new AppStateStore(_path).Load();

        Assert.Equal(LoadOutcome.Loaded, result.Outcome);
        Assert.Contains("'calculator'", result.Detail, StringComparison.Ordinal);
        Assert.True(File.Exists(_path));
    }
}
