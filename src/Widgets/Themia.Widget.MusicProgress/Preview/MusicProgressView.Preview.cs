#if DEBUG
using Themia.App.Shell.Gallery;

namespace Themia.Widget.MusicProgress;

/// <summary>Debug renders only: <c>sample</c> is the progress bar's sample (<see cref="ShowSampleAsync"/>).</summary>
internal sealed partial class MusicProgressView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample"];

    public Task ShowPreviewStateAsync(string state)
        => state == "sample" ? ShowSampleAsync() : Task.CompletedTask;
}
#endif
