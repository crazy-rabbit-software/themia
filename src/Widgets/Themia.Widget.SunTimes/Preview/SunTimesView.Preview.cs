#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.App.Widgets;

namespace Themia.Widget.SunTimes;

/// <summary>Debug renders only ([S-1917]): <c>sample</c> is the sample (<see cref="ShowSampleAsync"/>).</summary>
internal sealed partial class SunTimesView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample"];

    public Task ShowPreviewStateAsync(string state)
        => state == "sample" ? ShowSampleAsync() : Task.CompletedTask;
}
#endif
