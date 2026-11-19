using Microsoft.UI.Xaml;
using Themia.App.Widgets;
using Themia.Core.Links;
using Themia.Core.Office;
using Themia.Sdk.WinUI.Kit;

namespace Themia.Widget.UpNext;

/// <summary>
/// [S-11935] [S-11965] Up next's place on its view's links. A row is its meeting, and only its meeting: the pointer on
/// it publishes the meeting (<see cref="LinkTopics.HoveredEvent"/>, <see cref="CalendarShare.Key"/>) and lights that row
/// alone; the row takes the item layer's hover fill while that same meeting is pointed at in another widget of the view
/// (a calendar's chip, the next meeting, a planner's row). Neither the day a meeting is on nor its organiser lights it:
/// several meetings share those. The rows are lit in place, never rebuilt.
/// </summary>
internal sealed partial class UpNextView
{
    private readonly WidgetLinks _links = new();

    /// <summary>[S-11935] The row this widget's pointer is on, and its meeting.</summary>
    private RowBlock? _ownRow;
    private EventLink? _ownEvent;

    private void FollowLinks()
        => _links.Follow(LinkTopics.HoveredEvent, _ =>
        {
            // Another widget points now: this widget's pointer is no longer what the view points at.
            _ownRow = null;
            _ownEvent = null;
            LightRows();
        });

    /// <summary>[S-11935] A new row: the pointer on it publishes its meeting, whichever it shows then.</summary>
    private void LinkRow(RowBlock row)
    {
        row.PointerEntered += (_, _) =>
        {
            if (row.Tag is CalendarEvent item)
            {
                _ownRow = row;
                _ownEvent = CalendarShare.Key(item);
                _links.Set(LinkTopics.HoveredEvent, _ownEvent);
                LightRows();
            }
        };

        row.PointerExited += (_, _) =>
        {
            if (ReferenceEquals(row, _ownRow))
            {
                _ownRow = null;
                _ownEvent = null;
                _links.Set(LinkTopics.HoveredEvent, null);
                LightRows();
            }
        };
    }

    /// <summary>
    /// [S-11900] [S-11901] [S-11965] Light every row shown: under this widget's pointer the row under it alone; else the
    /// row that is the meeting pointed at on the view. [S-11964] What the board holds is read now, never remembered.
    /// </summary>
    private void LightRows()
    {
        var shown = _rows.Where(r => r.Visibility == Visibility.Visible && r.Tag is CalendarEvent).Select(r => (Row: r, Item: (CalendarEvent)r.Tag)).ToList();
        var meeting = _ownEvent is null ? LinkEmphasis.Shown(_links.Get(LinkTopics.HoveredEvent), shown.Select(s => CalendarShare.Key(s.Item))) : null;

        foreach (var (row, item) in shown)
        {
            var light = _ownEvent is not null
                ? ReferenceEquals(row, _ownRow) ? LinkLight.Pointed : LinkLight.None
                : LinkEmphasis.Light(CalendarShare.Key(item), meeting, null);
            LinkLook.Row(row, light);
        }
    }
}
