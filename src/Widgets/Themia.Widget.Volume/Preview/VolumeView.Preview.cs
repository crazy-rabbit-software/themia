#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.App.Services;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917]): made-up snapshots, never this PC's. <c>level</c> is the sample
/// (<see cref="ShowSampleAsync"/>); <c>unavailable</c> a PC with no output device ([S-1940]).
/// </summary>
internal sealed partial class VolumeView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["level", "unavailable"];

    public Task ShowPreviewStateAsync(string state)
    {
        switch (state)
        {
            case "level":
                return ShowSampleAsync();

            case "unavailable":
                HoldSnapshot(new AudioSnapshot(AudioStatus.NoDevice, null, [], [], null, AudioTarget.Default));
                break;
        }

        return Task.CompletedTask;
    }
}
#endif
