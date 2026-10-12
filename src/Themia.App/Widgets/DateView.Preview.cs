#if DEBUG
using Themia.App.Shell.Gallery;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917]): <c>sample</c> pins Wednesday 30 September 2026 (the longest English
/// weekday and month) so renders compare; <c>year</c> is the same day with the year shown.
/// </summary>
internal sealed partial class DateView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "year"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (!PreviewStates.Contains(state, StringComparer.Ordinal))
        {
            return Task.CompletedTask;
        }

        var clock = new DateTime(2026, 9, 30, 9, 0, 0, DateTimeKind.Unspecified);
        var pinned = new DateTimeOffset(clock, TimeZoneInfo.Local.GetUtcOffset(clock));
        _clock = () => pinned;
        _config = new Themia.Core.Model.DateConfig { ShowYear = state == "year" };
        Lay();
        return Task.CompletedTask;
    }
}
#endif
