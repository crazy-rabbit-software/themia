using Themia.App.Services.Web;
using Themia.App.Widgets;
using Themia.Core.Widgets;
using Themia.Sdk.Samples;

namespace Themia.Widget.HumidityGraph;

/// <summary>
/// [S-11850] The add page's picture, in every build: the sample day hour by hour (<see cref="SampleHours"/>) at the
/// sample place and moment, held by this view alone. Nothing is fetched or saved.
/// </summary>
internal sealed partial class HumidityGraphView : IWidgetSample
{
    public Task ShowSampleAsync()
    {
        Hold(SampleHours.Weather(), FetchStatus.Ok);
        return Task.CompletedTask;
    }

    /// <summary>The sample place and moment, and <paramref name="hours"/> held as the answer (null: <paramref name="status"/> alone); <paramref name="stale"/>: kept through a failed refresh.</summary>
    internal void Hold(WeatherHours? hours, FetchStatus status, bool stale = false)
    {
        var copy = (HumidityGraphConfig)_config.Clone();
        copy.Latitude = SamplePlace.Latitude;
        copy.Longitude = SamplePlace.Longitude;
        copy.LocationName = SamplePlace.LocationName;
        _config = copy;
        PinnedNow = SampleDay.Now;
        Fetched<WeatherHours>? held = hours is null ? null : new(FetchStatus.Ok, hours, null, SampleDay.MinutesAgo(8));
        _feed.Preview(stale && held is not null ? held.Stale(FetchStatus.Offline, null) : held, status);
    }
}
