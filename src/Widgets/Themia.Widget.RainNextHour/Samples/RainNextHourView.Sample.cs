using Themia.App.Services.Web;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Samples;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11850] [S-2052] The nowcast's sample: the sample day's shower from a quarter past eleven, built
/// through the SDK's parser, so it is exactly what a reply would have become. Nothing is fetched or saved.
/// </summary>
internal sealed partial class RainNextHourView : IWidgetSample
{
    public Task ShowSampleAsync()
    {
        ShowNowcast(dry: false, stale: false);
        return Task.CompletedTask;
    }

    /// <summary>A nowcast at the sample place and moment, dry or the sample's shower, kept through a failed refresh when <paramref name="stale"/>.</summary>
    internal void ShowNowcast(bool dry, bool stale)
    {
        // A quarter-hour grid around the sample moment: the quarter under way first, then nine more.
        var now = SampleDay.Now;
        var quarter = now.ToUnixTimeSeconds() / 900 * 900;
        IReadOnlyList<double> amounts = dry ? [0, 0, 0, 0, 0, 0, 0, 0, 0, 0] : SampleWeather.NextQuarters;
        var times = string.Join(',', amounts.Select((_, i) => quarter + (i * 900)));
        var values = string.Join(',', amounts.Select(a => a.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        var hour = now.ToUnixTimeSeconds() / 3600 * 3600;
        var json = "{\"minutely_15\":{\"time\":[" + times + "],\"precipitation\":[" + values + "]},"
            + "\"hourly\":{\"time\":[" + hour + "," + (hour + 3600) + "],\"precipitation_probability\":[" + (dry ? "5,10" : $"{SampleWeather.Days(1)[0].PrecipitationChance},80") + "]}}";

        _config = new RainNextHourConfig { Latitude = SamplePlace.Latitude, Longitude = SamplePlace.Longitude, LocationName = SamplePlace.LocationName };
        _pinnedNow = now;
        PublishPlace();
        var nowcast = new Fetched<RainNowcast>(FetchStatus.Ok, RainWire.Parse(json).Value, null, SampleDay.MinutesAgo(6));

        // [S-7042] stale: the answer on screen, and the last refresh failed: the header's problem glyph.
        _feed.Preview(stale ? nowcast.Stale(FetchStatus.Offline, null) : nowcast, FetchStatus.Ok);
    }
}
