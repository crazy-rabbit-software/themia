using Themia.Core.Model;
using Themia.Sdk.Samples;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11850] [S-2050] [S-2051] The moon's sample: the sample day's place at its moment (Zürich, today at
/// 10:42), so the disc and the phase month ([S-6050]) compare between pictures and agree with the
/// weather widgets' sunrise and sunset.
/// </summary>
internal sealed partial class MoonView : IWidgetSample
{
    public Task ShowSampleAsync()
    {
        var config = (MoonConfig)_config.Clone();
        config.Latitude = SamplePlace.Latitude;
        config.Longitude = SamplePlace.Longitude;
        config.Place = SamplePlace.Name;
        _now = SampleDay.Now;
        ApplyConfig(config);
        return Task.CompletedTask;
    }
}
