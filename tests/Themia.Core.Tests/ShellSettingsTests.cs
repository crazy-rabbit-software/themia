using System.Text.RegularExpressions;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Storage;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// The shell settings added ahead of the shell packages ([S-700..S-705]): header defaults per kind,
/// the new design, preference and display fields, and the repairs a corrupt or hand-edited file gets.
/// </summary>
public sealed class ShellSettingsTests : IDisposable
{
    /// <summary>A small valid state file that the repair tests edit into a corrupt one.</summary>
    private const string StateFile = """
        {
          "schemaVersion": 1,
          "views": [
            {
              "id": "v1",
              "name": "Desktop",
              "widgets": [
                { "id": "w1", "kind": "Clock", "x": 0.1, "y": 0.1, "w": 0.2, "h": 0.2, "config": { "kind": "clock" } }
              ]
            }
          ],
          "activeViewId": "v1",
          "monitorViews": {},
          "design": { "widgetGapPx": 8, "introAnimation": "Fade", "introAnimationMs": 400 }
        }
        """;

    private readonly string _dir;
    private readonly string _path;

    public ShellSettingsTests()
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

    private static AppState Parse(string json)
    {
        var result = AppStateStore.Parse(json);
        Assert.Equal(ParseRefusal.None, result.Refusal);
        return result.State!;
    }

    // -----------------------------------------------------------------------------------------
    // [S-700] Header defaults
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void S700_the_clock_starts_without_a_header()
        => Assert.Equal(HeaderPlacement.Hidden, WidgetInstance.Create(WidgetKind.Clock, new FracRect(0, 0, 0.1, 0.1)).Header);

    [Theory]
    [InlineData(WidgetKind.Sun)]
    [InlineData(WidgetKind.Moon)]
    [InlineData(WidgetKind.Disk)]
    public void S700_the_sun_the_moon_and_the_disk_meter_start_without_a_header(WidgetKind kind)
        => Assert.Equal(HeaderPlacement.Hidden, WidgetInstance.Create(kind, new FracRect(0, 0, 0.1, 0.1)).Header);

    [Theory]
    [InlineData(WidgetKind.Cpu)]
    [InlineData(WidgetKind.Notes)]
    [InlineData(WidgetKind.Countdown)]
    [InlineData(WidgetKind.Pomodoro)]
    public void S700_other_widgets_still_start_with_a_top_header(WidgetKind kind)
        => Assert.Equal(HeaderPlacement.Top, WidgetInstance.Create(kind, new FracRect(0, 0, 0.1, 0.1)).Header);

    [Fact]
    public void S700_a_saved_clock_keeps_the_header_it_was_saved_with()
    {
        var state = Parse(StateFile.Replace("\"kind\": \"Clock\",", "\"kind\": \"Clock\", \"header\": \"Top\",", StringComparison.Ordinal));

        Assert.Equal(HeaderPlacement.Top, state.Views[0].Widgets[0].Header);
    }

    // -----------------------------------------------------------------------------------------
    // [S-702..S-705] The new fields
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void S702_a_hand_edited_null_section_is_repaired_to_its_defaults()
    {
        var json = StateFile.Replace(
            "\"monitorViews\": {},",
            "\"monitorViews\": {}, \"preferences\": null, \"disabledMonitors\": null, \"monitorScales\": null,",
            StringComparison.Ordinal);

        var state = Parse(json);

        Assert.NotNull(state.Preferences);
        Assert.True(state.Preferences.DesktopIconsVisible);
        Assert.Empty(state.DisabledMonitors);
        Assert.Empty(state.MonitorScales);
    }

