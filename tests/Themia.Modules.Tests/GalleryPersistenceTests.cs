using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>
/// [S-305] The gallery's settings through the real store: the state file is the only copy of them, so
/// the round trip is pinned rather than assumed (under Native AOT a nested type the source generator
/// never learned about is not a compile error but a widget that comes back empty on the next launch).
/// The config is the gallery module's, so it runs here. Moved from Core's TrackerPersistenceTests
/// (the habit tracker's to HabitHostTests), unchanged in what it asserts.
/// </summary>
public sealed class GalleryPersistenceTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;

    public GalleryPersistenceTests()
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
    private async Task<T> RoundTripAsync<T>(WidgetId kind, Action<T> fill)
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
        var config = await RoundTripAsync<GalleryConfig>(new WidgetId("gallery"), gallery =>
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

}
