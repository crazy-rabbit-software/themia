#if DEBUG
using Themia.App.Shell.Gallery;

namespace Themia.App.Widgets;

/// <summary>Debug renders only ([S-1917]): <c>sample</c> pins Monday 12 October 2026, 14:32, so the arc and the day bar ([S-6045]) compare.</summary>
internal sealed partial class SunView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (state == "sample")
        {
            var clock = new DateTime(2026, 10, 12, 14, 32, 0, DateTimeKind.Unspecified);
            _now = new DateTimeOffset(clock, TimeZoneInfo.Local.GetUtcOffset(clock));
            Lay();
        }

        return Task.CompletedTask;
    }
}
#endif
