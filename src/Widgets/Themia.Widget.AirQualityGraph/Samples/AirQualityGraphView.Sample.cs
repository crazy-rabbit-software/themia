using Themia.App.Services.Web;
using Themia.App.Widgets;
using Themia.Core.Widgets;
using Themia.Sdk.Samples;

namespace Themia.Widget.AirQualityGraph;

/// <summary>
/// [S-11850] The add page's picture, in every build: the sample day's next four days of air (<see cref="SampleHours.Air"/>)
/// at the sample place and moment, held by this view alone. Nothing is fetched or saved.
/// </summary>
internal sealed partial class AirQualityGraphView : IWidgetSample
{
    public Task ShowSampleAsync()
    {
        Hold(SampleHours.Air(), FetchStatus.Ok);
        return Task.CompletedTask;
    }

    /// <summary>The sample place and moment, and <paramref name="hours"/> held as the answer (null: <paramref name="status"/> alone); <paramref name="stale"/>: kept through a failed refresh.</summary>
    internal void Hold(AirHours? hours, FetchStatus status, bool stale = false)
    {
        var copy = (AirQualityGraphConfig)_config.Clone();
        copy.Latitude = SamplePlace.Latitude;
        copy.Longitude = SamplePlace.Longitude;
        copy.LocationName = SamplePlace.LocationName;
        _config = copy;
        PinnedNow = SampleDay.Now;
        Fetched<AirHours>? held = hours is null ? null : new(FetchStatus.Ok, hours, null, SampleDay.MinutesAgo(12));
        _feed.Preview(stale && held is not null ? held.Stale(FetchStatus.Offline, null) : held, status);
    }
}
