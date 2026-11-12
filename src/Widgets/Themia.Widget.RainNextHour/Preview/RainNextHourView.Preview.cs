#if DEBUG
using Themia.App.Shell.Gallery;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917], [S-2052]): <c>sample</c> is the nowcast's sample (<see cref="ShowSampleAsync"/>),
/// <c>dry</c> a dry hour, and <c>stale</c> the sample kept through a failed refresh, so the atom can be
/// looked at without a place, the network or the sync.
/// </summary>
internal sealed partial class RainNextHourView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "dry", "stale"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (state is "sample" or "dry" or "stale")
        {
            ShowNowcast(dry: state == "dry", stale: state == "stale");
        }

        return Task.CompletedTask;
    }
}
#endif
