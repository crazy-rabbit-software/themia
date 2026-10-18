using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-44] The local-store widgets' settings as users have them, generated once with the build from
/// before they became projects (`d02d36bf`; Fixtures/state-local-configured.json, ids w00.., not
/// anyone's real file): the note shown and its text size, two habit trackers (list, style, period,
/// custom minutes, history, streak), the clipboard's capacity and timestamps, an alarm (time, days,
/// label, sound, volume, snooze), the focus session's length and do not disturb, the three screen
/// time widgets' periods, style and figures, a gallery folder and a gallery sticker, notifications.
/// Each widget reads it from its own module's JSON context once it moves; this pins that the file
/// still loads with no repair, reads the same values and writes back unchanged.
/// </summary>
public sealed class LocalStateFixtureTests
{
    private static string Fixture([CallerFilePath] string here = "")
        => File.ReadAllText(Path.Combine(Path.GetDirectoryName(here)!, "Fixtures", "state-local-configured.json"));

    [Fact]
    public void S44_the_local_widgets_configured_state_loads_without_a_repair_and_writes_back_unchanged()
    {
        var json = Fixture();
        var parsed = AppStateStore.Parse(json);

        Assert.NotNull(parsed.State);
        Assert.Empty(parsed.Repairs);
        var written = JsonSerializer.Serialize(parsed.State, StateJson.AppState);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(json), JsonNode.Parse(written)), "the local widgets' state does not write back as it was read");
    }

    [Fact]
    public void S44_the_local_widgets_configured_state_reads_the_choices_it_holds()
    {
        var state = AppStateStore.Parse(Fixture()).State!;
        var configs = Assert.Single(state.Views).Widgets.Select(w => w.Config).ToList();
        Assert.Equal(12, configs.Count);

        var notes = Assert.IsType<NotesConfig>(configs[0]);
        Assert.Equal(("1a2b3c4d5e6f708192a3b4c5d6e7f809", 16d), (notes.NoteId, notes.FontSizePx));
        var weekly = Assert.IsType<HabitTrackerConfig>(configs[1]);
        Assert.Equal(("list-weekly", HabitTrackerStyle.Heatmap, HabitPeriod.Weekly, 12, false), (weekly.ListId, weekly.Style, weekly.Period, weekly.HistoryDots, weekly.ShowStreak));
        var custom = Assert.IsType<HabitTrackerConfig>(configs[2]);
        Assert.Equal(("list-morning", HabitTrackerStyle.Checklist, HabitPeriod.Custom, 90), (custom.ListId, custom.Style, custom.Period, custom.CustomMinutes));
        var clipboard = Assert.IsType<ClipboardConfig>(configs[3]);
        Assert.Equal((120, false), (clipboard.Capacity, clipboard.ShowTimestamp));
        var alarm = Assert.IsType<AlarmConfig>(configs[4]);
        Assert.Equal(("07:30", true, "Gym 🏋", AlarmSound.Marimba, 40, 5), (alarm.Time, alarm.On, alarm.Label, alarm.Sound, alarm.Volume, alarm.SnoozeMinutes));
        Assert.Equal(["mon", "wed", "fri"], alarm.Days!);
        var focus = Assert.IsType<FocusSessionConfig>(configs[5]);
        Assert.Equal((50, false), (focus.Minutes, focus.DoNotDisturb));
        var chart = Assert.IsType<ScreenTimeChartConfig>(configs[6]);
        Assert.Equal((ScreenTimePeriod.Month, ScreenTimeChartStyle.Heatmap), (chart.Period, chart.Style));
        Assert.Equal(ScreenTimePeriod.Week, Assert.IsType<ScreenTimeDonutConfig>(configs[7]).Period);
        var apps = Assert.IsType<ScreenTimeAppsConfig>(configs[8]);
        Assert.Equal((ScreenTimePeriod.Month, 8, false, true), (apps.Period, apps.MaxApps, apps.ShowAverage, apps.ShowPercent));
        var folder = Assert.IsType<GalleryConfig>(configs[9]);
        Assert.Equal((GallerySource.Folder, @"D:\Pictures\Holidays", true, GalleryStyle.Mosaic, GalleryFit.Fit, false, 30, 12),
            (folder.Source, folder.Path, folder.Recursive, folder.Style, folder.Fit, folder.Shuffle, folder.SlideshowSeconds, folder.MaxImages));
        var sticker = Assert.IsType<GalleryConfig>(configs[10]);
        Assert.Equal((GallerySource.File, @"D:\Pictures\logo.png", -12, true, "#FFCC00", 8),
            (sticker.Source, sticker.FilePath, sticker.Rotation, sticker.Outline, sticker.OutlineColor, sticker.OutlineWidth));
        Assert.IsType<NotificationsConfig>(configs[11]);
    }
}