    [Fact]
    public async Task S702_every_new_field_survives_a_save_and_load()
    {
        var state = AppState.CreateDefault();
        var view = state.Views[0];
        view.Icon = "\uE80F";
        state.Design.HeaderBackgroundColor = "#112233";
        state.Design.HeaderOpacity = 0.4;
        state.Design.IntroAnimation = IntroAnimation.Hyperspace;
        state.Design.TransitionAnimation = IntroAnimation.Hacker;
        state.Design.TransitionAnimationMs = 700;
        state.Design.UnlockAnimation = IntroAnimation.None;
        state.Design.UnlockAnimationMs = 900;
        state.Preferences.Language = AppLanguage.Japanese;
        state.Preferences.GameModeSuspend = true;
        state.Preferences.DesktopIconsVisible = false;
        view.Color = "#3366CC";
        state.DefaultViewForNewDisplays = view.Id;
        state.DisabledMonitors.Add(@"\\.\DISPLAY2");
        state.MonitorScales[@"\\.\DISPLAY1"] = 1.5;

        await using (var store = new AppStateStore(_path, TimeSpan.Zero))
        {
            store.SaveSoon(state);
            await store.FlushAsync();
        }

        var loaded = new AppStateStore(_path, TimeSpan.Zero).Load();
        Assert.Equal(LoadOutcome.Loaded, loaded.Outcome);
        var back = loaded.State;

        Assert.Equal("\uE80F", back.Views[0].Icon);
        Assert.Equal("#112233", back.Design.HeaderBackgroundColor);
        Assert.Equal(0.4, back.Design.HeaderOpacity);
        Assert.Equal(IntroAnimation.Hyperspace, back.Design.IntroAnimation);
        Assert.Equal(IntroAnimation.Hacker, back.Design.TransitionAnimation);
        Assert.Equal(700, back.Design.TransitionAnimationMs);
        Assert.Equal(IntroAnimation.None, back.Design.UnlockAnimation);
        Assert.Equal(900, back.Design.UnlockAnimationMs);
        Assert.Equal(AppLanguage.Japanese, back.Preferences.Language);
        Assert.True(back.Preferences.GameModeSuspend);
        Assert.False(back.Preferences.DesktopIconsVisible);
        Assert.Equal("#3366CC", back.Views[0].Color);
        Assert.Equal(view.Id, back.DefaultViewForNewDisplays);
        Assert.Equal([@"\\.\DISPLAY2"], back.DisabledMonitors);
        Assert.Equal(1.5, back.MonitorScales[@"\\.\DISPLAY1"]);
    }

    [Fact]
    public void S702_design_clone_carries_the_new_fields()
    {
        var design = new DesignState
        {
            HeaderBackgroundColor = "#ABCDEF",
            HeaderOpacity = 0.3,
            TransitionAnimation = IntroAnimation.Flip,
            TransitionAnimationMs = 120,
            UnlockAnimation = IntroAnimation.Blur,
            UnlockAnimationMs = 240,
        };

        var copy = design.Clone();

        Assert.Equal("#ABCDEF", copy.HeaderBackgroundColor);
        Assert.Equal(0.3, copy.HeaderOpacity);
        Assert.Equal(IntroAnimation.Flip, copy.TransitionAnimation);
        Assert.Equal(120, copy.TransitionAnimationMs);
        Assert.Equal(IntroAnimation.Blur, copy.UnlockAnimation);
        Assert.Equal(240, copy.UnlockAnimationMs);
    }

    // -----------------------------------------------------------------------------------------
    // Repairs at load
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void S704_out_of_range_design_values_are_clamped_or_reset()
    {
        var json = StateFile.Replace(
            "\"introAnimationMs\": 400",
            "\"introAnimationMs\": 400, \"headerBackgroundColor\": \"not a colour\", \"headerOpacity\": 5, "
            + "\"transitionAnimationMs\": -10, \"unlockAnimationMs\": 99999, \"transitionAnimation\": 42",
            StringComparison.Ordinal);

        var design = Parse(json).Design;

        Assert.Equal(string.Empty, design.HeaderBackgroundColor);
        Assert.Equal(1, design.HeaderOpacity);
        // [S-1000] The offered range is 100..5000 ms, the original's stepper.
        Assert.Equal(100, design.TransitionAnimationMs);
        Assert.Equal(5000, design.UnlockAnimationMs);
        Assert.Equal(IntroAnimation.Scale, design.TransitionAnimation);
    }

