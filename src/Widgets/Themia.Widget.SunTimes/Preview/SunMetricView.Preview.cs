#if DEBUG

namespace Themia.Widget.SunTimes;

/// <summary>Debug renders only ([S-1917]): <c>sample</c> is the sample (<see cref="ShowSampleAsync"/>).</summary>
internal sealed partial class SunMetricView
{
    protected override IReadOnlyList<string> OwnPreviewStates { get; } = ["sample"];

    protected override Task ShowOwnPreviewStateAsync(string state)
        => state == "sample" ? ShowSampleAsync() : Task.CompletedTask;
}
#endif
