using System.Globalization;
using Themia.App.Services.Web;
using Themia.App.Widgets;
using Themia.Core.Widgets;
using Themia.Sdk.Samples;

namespace Themia.Widget.RainGraph;

/// <summary>
/// [S-11850] The add page's picture, in every build: the sample day hour by hour (<see cref="SampleHours"/>), or in
/// the nowcast look the sample day's shower by the quarter (the rain widget's, through the SDK's parser), at the
/// sample place and moment, held by this view alone. Nothing is fetched or saved.
/// </summary>
internal sealed partial class RainGraphView : IWidgetSample
{
    public Task ShowSampleAsync()
    {
        Hold(SampleHours.Weather(), FetchStatus.Ok);
        return Task.CompletedTask;
    }

    /// <summary>The sample place and moment, and the sample held as the look's answer (none: <paramref name="status"/> alone).</summary>
    internal void Hold(WeatherHours? hours, FetchStatus status, bool stale = false)
    {
        var copy = (RainGraphConfig)_config.Clone();
        copy.Latitude = SamplePlace.Latitude;
        copy.Longitude = SamplePlace.Longitude;
        copy.LocationName = SamplePlace.LocationName;
        _config = copy;
        PinnedNow = SampleDay.Now;
        var at = SampleDay.MinutesAgo(8);
        if (_config.Style == RainGraphStyle.Nowcast)
        {
            Fetched<RainNowcast>? cast = hours is null ? null : new(FetchStatus.Ok, SampleNowcast(), null, at);
            _nowcast.Preview(stale && cast is not null ? cast.Stale(FetchStatus.Offline, null) : cast, status);
            return;
        }

        Fetched<WeatherHours>? held = hours is null ? null : new(FetchStatus.Ok, hours, null, at);
        _hours.Preview(stale && held is not null ? held.Stale(FetchStatus.Offline, null) : held, status);
    }

    /// <summary>The sample day's quarters from the one under way (<see cref="SampleWeather.NextQuarters"/>), as a reply would have become.</summary>
    private static RainNowcast SampleNowcast()
    {
        var quarter = SampleDay.Now.ToUnixTimeSeconds() / 900 * 900;
        var amounts = SampleWeather.NextQuarters;

        // A step is stamped at its end ([S-5122]): the quarter under way ends a quarter on.
        var times = string.Join(',', amounts.Select((_, i) => quarter + ((i + 1) * 900)));
        var values = string.Join(',', amounts.Select(a => a.ToString(CultureInfo.InvariantCulture)));
        return RainWire.Parse("{\"minutely_15\":{\"time\":[" + times + "],\"precipitation\":[" + values + "]}}").Value!;
    }
}
