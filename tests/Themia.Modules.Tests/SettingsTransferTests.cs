using Themia.Core.Geometry;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

public sealed class SettingsTransferTests : IDisposable
{
    private readonly string _dir;

    public SettingsTransferTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "themia-tests", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(_dir);
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

    private static AppState Sample()
    {
        var work = ViewState.Create("Work");
        work.Widgets.Add(WidgetInstance.Create(new WidgetId("clock"), new FracRect(0.1, 0.1, 0.2, 0.2)));
        work.Widgets.Add(WidgetInstance.Create(new WidgetId("cpu"), new FracRect(0.4, 0.1, 0.2, 0.2)));

        var home = ViewState.Create("Home");

        return new AppState
        {
            Views = [work, home],
            ActiveViewId = work.Id,
            MonitorViews = { ["\\\\.\\DISPLAY1"] = work.Id },
        };
    }

    /// <summary>A deep copy, through the same serialiser an export uses.</summary>
    private static AppState Copy(AppState state)
        => AppStateStore.Parse(SettingsTransfer.Serialize(state)).State!;

    private static string[] Displays => ["\\\\.\\DISPLAY1"];

    [Fact]
    public void S494_an_export_reads_back_as_the_same_document()
    {
        var state = Sample();
        var path = Path.Combine(_dir, "export.json");

        Assert.Null(SettingsTransfer.WriteFile(state, path));
        var read = SettingsTransfer.ReadFile(path);

        Assert.NotNull(read.State);
        Assert.Equal(SettingsTransfer.Serialize(state), SettingsTransfer.Serialize(read.State!));
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void S495_an_empty_file_is_refused()
    {
        var path = Path.Combine(_dir, "empty.json");
        File.WriteAllText(path, "  ");

        var read = SettingsTransfer.ReadFile(path);

        Assert.Null(read.State);
        Assert.Equal(ParseRefusal.Empty, read.Refusal);
    }

    [Fact]
    public void S495_a_file_that_is_not_json_is_refused()
    {
        var read = SettingsTransfer.Read("{ not json", "test");

        Assert.Null(read.State);
        Assert.Equal(ParseRefusal.NotJson, read.Refusal);
    }

    [Fact]
    public void S495_another_apps_json_is_refused_rather_than_read_as_a_blank_layout()
    {
        var read = SettingsTransfer.Read("""{ "dock": { "edge": "bottom" } }""", "test");

        Assert.Null(read.State);
        Assert.Equal(ParseRefusal.NotState, read.Refusal);
    }

    [Fact]
    public void S495_settings_from_a_newer_build_are_refused()
    {
        var json = SettingsTransfer.Serialize(Sample())
            .Replace("\"schemaVersion\": 1", $"\"schemaVersion\": {AppState.CurrentSchemaVersion + 1}", StringComparison.Ordinal);

        var read = SettingsTransfer.Read(json, "test");

        Assert.Null(read.State);
        Assert.Equal(ParseRefusal.NewerSchema, read.Refusal);
    }

    [Fact]
    public void S495_a_missing_file_is_refused_not_thrown()
    {
        var read = SettingsTransfer.ReadFile(Path.Combine(_dir, "nope.json"));

        Assert.Null(read.State);
        Assert.Equal(ParseRefusal.Unreadable, read.Refusal);
    }

    [Fact]
    public void S496_the_same_settings_change_nothing()
    {
        var state = Sample();

        var preview = SettingsTransfer.Compare(Locale.En, state, Copy(state), [], Displays);

        Assert.Empty(preview.Changes);
        Assert.Empty(preview.Notes);
    }

    [Fact]
    public void S496_a_renamed_view_is_a_rename_not_a_removal()
    {
        var state = Sample();
        var incoming = Copy(state);
        incoming.Views[0].Name = "Office";

        var preview = SettingsTransfer.Compare(Locale.En, state, incoming, [], Displays);

        var line = Assert.Single(preview.Changes);
        Assert.Contains("Renames", line, StringComparison.Ordinal);
        Assert.Contains("Office", line, StringComparison.Ordinal);
    }

    [Fact]
    public void S496_views_from_another_pc_are_matched_by_name()
    {
        var state = Sample();
        var incoming = Copy(state);

        // Another PC: same names, ids of its own.
        foreach (var view in incoming.Views)
        {
            view.Id = Guid.NewGuid().ToString("n");
        }

        var preview = SettingsTransfer.Compare(Locale.En, state, incoming, [], Displays);

        Assert.DoesNotContain(preview.Changes, c => c.StartsWith("Removes", StringComparison.Ordinal));
        Assert.DoesNotContain(preview.Changes, c => c.StartsWith("Adds", StringComparison.Ordinal));
    }

    [Fact]
    public void S496_added_removed_and_changed_widgets_are_counted_per_view()
    {
        var state = Sample();
        var incoming = Copy(state);
        var work = incoming.Views[0];
        work.Widgets.RemoveAt(1);
        work.Widgets[0].X = 0.5;
        work.Widgets.Add(WidgetInstance.Create(NotesConfig.Descriptor.Id, new FracRect(0.1, 0.5, 0.2, 0.2)));
        work.Widgets.Add(WidgetInstance.Create(new WidgetId("memory"), new FracRect(0.5, 0.5, 0.2, 0.2)));

        var preview = SettingsTransfer.Compare(Locale.En, state, incoming, [], Displays);

        var line = Assert.Single(preview.Changes);
        Assert.Equal("“Work”: 2 widgets added, 1 widget removed, 1 widget changed", line);
    }

    [Fact]
    public void S496_added_and_removed_views_are_named()
    {
        var state = Sample();
        var incoming = Copy(state);
        incoming.Views.RemoveAt(0);
        incoming.Views.Add(ViewState.Create("Travel"));
        incoming.ActiveViewId = incoming.Views[0].Id;

        var preview = SettingsTransfer.Compare(Locale.En, state, incoming, [], Displays);

        Assert.Contains("Adds the view “Travel” with 0 widgets", preview.Changes);
        Assert.Contains("Removes the view “Work” and its 2 widgets", preview.Changes);
    }

    [Fact]
    public void S496_design_changes_are_listed_before_and_after()
    {
        var state = Sample();
        var incoming = Copy(state);
        incoming.Design.Theme = AppTheme.Dark;
        incoming.Design.PrimaryColor = "#FF8800";
        incoming.Design.WidgetGapPx = 12;

        var preview = SettingsTransfer.Compare(Locale.En, state, incoming, [], Displays);

        Assert.Contains("Theme: System → Dark", preview.Changes);
        Assert.Contains("Accent colour: theme → #FF8800", preview.Changes);
        Assert.Contains("Gap between widgets: 8px → 12px", preview.Changes);
    }

    [Fact]
    public void S1100_the_preview_is_worded_in_the_asked_language()
    {
        var state = Sample();
        var incoming = Copy(state);
        incoming.Design.Theme = AppTheme.Dark;

        var preview = SettingsTransfer.Compare(Locale.De, state, incoming, [], Displays);

        var line = Assert.Single(preview.Changes);
        Assert.Equal(
            $"{Strings.Get(Locale.De, Str.Settings_Theme_Label)}: {Strings.Get(Locale.De, Str.Settings_Theme_System)} → {Strings.Get(Locale.De, Str.Settings_Theme_Dark)}",
            line);
    }

    [Fact]
    public void S496_displays_that_are_not_connected_and_repairs_are_notes_not_changes()
    {
        var state = Sample();
        var incoming = Copy(state);
        incoming.MonitorViews["\\\\.\\DISPLAY9"] = incoming.Views[1].Id;

        var preview = SettingsTransfer.Compare(Locale.En, state, incoming, ["widget 'x' was off-screen"], Displays);

        Assert.Empty(preview.Changes);
        Assert.Equal(2, preview.Notes.Count);
        Assert.Contains(preview.Notes, n => n.Contains("isn't connected", StringComparison.Ordinal));
        Assert.Contains(preview.Notes, n => n.Contains("off-screen", StringComparison.Ordinal));
    }

    [Fact]
    public void S497_the_backup_sits_beside_the_state_file_named_for_the_moment()
    {
        var path = SettingsTransfer.BackupPath(
            Path.Combine("C:", "data", "state.json"),
            new DateTime(2026, 10, 4, 9, 5, 7));

        Assert.Equal(Path.Combine("C:", "data", "state.before-import-20261004-090507.json"), path);
    }

    [Fact]
    public void S498_apply_replaces_the_layout_in_place_but_keeps_start_with_windows()
    {
        var state = Sample();
        state.Design.RunAtStartup = true;

        var incoming = new AppState { Views = [ViewState.Create("Imported")] };
        incoming.ActiveViewId = incoming.Views[0].Id;
        incoming.Design.RunAtStartup = false;
        incoming.Design.CornerRadiusPx = 20;

        var before = state;
        SettingsTransfer.Apply(state, incoming);

        Assert.Same(before, state);
        Assert.Equal("Imported", Assert.Single(state.Views).Name);
        Assert.Equal(incoming.ActiveViewId, state.ActiveViewId);
        Assert.Empty(state.MonitorViews);
        Assert.Equal(20, state.Design.CornerRadiusPx);
        Assert.True(state.Design.RunAtStartup);
    }

    [Fact]
    public async Task S499_a_detached_store_never_writes()
    {
        var path = Path.Combine(_dir, "state.json");
        var store = AppStateStore.Detached(path);

        store.SaveSoon(Sample());
        await store.FlushAsync();
        await store.DisposeAsync();

        Assert.False(File.Exists(path));
        Assert.Equal(path, store.Path);
    }
}
