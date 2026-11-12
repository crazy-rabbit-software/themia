#if DEBUG
using Themia.App.Services.Web;
using Themia.App.Shell.Gallery;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Samples;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917], [S-2052]): fixture nowcasts (<c>sample</c>: the sample day's shower from a quarter past eleven, <c>dry</c>, and <c>stale</c>: the sample
/// kept through a failed refresh), so the atom can be
/// looked at without a place, the network or the sync. Built through the SDK's parser, so a fixture
/// is exactly what a reply would have become. Nothing is fetched or saved.
/// </summary>
internal sealed partial class RainNextHourView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "dry", "stale"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (state is not ("sample" or "dry" or "stale"))
        {
            return Task.CompletedTask;
        }

        // A quarter-hour grid around the sample moment: the quarter under way first, then nine more.
        var now = SampleDay.Now;
        var quarter = now.ToUnixTimeSeconds() / 900 * 900;
        IReadOnlyList<double> amounts = state == "dry" ? [0, 0, 0, 0, 0, 0, 0, 0, 0, 0] : SampleWeather.NextQuarters;
        var times = string.Join(',', amounts.Select((_, i) => quarter + (i * 900)));
        var values = string.Join(',', amounts.Select(a => a.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        var hour = now.ToUnixTimeSeconds() / 3600 * 3600;
        var json = "{\"minutely_15\":{\"time\":[" + times + "],\"precipitation\":[" + values + "]},"
            + "\"hourly\":{\"time\":[" + hour + "," + (hour + 3600) + "],\"precipitation_probability\":[" + (state == "dry" ? "5,10" : $"{SampleWeather.Days(1)[0].PrecipitationChance},80") + "]}}";

        _config = new RainNextHourConfig { Latitude = SamplePlace.Latitude, Longitude = SamplePlace.Longitude, LocationName = SamplePlace.LocationName };
        _pinnedNow = now;
        PublishPlace();
        var nowcast = new Fetched<RainNowcast>(FetchStatus.Ok, RainWire.Parse(json).Value, null, SampleDay.MinutesAgo(6));

        // [S-7042] stale: the answer on screen, and the last refresh failed: the header's problem glyph.
        _feed.Preview(state == "stale" ? nowcast.Stale(FetchStatus.Offline, null) : nowcast, FetchStatus.Ok);
        return Task.CompletedTask;
    }
}
#endif
