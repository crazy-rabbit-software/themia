#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.Sdk.Samples;

namespace Themia.Widget.MoonTimes;

/// <summary>Debug renders only ([S-1917]): <c>sample</c> is the sample place at the sample moment, as the moon reading's preview.</summary>
internal sealed partial class MoonTimesView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (state == "sample")
        {
            var config = (MoonTimesConfig)_config.Clone();
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
