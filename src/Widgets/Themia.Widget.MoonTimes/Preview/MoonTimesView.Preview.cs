#if DEBUG
using Themia.App.Shell.Gallery;

namespace Themia.Widget.MoonTimes;

/// <summary>Debug renders only ([S-1917]): <c>sample</c> is the moon times' sample (<see cref="ShowSampleAsync"/>).</summary>
internal sealed partial class MoonTimesView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample"];

    public Task ShowPreviewStateAsync(string state)
        => state == "sample" ? ShowSampleAsync() : Task.CompletedTask;
}
#endif
