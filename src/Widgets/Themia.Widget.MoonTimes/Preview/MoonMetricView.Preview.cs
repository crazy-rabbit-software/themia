
namespace Themia.Widget.MoonTimes;

/// <summary>Debug renders only ([S-1917]): <c>sample</c> is the sample (<see cref="ShowSampleAsync"/>).</summary>
internal sealed partial class MoonMetricView
{
    protected override IReadOnlyList<string> OwnPreviewStates { get; } = ["sample"];

    protected override Task ShowOwnPreviewStateAsync(string state)
        => state == "sample" ? ShowSampleAsync() : Task.CompletedTask;
}
