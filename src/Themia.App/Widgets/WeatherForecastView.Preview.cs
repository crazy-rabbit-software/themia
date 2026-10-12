#if DEBUG
using Themia.App.Services.Web;
using Themia.App.Shell.Gallery;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917]): a fixture week for Zürich, so each layout's ladder can be looked
/// at without a place, the network or the sync service. Nothing is fetched or saved.
/// <c>sample</c> lets the tile pick its layout; <c>rows</c>, <c>columns</c> and <c>grid</c> force one.
/// </summary>
internal sealed partial class WeatherForecastView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "rows", "columns", "grid"];

    public Task ShowPreviewStateAsync(string state)
    {
        var layout = state switch
        {
            "sample" => ForecastLayout.Auto,
            "rows" => ForecastLayout.Rows,
            "columns" => ForecastLayout.Columns,
            "grid" => ForecastLayout.Grid,
            _ => (ForecastLayout?)null,
        };

        if (layout is not { } chosen)
        {
            return Task.CompletedTask;
        }

        _config = new WeatherForecastConfig
        {
            Latitude = 47.37,
            Longitude = 8.54,
            LocationName = "Zürich, Zurich, Switzerland",
            Layout = chosen,
        };

        _applied = (WeatherForecastConfig)_config.Clone();
        _want = null;
        _forecast = new Fetched<Forecast>(FetchStatus.Ok, new Forecast(WeatherNowView.SampleDays(_config.EffectiveDays)), null, DateTimeOffset.Now.AddMinutes(-4));
        PublishTitle();
        Render();
        return Task.CompletedTask;
    }
}
#endif
