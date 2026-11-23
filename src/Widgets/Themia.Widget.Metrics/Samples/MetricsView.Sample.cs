using Themia.App.Services.Web;
using Themia.App.Widgets;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk.Samples;

namespace Themia.Widget.Metrics;

/// <summary>
/// [S-11850] [S-16309] The add page's picture of the Metric widget, in every build: the sample day's data in place of the
/// metric's live source (the place's hours and days, its air, MSFT's session, the ECB's table), held by this view alone,
/// never written into a store. A system metric has none of its own: its host hands it the sample PC already.
/// </summary>
internal sealed partial class MetricsView : IWidgetSample
{
    public Task ShowSampleAsync()
    {
        ShowSample(stale: false, empty: false);
        return Task.CompletedTask;
    }

    /// <summary>The sample at the sample moment: kept through a failed refresh where <paramref name="stale"/>, none at all where <paramref name="empty"/>.</summary>
    internal void ShowSample(bool stale, bool empty)
    {
        var config = (MetricsConfig)_config.Clone();
        if (!config.HasLocation)
        {
            (config.Latitude, config.Longitude, config.LocationName) = (SamplePlace.Latitude, SamplePlace.Longitude, SamplePlace.LocationName);
        }

        if (config.Symbol.Length == 0)
        {
            (config.Symbol, config.DisplayName) = ("MSFT", "Microsoft Corporation");
        }

        _config = config;
        _pinnedNow = SampleDay.Now;
        Fetched<T>? Held<T>(T value)
            where T : class
        {
            if (empty)
            {
                return null;
            }

            var held = new Fetched<T>(FetchStatus.Ok, value, null, SampleDay.MinutesAgo(6));
            return stale ? held.Stale(FetchStatus.Offline, null) : held;
        }

        var status = empty ? FetchStatus.Loading : FetchStatus.Ok;
        switch (config.Feed.Source)
        {
            case MetricSource.Hours:
                _hours.Preview(Held(SampleHours.Weather()), status);
                break;
            case MetricSource.Days:
                _days.Preview(Held(new Forecast(SampleWeather.Days(WeatherReport.ForecastDays))), status);
                break;
            case MetricSource.AirHours:
                _air.Preview(Held(SampleHours.Air()), status);
                break;
            case MetricSource.Quote:
                _quote.Preview(empty ? null : SampleQuote.Quote(rising: true) is { Value: { } quote } ? Held(quote) : null, status);
                break;
            case MetricSource.Rates:
                _rates.Preview(empty ? null : SampleRates.Table() is { Value: { } table } ? Held(table) : null, status);
                break;
            default:
                Render();
                break;
        }
    }
}
