#if DEBUG
using Themia.App.Shell.Gallery;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only: <c>sample</c> stops the clock at 10:42:18 in its own zone, so every style
/// and every language is drawn at the same moment (the word style says "twenty to eleven").
/// </summary>
public sealed partial class ClockView : IWidgetPreviewState
{
    IReadOnlyList<string> IWidgetPreviewState.PreviewStates { get; } = ["sample"];

    Task IWidgetPreviewState.ShowPreviewStateAsync(string state)
    {
        if (state == "sample")
        {
            var zone = _config.ResolveTimeZone();
            var today = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).Date;
            var local = today.AddHours(10).AddMinutes(42).AddSeconds(18);
            _frozen = new DateTimeOffset(local, zone.GetUtcOffset(local));
            ApplyConfig(_config);
        }

        return Task.CompletedTask;
    }
}
#endif
