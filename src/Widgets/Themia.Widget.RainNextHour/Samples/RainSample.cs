using Themia.App.Services.Web;
using Themia.App.Widgets;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Samples;

namespace Themia.Widget.RainNextHour;

/// <summary>
/// [S-11850] [S-2052] The rain family's sample: the sample day's shower from a quarter past eleven at the sample place, built
/// through the SDK's parser, so it is exactly what a reply would have become; the sentence, the graph and the total of a
/// group agree. Nothing is fetched or saved.
/// </summary>
internal static class RainSample
{
    /// <summary><paramref name="config"/> at the sample place.</summary>
    public static T Placed<T>(T config)
        where T : RainConfig
    {
        config.Latitude = SamplePlace.Latitude;
        config.Longitude = SamplePlace.Longitude;
        config.LocationName = SamplePlace.LocationName;
        return config;
    }

    /// <summary>
    /// The nowcast at the sample moment, dry or the sample's shower, kept through a failed refresh when
    /// <paramref name="stale"/> ([S-7042]: the answer on screen, the status's problem glyph).
    /// </summary>
    public static Fetched<RainNowcast> Answer(bool dry, bool stale)
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
        var nowcast = new Fetched<RainNowcast>(FetchStatus.Ok, RainWire.Parse(json).Value, null, SampleDay.MinutesAgo(6));
        return stale ? nowcast.Stale(FetchStatus.Offline, null) : nowcast;
    }
}

/// <summary>[S-11850] The rain's sentence on the sample day's shower.</summary>
internal sealed partial class RainOutlookView : IWidgetSample
{
    public Task ShowSampleAsync()
    {
        Hold(dry: false, stale: false);
        return Task.CompletedTask;
    }

    internal void Hold(bool dry, bool stale)
        => _feed.Preview(RainSample.Placed(new RainOutlookConfig()), SampleDay.Now, RainSample.Answer(dry, stale), FetchStatus.Ok);
}

/// <summary>[S-11850] The rain's total on the sample day's shower.</summary>
internal sealed partial class RainTotalView : IWidgetSample
{
    public Task ShowSampleAsync()
    {
        Hold(dry: false, stale: false);
        return Task.CompletedTask;
    }

    internal void Hold(bool dry, bool stale)
        => _feed.Preview(RainSample.Placed(new RainTotalConfig()), SampleDay.Now, RainSample.Answer(dry, stale), FetchStatus.Ok);
}
