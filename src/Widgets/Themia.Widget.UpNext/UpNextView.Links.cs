using Microsoft.UI.Xaml;
using Themia.App.Widgets;
using Themia.Core.Links;
using Themia.Core.Office;
using Themia.Sdk.WinUI.Kit;

namespace Themia.Widget.UpNext;

/// <summary>
/// [S-11925] Up next's place on its view's links: while a day is pointed at anywhere on the view
/// (<see cref="LinkTopics.HoveredDate"/>: a calendar's cell, a forecast's day, a task's due day), the
/// meetings on that day (<see cref="CalendarShare.Day"/>) take the item layer's hover fill. The rows are
/// lit in place, never rebuilt.
/// </summary>
internal sealed partial class UpNextView
{
    private readonly WidgetLinks _links = new();

    /// <summary>[S-11925] The day pointed at on the view.</summary>
    private DateLink? _pointedDay;

    private void FollowLinks() => _links.Follow(LinkTopics.HoveredDate, value =>
    {
        _pointedDay = value;
        LightRows();
    });

    /// <summary>[S-11900] [S-11901] Light every row shown against what the view points at: a row on that day takes the hover fill.</summary>
    private void LightRows()
    {
        var zone = TimeZoneInfo.Local;
        var shown = _rows.Where(r => r.Visibility == Visibility.Visible && r.Tag is CalendarEvent).Select(r => (Row: r, Item: (CalendarEvent)r.Tag)).ToList();
        var day = LinkEmphasis.Shown(_pointedDay, shown.Select(s => CalendarShare.Day(s.Item, zone)));

        foreach (var (row, item) in shown)
        {
            LinkLook.Row(row, LinkEmphasis.Light(CalendarShare.Day(item, zone), day, null));
        }
    }
}
