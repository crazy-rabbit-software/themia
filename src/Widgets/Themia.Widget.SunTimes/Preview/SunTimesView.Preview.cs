#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.Sdk.Samples;

namespace Themia.Widget.SunTimes;

/// <summary>
/// Debug renders only ([S-1917]): <c>sample</c> is the sample day's place at its moment (Zürich, the sample
/// day at 10:42), the same the sun graphic's preview draws, so the two agree in a recipe.
/// </summary>
internal sealed partial class SunTimesView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (state == "sample")
        {
            var config = (SunTimesConfig)_config.Clone();
            config.Latitude = SamplePlace.Latitude;
            config.Longitude = SamplePlace.Longitude;
            config.Place = SamplePlace.Name;
            _now = SampleDay.Now;
            ApplyConfig(config);
        }

        return Task.CompletedTask;
    }
}
#endif
