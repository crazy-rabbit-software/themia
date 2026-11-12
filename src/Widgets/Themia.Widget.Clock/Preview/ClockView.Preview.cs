#if DEBUG
using Themia.App.Shell.Gallery;

namespace Themia.App.Widgets;

/// <summary>Debug renders only ([S-2050]): <c>sample</c> is the clock's sample (<see cref="ShowSampleAsync"/>).</summary>
public sealed partial class ClockView : IWidgetPreviewState
{
    IReadOnlyList<string> IWidgetPreviewState.PreviewStates { get; } = ["sample"];

    Task IWidgetPreviewState.ShowPreviewStateAsync(string state)
        => state == "sample" ? ShowSampleAsync() : Task.CompletedTask;
}
#endif
