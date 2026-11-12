#if DEBUG
using Themia.App.Services.Web;
using Themia.App.Shell.Gallery;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1314]): a render never opens a camera, so every state is a still. "live" is the
/// sample (<see cref="ShowSampleAsync"/>); "paused", "connecting", "dropped" and "refused" are the Status
/// row's states. The address is a placeholder no render connects to.
/// </summary>
internal sealed partial class VideoStreamView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["live", "paused", "connecting", "dropped", "refused"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (state == "live")
        {
            return ShowSampleAsync();
        }

        ShowStill(
            state == "refused" ? "ftp://camera.local/video" : PreviewAddress,
            paused: state == "paused",
            state == "dropped" ? StreamPhase.Retrying : StreamPhase.Connecting,
            reconnects: state == "dropped" ? 3 : 0);
        Render();
        return Task.CompletedTask;
    }
}
#endif
