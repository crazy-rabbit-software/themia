using Themia.App.Shell.Gallery;

namespace Themia.Widget.MusicProgress;

/// <summary>
/// Debug renders only: <c>sample</c> is the progress bar's sample (<see cref="ShowSampleAsync"/>); <c>hovered</c> the
/// sample with the pointer over its seekable bar, a thin bar's thumb shown ([S-12816]: nothing else changes).
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
        }
    }
}
