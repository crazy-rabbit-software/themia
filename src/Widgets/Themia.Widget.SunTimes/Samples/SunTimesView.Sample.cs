using Themia.App.Widgets;
using Themia.Sdk.Samples;

namespace Themia.Widget.SunTimes;

/// <summary>
/// [S-11850] The sun times' sample ([S-1917]): the sample day's place at its moment (Zürich, the sample day at 10:42),
/// the same the sun graphic's sample draws, so the two agree in a recipe.
/// </summary>
internal sealed partial class SunTimesView : IWidgetSample
{
    public Task ShowSampleAsync()
    {
        var config = (SunTimesConfig)_config.Clone();
        config.Latitude = SamplePlace.Latitude;
        config.Longitude = SamplePlace.Longitude;
        config.Place = SamplePlace.Name;
        _now = SampleDay.Now;
        ApplyConfig(config);
        return Task.CompletedTask;
    }
}
