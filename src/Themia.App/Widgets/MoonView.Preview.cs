#if DEBUG
using Themia.App.Shell.Gallery;

namespace Themia.App.Widgets;

/// <summary>Debug renders only ([S-1917]): <c>sample</c> pins Monday 12 October 2026, 21:00, so the disc and the phase month ([S-6050]) compare.</summary>
internal sealed partial class MoonView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (state == "sample")
        {
            var clock = new DateTime(2026, 10, 12, 21, 0, 0, DateTimeKind.Unspecified);
            _now = new DateTimeOffset(clock, TimeZoneInfo.Local.GetUtcOffset(clock));
            Lay();
        }

        return Task.CompletedTask;
    }
}
#endif
