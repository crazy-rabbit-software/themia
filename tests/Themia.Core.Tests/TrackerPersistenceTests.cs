using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// The trackers keep their data <b>in their config</b> (habits keep their settings there and their
/// history in <c>habits.json</c>), which means the state file is the only copy of every mood ever
/// logged. So the round trip through the real store is pinned here rather than assumed.
/// <para>
/// It matters more than for a widget whose config is a handful of switches: this is the test that
/// catches a nested list the source-generated serializer never learned about, which under Native AOT
/// is not a compile error but a widget that silently comes back empty on the next launch.
/// </para>
/// </summary>
public sealed class TrackerPersistenceTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;

    public TrackerPersistenceTests()
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

    /// <summary>Save a widget of this kind with its config filled in, and read it back.</summary>
    private async Task<T> RoundTripAsync<T>(WidgetKind kind, Action<T> fill)
        where T : WidgetConfig
    {
        var state = AppState.CreateDefault();
        var view = state.ActiveView();
        var widget = WidgetInstance.Create(kind, new FracRect(0.1, 0.1, 0.2, 0.2));

        fill((T)widget.Config);
        view.Widgets.Add(widget);

        var store = NewStore();
        store.SaveSoon(state);
        await store.FlushAsync();

        var loaded = NewStore().Load();

        Assert.Equal(LoadOutcome.Loaded, loaded.Outcome);

        var reloaded = loaded.State.ActiveView().Widgets.Single(w => w.Kind == kind);
        return Assert.IsType<T>(reloaded.Config);
    }

    // -----------------------------------------------------------------------------------------
    // [S-305] gallery
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task S305_a_gallery_keeps_every_setting_across_a_restart()
    {
        var config = await RoundTripAsync<GalleryConfig>(WidgetKind.Gallery, gallery =>
        {
            gallery.Path = @"C:\Users\me\Pictures";
            gallery.Recursive = true;
            gallery.Style = GalleryStyle.Grid;
            gallery.Fit = GalleryFit.Fit;
            gallery.Shuffle = false;
            gallery.SlideshowSeconds = 45;
            gallery.MaxImages = 33;
        });

        Assert.Equal(@"C:\Users\me\Pictures", config.Path);
        Assert.True(config.Recursive);
        Assert.Equal(GalleryStyle.Grid, config.Style);
        Assert.Equal(GalleryFit.Fit, config.Fit);
        Assert.False(config.Shuffle);
        Assert.Equal(45, config.SlideshowSeconds);
        Assert.Equal(33, config.MaxImages);
    }

    // -----------------------------------------------------------------------------------------
    // [S-1420] habits: the config is a pointer and the widget's own settings; the habits live in
    // habits.json (HabitListStoreTests)
    // -----------------------------------------------------------------------------------------

    [Fact]
    public async Task S1420_a_habit_widgets_settings_survive_a_restart()
    {
        var config = await RoundTripAsync<HabitTrackerConfig>(WidgetKind.HabitTracker, habits =>
        {
            habits.ListId = "list-morning";
            habits.Period = HabitPeriod.Custom;
            habits.CustomMinutes = 45;
            habits.HistoryDots = 14;
            habits.ShowStreak = false;
        });

        Assert.Equal("list-morning", config.ListId);
        Assert.Equal(HabitPeriod.Custom, config.Period);
        Assert.Equal(45, config.CustomMinutes);
        Assert.Equal(14, config.HistoryDots);
        Assert.False(config.ShowStreak);
    }

    [Fact]
    public void S1422_a_hand_edited_habit_config_is_repaired_to_its_ranges()
    {
        var config = new HabitTrackerConfig { Period = (HabitPeriod)99, CustomMinutes = 0, HistoryDots = 500 };

        var repairs = config.Repair();

        Assert.Equal(3, repairs.Count);
        Assert.Equal(HabitPeriod.Daily, config.Period);
        Assert.Equal(HabitCadence.MinCustomMinutes, config.CustomMinutes);
        Assert.Equal(HabitRules.MaxHistory, config.HistoryDots);
    }
}
