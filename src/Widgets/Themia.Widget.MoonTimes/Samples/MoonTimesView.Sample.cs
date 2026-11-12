using Themia.App.Widgets;
using Themia.Sdk.Samples;

namespace Themia.Widget.MoonTimes;

/// <summary>[S-11850] The moon times' sample: the sample place at the sample moment, as the moon reading's.</summary>
internal sealed partial class MoonTimesView : IWidgetSample
{
    public Task ShowSampleAsync()
    {
        var config = (MoonTimesConfig)_config.Clone();
        config.Latitude = SamplePlace.Latitude;
        config.Longitude = SamplePlace.Longitude;
        config.Place = SamplePlace.Name;
        _now = SampleDay.Now;
        ApplyConfig(config);
        return Task.CompletedTask;
    }
}
