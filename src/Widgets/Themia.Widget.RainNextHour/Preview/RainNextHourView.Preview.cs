#if DEBUG
using Themia.App.Services.Web;
using Themia.App.Shell.Gallery;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917]): fixture nowcasts (<c>sample</c>, <c>dry</c>), so the ladder can be
/// looked at without a place, the network or the sync. Built through the SDK's parser, so a fixture
/// is exactly what a reply would have become. Nothing is fetched or saved.
/// </summary>
internal sealed partial class RainNextHourView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "dry"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (state is not ("sample" or "dry"))
        {
            return Task.CompletedTask;
        }

        // A quarter-hour grid around now: the quarter under way first, then eight more.
        var now = DateTimeOffset.UtcNow;
        var quarter = now.ToUnixTimeSeconds() / 900 * 900;
        double[] amounts = state == "dry" ? [0, 0, 0, 0, 0, 0, 0, 0, 0, 0] : [0, 0, 0, 0.3, 0.9, 1.6, 1.1, 0.4, 0.1, 0];
        var times = string.Join(',', amounts.Select((_, i) => quarter + (i * 900)));
        var values = string.Join(',', amounts.Select(a => a.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        var hour = now.ToUnixTimeSeconds() / 3600 * 3600;
        var json = "{\"minutely_15\":{\"time\":[" + times + "],\"precipitation\":[" + values + "]},"
            + "\"hourly\":{\"time\":[" + hour + "," + (hour + 3600) + "],\"precipitation_probability\":[" + (state == "dry" ? "5,10" : "65,80") + "]}}";

        _config = new RainNextHourConfig { Latitude = 47.37, Longitude = 8.54, LocationName = "Zürich, Zurich, Switzerland" };
        _applied = (RainNextHourConfig)_config.Clone();
        _want = null;
        _pinnedNow = DateTimeOffset.FromUnixTimeSeconds(quarter + 420);
        _nowcast = new Fetched<RainNowcast>(FetchStatus.Ok, RainWire.Parse(json).Value, null, DateTimeOffset.Now.AddMinutes(-6));
        PublishTitle();
        Render();
        return Task.CompletedTask;
    }
}
#endif
