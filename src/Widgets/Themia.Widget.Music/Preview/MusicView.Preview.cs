#if DEBUG
using Themia.App.Services;
using Themia.App.Shell.Gallery;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only: <c>sample</c> is the music widget's sample (<see cref="ShowSampleAsync"/>); <c>idle</c> a
/// session with no track in it, the face kept ([S-12813]).
/// </summary>
internal sealed partial class MusicView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "idle"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (state == "sample")
        {
            return ShowSampleAsync();
        }

        if (state == "idle")
        {
            _feed.Show(NowPlaying.Of(MusicState.Idle));
        }

        return Task.CompletedTask;
    }
}
#endif
