using Microsoft.UI.Xaml;
using Themia.App.Widgets;
using Themia.Core.Links;
using Themia.Core.Office;
using Themia.Sdk.WinUI.Kit;

namespace Themia.Widget.UpNext;

/// <summary>
/// [S-11925] [S-11935] Up next's place on its view's links. A row is its meeting: the pointer on it publishes
/// the meeting (<see cref="LinkTopics.HoveredEvent"/>, <see cref="CalendarShare.Key"/>), and the row takes the
/// item layer's hover fill while that meeting is pointed at anywhere on the view (a calendar's chip, the next
/// meeting, a planner's row), or while a day it is on is (<see cref="LinkTopics.HoveredDate"/>: a calendar's
/// cell, a forecast's day, a task's due day, <see cref="CalendarShare.Day"/>). The rows are lit in place,
/// never rebuilt.
/// </summary>
internal sealed partial class UpNextView
{
    private readonly WidgetLinks _links = new();

    /// <summary>[S-11925] The day pointed at on the view.</summary>
    private DateLink? _pointedDay;

    /// <summary>[S-11935] The meeting another widget points at.</summary>
    private EventLink? _pointedEvent;

    /// <summary>[S-11935] The meeting this widget's pointer is on.</summary>
    private EventLink? _ownEvent;

    private void FollowLinks()
    {
        _links.Follow(LinkTopics.HoveredDate, value =>
        {
            _pointedDay = value;
            LightRows();
        });
        _links.Follow(LinkTopics.HoveredEvent, value =>
        {
            _pointedEvent = value;
            _ownEvent = null;
            LightRows();
        });
    }

    /// <summary>[S-11935] A new row: the pointer on it publishes its meeting, whichever it shows then.</summary>
    private void LinkRow(RowBlock row)
    {
        row.PointerEntered += (_, _) =>
        {
            if (row.Tag is CalendarEvent item)
            {
                _ownEvent = CalendarShare.Key(item);
                _links.Set(LinkTopics.HoveredEvent, _ownEvent);
                LightRows();
            }
        };

        row.PointerExited += (_, _) =>
        {
            if (_ownEvent is not null && row.Tag is CalendarEvent item && _ownEvent.Equals(CalendarShare.Key(item)))
            {
                _ownEvent = null;
                _links.Set(LinkTopics.HoveredEvent, null);
                LightRows();
            }
        };
    }

    /// <summary>
    /// [S-11900] [S-11901] Light every row shown against what the view points at: the meeting pointed at, or a
    /// meeting on the day pointed at, takes the hover fill.
    /// </summary>
    private void LightRows()
    {
        var zone = TimeZoneInfo.Local;
        var shown = _rows.Where(r => r.Visibility == Visibility.Visible && r.Tag is CalendarEvent).Select(r => (Row: r, Item: (CalendarEvent)r.Tag)).ToList();
        var day = LinkEmphasis.Shown(_pointedDay, shown.Select(s => CalendarShare.Day(s.Item, zone)));
        var meeting = LinkEmphasis.Shown(_ownEvent ?? _pointedEvent, shown.Select(s => CalendarShare.Key(s.Item)));

        foreach (var (row, item) in shown)
        {
            var light = LinkEmphasis.Light(CalendarShare.Key(item), meeting, null) != LinkLight.None
                ? LinkLight.Pointed
                : LinkEmphasis.Light(CalendarShare.Day(item, zone), day, null);
            LinkLook.Row(row, light);
        }
    }
}
