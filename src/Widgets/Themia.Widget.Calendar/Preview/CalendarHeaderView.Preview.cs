using Themia.App.Shell.Gallery;

namespace Themia.Widget.Calendar;

/// <summary>
/// Debug renders only ([S-1917]): <c>sample</c> the sample day's period (<see cref="ShowSampleAsync"/>); <c>next</c> the
/// sample, then forward one period by the header's own arrow ([S-3626]): in a picture with calendars (a ready-made group,
/// <c>--render recipe calendar --states calendarHeader=next</c>) every calendar of it shows the period after the sample
/// day's.
/// </summary>
internal sealed partial class CalendarHeaderView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "next"];

    /// <inheritdoc />
    public Themia.Core.Model.WidgetConfig? DrawnConfig => null;

    public async Task ShowPreviewStateAsync(string state)
    {
        await ShowSampleAsync().ConfigureAwait(true);
        if (string.Equals(state, "next", StringComparison.OrdinalIgnoreCase))
        {
            Navigate(1);
        }
    }
}
