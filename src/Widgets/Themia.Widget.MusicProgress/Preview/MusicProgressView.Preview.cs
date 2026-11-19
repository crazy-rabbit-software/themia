#if DEBUG
using Themia.App.Shell.Gallery;

namespace Themia.Widget.MusicProgress;

/// <summary>
/// Debug renders only: <c>sample</c> is the progress bar's sample (<see cref="ShowSampleAsync"/>); <c>hovered</c> the
/// sample with the pointer over its seekable bar, its thumb shown ([S-12816]: the block keeps its scale).
/// </summary>
internal sealed partial class MusicProgressView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "hovered"];

    public async Task ShowPreviewStateAsync(string state)
    {
        if (state is not ("sample" or "hovered"))
        {
            return;
        }

        await ShowSampleAsync().ConfigureAwait(true);
        if (state == "hovered")
        {
            _seek.CanSeek = true;
            _seek.Point(true);

            // Any layout pass while the pointer stays (a tick's text, a resize, the card's own) measures the block again.
            _fit.InvalidateMeasure();
        }
    }
}
#endif
