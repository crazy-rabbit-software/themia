#if DEBUG
using Themia.App.Shell.Gallery;

namespace Themia.App.Widgets;

/// <summary>Debug renders only ([S-1917]): <c>sample</c> is the countdown's sample (<see cref="ShowSampleAsync"/>).</summary>
internal sealed partial class CountdownView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample"];

    public Task ShowPreviewStateAsync(string state)
        => state == "sample" ? ShowSampleAsync() : Task.CompletedTask;
}
#endif
