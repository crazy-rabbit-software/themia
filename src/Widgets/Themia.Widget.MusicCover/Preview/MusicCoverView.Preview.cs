#if DEBUG
using Themia.App.Shell.Gallery;

namespace Themia.Widget.MusicCover;

/// <summary>Debug renders only: <c>sample</c> is the cover's sample (<see cref="ShowSampleAsync"/>).</summary>
internal sealed partial class MusicCoverView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample"];

    public Task ShowPreviewStateAsync(string state)
        => state == "sample" ? ShowSampleAsync() : Task.CompletedTask;
}
#endif
