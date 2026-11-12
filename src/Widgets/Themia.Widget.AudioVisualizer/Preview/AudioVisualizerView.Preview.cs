#if DEBUG
using Themia.App.Shell.Gallery;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-3207]): "playing" is the sample's still spectrum (<see cref="ShowSampleAsync"/>);
/// "unavailable" what a device the capture cannot reach draws: the idle baseline, disabled ([S-1940]).
/// </summary>
internal sealed partial class AudioVisualizerView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["playing", "unavailable"];

    public Task ShowPreviewStateAsync(string state)
    {
        switch (state)
        {
            case "playing":
                return ShowSampleAsync();
            case "unavailable":
                ShowAvailability(available: false);
                break;
        }

        return Task.CompletedTask;
    }
}
#endif
