#if DEBUG
using Themia.App.Shell.Gallery;

namespace Themia.App.Widgets;

/// <summary>Debug renders only ([S-1917]): a fixed afternoon (Monday 12 October 2026, 14:32) so renders compare.</summary>
internal sealed partial class YearProgressView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (state == "sample")
        {
            var clock = new DateTime(2026, 10, 12, 14, 32, 0, DateTimeKind.Unspecified);
            var pinned = new DateTimeOffset(clock, TimeZoneInfo.Local.GetUtcOffset(clock));
            _clock = () => pinned;
            Render();
        }

        return Task.CompletedTask;
    }
}
#endif