    [Fact]
    public void S702_an_unknown_language_number_falls_back_to_following_windows()
    {
        var json = StateFile.Replace(
            "\"monitorViews\": {},",
            "\"monitorViews\": {}, \"preferences\": { \"language\": 99, \"gameModeSuspend\": true },",
            StringComparison.Ordinal);

        var result = AppStateStore.Parse(json);

        Assert.Equal(AppLanguage.System, result.State!.Preferences.Language);
        Assert.True(result.State.Preferences.GameModeSuspend);
        Assert.Contains(result.Repairs, r => r.Contains("language", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("\"\"")]
    [InlineData("\" \"")]
    [InlineData("\"Home\"")]
    public void S703_a_view_icon_that_is_not_one_glyph_falls_back_to_initials(string icon)
    {
        var json = StateFile.Replace("\"name\": \"Desktop\",", $"\"name\": \"Desktop\", \"icon\": {icon},", StringComparison.Ordinal);

        var result = AppStateStore.Parse(json);

        Assert.Null(result.State!.Views[0].Icon);
        Assert.Contains(result.Repairs, r => r.Contains("icon", StringComparison.Ordinal));
    }

    [Fact]
    public void S705_display_settings_are_repaired_against_the_views_and_the_scale_range()
    {
        var json = StateFile.Replace(
            "\"monitorViews\": {},",
            "\"monitorViews\": {}, \"defaultViewForNewDisplays\": \"deleted\", "
            + "\"disabledMonitors\": [\"A\", \"A\", \"\", \"B\"], \"monitorScales\": { \"A\": 9, \"B\": 0.1, \"C\": 1.25 },",
            StringComparison.Ordinal);

        var state = Parse(json);

        Assert.Null(state.DefaultViewForNewDisplays);
        Assert.Equal(["A", "B"], state.DisabledMonitors);
        Assert.Equal(AppStateStore.MaxMonitorScale, state.MonitorScales["A"]);
        Assert.Equal(AppStateStore.MinMonitorScale, state.MonitorScales["B"]);
        Assert.Equal(1.25, state.MonitorScales["C"]);
    }

    [Fact]
    public void S705_a_default_view_that_exists_is_kept()
    {
        var json = StateFile.Replace("\"monitorViews\": {},", "\"monitorViews\": {}, \"defaultViewForNewDisplays\": \"v1\",", StringComparison.Ordinal);

        Assert.Equal("v1", Parse(json).DefaultViewForNewDisplays);
    }

    // -----------------------------------------------------------------------------------------
    // Import
    // -----------------------------------------------------------------------------------------

    [Fact]
    public void S702_an_import_brings_the_preferences_and_display_settings_and_says_so()
    {
        var current = AppState.CreateDefault();
        var incoming = AppState.CreateDefault();
        incoming.Preferences.GameModeSuspend = true;
        incoming.Design.UnlockAnimationMs = 900;
        incoming.DisabledMonitors.Add("X");
        incoming.MonitorScales["X"] = 0.75;

        var preview = SettingsTransfer.Compare(current, incoming, [], []);

        Assert.Contains(preview.Changes, c => c.StartsWith("Pause widgets during full-screen apps", StringComparison.Ordinal));
        Assert.Contains(preview.Changes, c => c.StartsWith("Unlock animation length", StringComparison.Ordinal));

        SettingsTransfer.Apply(current, incoming);

        Assert.True(current.Preferences.GameModeSuspend);
        Assert.Equal(900, current.Design.UnlockAnimationMs);
        Assert.Equal(["X"], current.DisabledMonitors);
        Assert.Equal(0.75, current.MonitorScales["X"]);
    }

    [Fact]
    public void S702_saved_json_names_the_new_fields_in_camel_case()
    {
        // The file is meant to be readable and hand-editable; pin the spelling the repairs expect.
        var json = SettingsTransfer.Serialize(AppState.CreateDefault());

        Assert.Matches(new Regex("\"preferences\"\\s*:"), json);
        Assert.Matches(new Regex("\"headerOpacity\"\\s*:"), json);
        Assert.Matches(new Regex("\"monitorScales\"\\s*:"), json);
    }
}
