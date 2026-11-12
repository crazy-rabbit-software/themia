#if DEBUG
using Themia.App.Services;
using Themia.App.Shell.Gallery;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917]): <c>sample</c> is the mixer's sample (<see cref="ShowSampleAsync"/>),
/// <c>unavailable</c> a PC with no output device ([S-1940]); never this PC's.
/// </summary>
internal sealed partial class MixerView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "unavailable"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (state == "sample")
        {
            return ShowSampleAsync();
        }

        if (state == "unavailable")
        {
            Hold(new AudioSnapshot(AudioStatus.NoDevice, null, [], [], null, AudioTarget.Default));
        }

        return Task.CompletedTask;
    }
}
#endif
