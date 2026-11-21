using Themia.App.Services;
using Themia.Core.Widgets;
using Themia.App.Shell.Gallery;

namespace Themia.Widget.MusicCover;

/// <summary>
/// Debug renders only: <c>sample</c> is the cover's sample (<see cref="ShowSampleAsync"/>); <c>idle</c> a session with no
/// track, the placeholder in the picture's box ([S-11320]).
/// </summary>
internal sealed partial class MusicCoverView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "idle"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (state == "idle")
        {
            _feed.Show(NowPlaying.Of(MusicState.Idle));
            return Task.CompletedTask;
        }

        return state == "sample" ? ShowSampleAsync() : Task.CompletedTask;
    }
}
