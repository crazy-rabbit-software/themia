using Themia.App.Widgets;
using Themia.Sdk.Samples;

namespace Themia.Widget.MoonTimes;

/// <summary>[S-11850] A moon time's sample ([S-1917]): the sample day's place at its moment, as the moon reading's.</summary>
internal sealed partial class MoonMetricView : IWidgetSample
{
    public Task ShowSampleAsync()
    {
        var config = (MoonMetricConfig)_config.Clone();
        config.Latitude = SamplePlace.Latitude;
        config.Longitude = SamplePlace.Longitude;
        config.Place = SamplePlace.Name;
        _now = SampleDay.Now;
        _config = config;
        Render();
        return Task.CompletedTask;
    }
}
