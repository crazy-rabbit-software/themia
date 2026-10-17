using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Storage;
using Themia.Sdk;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// Put one widget of a kind with a config into a real state file, save it through the store and
/// read it back: where a list with a populated default can quietly double on every load, and a
/// drawing be quietly lost. Shared by both test projects (a moved widget's tests round-trip as they did in Core).
/// </summary>
internal static class StateRoundTrip
{
    public static async Task<T> Of<T>(WidgetId kind, T config)
        where T : WidgetConfig
    {
        var dir = Path.Combine(Path.GetTempPath(), "themia-tests", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "state.json");
            var state = AppState.CreateDefault();
            var view = state.ActiveView();
            var widget = WidgetInstance.Create(kind, new FracRect(0.1, 0.1, 0.3, 0.3));
            widget.Config = config;

            view.Widgets.Clear();
            view.Widgets.Add(widget);

            var store = new AppStateStore(path, TimeSpan.Zero);
            store.SaveSoon(state);
            await store.FlushAsync();

            var loaded = new AppStateStore(path, TimeSpan.Zero).Load();
            Assert.Equal(LoadOutcome.Loaded, loaded.Outcome);

            return Assert.IsType<T>(loaded.State.ActiveView().Widgets[0].Config);
        }
        finally
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch (IOException)
            {
                // ignore: temp cleanup is best effort and must not fail a test run
            }
        }
    }
}
