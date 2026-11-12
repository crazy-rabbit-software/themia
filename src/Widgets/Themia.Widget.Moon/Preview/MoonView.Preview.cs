#if DEBUG
using Themia.App.Shell.Gallery;

namespace Themia.App.Widgets;

/// <summary>Debug renders only ([S-1917], [S-2050], [S-2051]): <c>sample</c> is the moon's sample (<see cref="ShowSampleAsync"/>).</summary>
internal sealed partial class MoonView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample"];

    public Task ShowPreviewStateAsync(string state)
        => state == "sample" ? ShowSampleAsync() : Task.CompletedTask;
}
#endif
