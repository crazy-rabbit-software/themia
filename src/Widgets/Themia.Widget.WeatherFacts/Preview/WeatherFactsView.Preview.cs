#if DEBUG
using Themia.App.Services.Web;
using Themia.App.Shell.Gallery;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Samples;

namespace Themia.Widget.WeatherFacts;

/// <summary>
/// Debug renders only ([S-1917], [S-2052]): the sample day's reading at the sample place (the same the
/// reading card's previews draw, so a recipe's atoms agree), nothing fetched. <c>stale</c> is that
/// reading through a failed refresh; <c>failed</c> no reading and the service out of reach ([S-6734]).
/// </summary>
internal sealed partial class WeatherFactsView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "stale", "failed"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (state is not ("sample" or "stale" or "failed"))
        {
            return Task.CompletedTask;
        }

        _config = new WeatherFactsConfig { Latitude = SamplePlace.Latitude, Longitude = SamplePlace.Longitude, LocationName = SamplePlace.LocationName, Facts = [.. _config.Facts] };
        _header.SetAbout(_config.PlaceLabel);
        var reading = new Fetched<CurrentWeather>(FetchStatus.Ok, SampleWeather.Reading(), null, SampleDay.MinutesAgo(4));
        _feed.Preview(
            state switch
            {
                "stale" => reading.Stale(FetchStatus.Offline, null),
                "failed" => null,
                _ => reading,
            },
            state == "failed" ? FetchStatus.Unreachable : FetchStatus.Ok);
        return Task.CompletedTask;
    }
}
#endif
