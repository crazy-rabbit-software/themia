using Microsoft.UI.Xaml;
using Themia.Core.Links;
using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-3620]..[S-3625] [S-11923] The calendar's place on its view's links ([S-3300]): the selected day it
/// shares with the other calendar widgets there, the range it was paged to, the event under the pointer
/// and the day under the pointer. Who follows what is Core (<see cref="CalendarShare"/>); this publishes,
/// listens and redraws. Without a board (a host with no view) the selection is the widget's own.
/// </summary>
internal sealed partial class CalendarView
{
    /// <summary>[S-3623] Every event element drawn now, so a hover from another widget dims without a redraw.</summary>
    private readonly List<(FrameworkElement Element, CalendarEvent Event)> _eventElements = [];

    private readonly WidgetLinks _links = new();

    /// <summary>[S-11923] The days drawn now (cells, heads, headings, agenda rows): the day pointed at anywhere on the view lights.</summary>
    private readonly LinkLights<DateLink> _days;

    /// <summary>[S-11923] The days lit on the agenda's rows and headings, which the kit's list draws again on its own.</summary>
    private readonly List<FrameworkElement> _rowDays = [];

    /// <summary>[S-3620] The selected day, this widget's or another's on the view; also the keys' cursor day ([S-3624]).</summary>
    private DateOnly? _selected;

    /// <summary>[S-3623] The event another widget's pointer is on.</summary>
    private EventLink? _linkedHover;

    /// <summary>[S-3622] A day the agenda turns its page to on its next draw, once it knows its pages.</summary>
    private DateOnly? _agendaJump;

    /// <summary>Whether the selected day is in the period on screen.</summary>
    private bool SelectedInView => _selected is { } day && InView(day);

    /// <summary>The topics followed, once; <see cref="WidgetLinks"/> watches them again on every join.</summary>
    private LinkLights<DateLink> FollowLinks()
    {
        // [S-3305] A view shown again: what was selected there is followed at once.
        _links.Follow(LinkTopics.SelectedDate, OnSelectedElsewhere);
        _links.Follow(LinkTopics.HoveredEvent, OnHoveredElsewhere);
        return new LinkLights<DateLink>(_links, LinkTopics.HoveredDate);
    }

    private void JoinLinks(LinkBoard? board) => _links.Join(board);

    /// <summary>Leave the board (Detach): this widget's hovers go with it, the selection stays ([S-3304]).</summary>
    private void LeaveLinks()
    {
        _links.Leave();
        _linkedHover = null;
    }

    /// <summary>
    /// [S-3620] Select a day (null: let the selection go), here and on the view: every calendar there
    /// follows it ([S-3621]).
    /// </summary>
    private void Select(DateOnly? day)
    {
        _selected = day;
        _links.Set(LinkTopics.SelectedDate, day is { } d ? new DateLink(d) : null);

        if (day is { } shown)
        {
            Follow(shown);
        }
        else
        {
            Render();
        }
    }

    /// <summary>[S-3620] A click on a day cell: select it, or let it go when it is the selection.</summary>
    private void ToggleDay(DateOnly day) => Select(CalendarShare.Toggle(_selected, day));

    private void OnSelectedElsewhere(DateLink? value)
    {
        // Joining tells the value held there: the same day as this widget's changes nothing.
        if (_detached || value?.Date == _selected)
        {
            return;
        }

        _selected = value?.Date;

        if (value is { } link)
        {
            Follow(link.Date);
        }
        else
        {
            Render();
        }
    }

    /// <summary>
    /// [S-3621] Bring the selected day into view: the month, week, day and year anchor on it (paging
    /// only when it is in another period), the agenda turns to its page ([S-3622]).
    /// </summary>
    private void Follow(DateOnly day)
    {
        if (Layout == CalendarStyle.Agenda)
        {
            _agendaJump = day;
            Render();
            return;
        }

        var before = Range();
        _anchor = CalendarShare.AnchorFor(Layout, _anchor, day);

        // A selection in the period holding today keeps following today across midnight.
        _followToday = InView(Today);
        Render();

        if (Range() != before)
        {
            _ = RefreshAsync(force: false);
        }
    }

    /// <summary>[S-3625] Paged: say which days are on screen now. No other calendar moves for it.</summary>
    private void PublishRange()
    {
        if (!_links.Joined || Layout == CalendarStyle.Agenda)
        {
            return;
        }

        var (first, last) = Range();
        _links.Set(LinkTopics.VisibleRange, new RangeLink(first, last));
    }

    /// <summary>
    /// [S-11923] A day's cell, head, heading or row: it publishes its day while the pointer is on it and takes
    /// the item layer's hover fill while that day is pointed at anywhere on the view. A hover never moves
    /// or pages the calendar. <paramref name="rest"/> puts back a face of its own (a selected day's fill).
    /// </summary>
    private void LinkDay(FrameworkElement element, DateOnly day, ItemRole role = ItemRole.Row, Action? rest = null)
        => _days.Row(element, new DateLink(day), role: role, rest: rest);

    /// <summary>
    /// [S-11923] A day whose face is its own (today's disc, a square in its first event's colour): it publishes
    /// its day under the pointer and is never repainted for one.
    /// </summary>
    private void PublishDay(FrameworkElement element, DateOnly day)
    {
        var link = new DateLink(day);
        element.PointerEntered += (_, _) => _days.Point(link);
        element.PointerExited += (_, _) =>
        {
            if (link.Equals(_days.Pointed))
            {
                _days.Point(null);
            }
        };
    }

    /// <summary>[S-11923] An agenda row or heading for its day; forgotten when the kit's list draws its rows again.</summary>
    private void LinkRowDay(FrameworkElement element, DateOnly day)
    {
        _rowDays.Add(element);
        LinkDay(element, day);
    }

    /// <summary>The kit's list draws its rows again: the old rows' days are forgotten.</summary>
    private void ForgetRowDays()
    {
        foreach (var element in _rowDays)
        {
            _days.Remove(element);
        }

        _rowDays.Clear();
    }

    /// <summary>[S-3623] An event drawn: it lights the same event in the other calendars while pointed at, and dims when another is.</summary>
    private void LinkEvent(FrameworkElement element, CalendarEvent item)
    {
        _eventElements.Add((element, item));
        element.Opacity = EventStrength(_linkedHover, item);

        var key = CalendarShare.Key(item);
        element.PointerEntered += (_, _) =>
        {
            // The pointer here replaces whatever another widget lit: nothing here stays dimmed by it.
            _links.Set(LinkTopics.HoveredEvent, key);
            OnHoveredElsewhere(null);
        };

        // [S-3304] Only this widget's own hover is taken away by its pointer leaving.
        element.PointerExited += (_, _) => _links.Set(LinkTopics.HoveredEvent, null);
    }

    private void OnHoveredElsewhere(EventLink? value)
    {
        _linkedHover = value;

        foreach (var (element, item) in _eventElements)
        {
            element.Opacity = EventStrength(value, item);
        }
    }

    /// <summary>
    /// [S-3623] [S-11902] An event's chip against the event pointed at elsewhere: whole, or the links' "less"
    /// (0.6) while another event this calendar draws is the one; an event it does not draw dims nothing.
    /// </summary>
    private double EventStrength(EventLink? hovered, CalendarEvent item)
        => CalendarShare.Dimmed(hovered, item) && _eventElements.Any(drawn => !CalendarShare.Dimmed(hovered, drawn.Event))
            ? LinkEmphasis.Others(pointed: true, picked: false)
            : 1;
}
