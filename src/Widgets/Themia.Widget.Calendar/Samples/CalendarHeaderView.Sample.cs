using Themia.App.Widgets;
using Themia.Sdk.Samples;

namespace Themia.Widget.Calendar;

/// <summary>
/// [S-11850] [S-2054] The calendar header's sample: the sample day's month, as the calendars over the sample schedule show
/// it ([S-2050]). Held by this view alone: a sample says no period on the view's links.
/// </summary>
internal sealed partial class CalendarHeaderView : IWidgetSample
{
    public Task ShowSampleAsync()
    {
        var now = SampleDay.Now;
        _clock = () => now;
        _anchor = Today;
        _followToday = true;
        Render();
        return Task.CompletedTask;
    }
}
