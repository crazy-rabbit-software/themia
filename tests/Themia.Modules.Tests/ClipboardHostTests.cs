using Themia.Core.Geometry;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Core.Tests;
using Themia.Core.Widgets;
using Themia.Sdk;
using Xunit;

namespace Themia.Modules.Tests;

/// <summary>[S-1458] The host's one clipboard history under the configs that ask for it, and the clipboard's settings through the real persistence path (no history reaches the disk): the config is the clipboard module's, so they run here. Moved from Core's TrackersLayoutTests and LauncherPersistenceTests, unchanged in what they assert.</summary>
public sealed class ClipboardHostTests : IDisposable
{
    private readonly string _dir;
    private readonly string _path;

    public ClipboardHostTests()
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

    private async Task<WidgetConfig> RoundTripAsync(WidgetConfig config)
    {
        var state = AppState.CreateDefault();
        var widget = state.ActiveView().Widgets[0];

        widget.Config = config;

        var store = NewStore();
        store.SaveSoon(state);
        await store.FlushAsync();

        var loaded = NewStore().Load();

        Assert.Equal(LoadOutcome.Loaded, loaded.Outcome);
        return loaded.State.ActiveView().Widgets[0].Config;
    }

    /// <summary>[S-1458] The history is kept while any view holds a Clipboard widget, on screen or not, at the longest capacity asked for.</summary>
    [Fact]
    public void S1458_the_history_is_wanted_while_any_view_holds_a_clipboard_widget()
    {
        var state = AppState.CreateDefault();
        foreach (var view in state.Views)
        {
            view.Widgets.Clear();
        }

        Assert.Null(ClipboardInterest.Capacity(state));

        // A clipboard widget on a view that is not the one on screen still wants the history.
        var hidden = ViewState.Create("Hidden");
        var small = WidgetInstance.Create(new WidgetId("clipboard"), new FracRect(0, 0, 0.2, 0.2));
        ((ClipboardConfig)small.Config).Capacity = 10;
        hidden.Widgets.Add(small);
        state.Views.Add(hidden);
        Assert.Equal(10, ClipboardInterest.Capacity(state));

        var large = WidgetInstance.Create(new WidgetId("clipboard"), new FracRect(0, 0, 0.2, 0.2));
        ((ClipboardConfig)large.Config).Capacity = 120;
        state.Views[0].Widgets.Add(large);
        Assert.Equal(120, ClipboardInterest.Capacity(state));

        // Any other widget wants nothing of the clipboard.
        hidden.Widgets.Clear();
        state.Views[0].Widgets.Clear();
        state.Views[0].Widgets.Add(WidgetInstance.Create(new WidgetId("notes"), new FracRect(0, 0, 0.2, 0.2)));
        Assert.Null(ClipboardInterest.Capacity(state));
    }

    [Fact]
    public async Task The_clipboard_settings_survive_a_round_trip_and_carry_no_history()
    {
        var config = new ClipboardConfig { Capacity = 40, ShowTimestamp = false };

        var reloaded = Assert.IsType<ClipboardConfig>(await RoundTripAsync(config));

        Assert.Equal(40, reloaded.Capacity);
        Assert.False(reloaded.ShowTimestamp);

        // The point of the widget's design, stated as a test: nothing that was copied reaches the
        // disk, so the saved config has only these two numbers in it.
        var json = File.ReadAllText(_path);

        Assert.DoesNotContain("history", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("entries", json, StringComparison.OrdinalIgnoreCase);
    }
}
