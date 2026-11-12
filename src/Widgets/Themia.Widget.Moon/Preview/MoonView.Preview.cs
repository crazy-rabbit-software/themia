#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.Core.Model;
using Themia.Sdk.Samples;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917], [S-2050], [S-2051]): <c>sample</c> is the sample day's place at its
/// moment (Zürich, today at 10:42), so the disc and the phase month ([S-6050]) compare between renders and agree with the
/// weather widgets' sunrise and sunset.
/// </summary>
internal sealed partial class MoonView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (state == "sample")
        {
            var config = (MoonConfig)_config.Clone();
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
