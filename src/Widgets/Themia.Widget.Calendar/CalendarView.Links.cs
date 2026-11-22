using Microsoft.UI.Xaml;
using Themia.Core.Links;
using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-3620]..[S-3629] [S-11923] The calendar's place on its view's links ([S-3300]): the selected day it
/// shares with the other calendar widgets there, the period the view's calendars show ([S-3626]: a calendar header or a
/// calendar paged there), the event under the pointer and the day under the pointer. Who follows what is Core (<see cref="CalendarShare"/>); this publishes,
/// listens and redraws. Without a board (a host with no view) the selection is the widget's own.
/// </summary>
internal sealed partial class CalendarView
{
    /// <summary>[S-3623] Every event element drawn now, so a hover from another widget dims without a redraw.</summary>
    private readonly List<(FrameworkElement Element, CalendarEvent Event)> _eventElements = [];

    private readonly WidgetLinks _links = new();

    /// <summary>
    /// [S-11923] The days drawn now (cells, heads, the agenda's headings): the day pointed at anywhere on the view lights.
    /// [S-11965] An event is never its day: its chip or row lights for the event alone.
    /// </summary>
    private readonly LinkLights<DateLink> _days;

    /// <summary>[S-11923] The days lit on the agenda's headings, which the kit's list draws again on its own.</summary>
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
        _links.Follow(LinkTopics.VisibleRange, OnPeriodElsewhere);
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
    /// follows it ([S-3621]). [S-3627] A day in another period pages there, and that period is the view's too.
    /// </summary>
    private void Select(DateOnly? day)
    {
        _selected = day;
        _links.Set(LinkTopics.SelectedDate, day is { } d ? new DateLink(d) : null);

        if (day is { } shown)
        {
            Follow(shown);
            if (Layout != CalendarStyle.Agenda)
            {
                PublishPeriod();
            }
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

    /// <summary>
    /// [S-3626] [S-3627] Paged: say the period on screen now (a month its own days, the agenda its page's), so every
    /// calendar header and calendar of the view follows it. Only the widget that paged says it: a follower never does.
    /// </summary>
    private void PublishPeriod()
    {
        if (!_links.Joined)
        {
            return;
        }

        var period = Layout == CalendarStyle.Agenda
            ? CalendarShare.AgendaPeriod(_agendaDays, _agenda.PageStarts, _agendaPage)
            : CalendarShare.Period(Layout, _anchor, Today, Shown);
        if (period is not null)
        {
            _links.Set(LinkTopics.VisibleRange, period);
        }
    }

    /// <summary>[S-3626] Another calendar header or calendar paged the view: show its period (on joining, the one held there).</summary>
    private void OnPeriodElsewhere(RangeLink? period)
    {
        if (_detached || period is null)
        {
            return;
        }

        FollowPeriod(period);
    }

    /// <summary>
    /// [S-3628] Show the period the view shows: a month, a week, a day or a year anchors on the selected day, today or the
    /// period's first day (<see cref="CalendarShare.AnchorIn"/>) and shows its own period holding it; the agenda turns to the
    /// page holding that day. Never said again ([S-3626]).
    /// </summary>
    private void FollowPeriod(RangeLink period)
    {
        var day = CalendarShare.AnchorIn(period, Today, _selected);
        if (Layout == CalendarStyle.Agenda)
        {
            _agendaJump = day;
            Render();
            return;
        }

        var before = Range();
        _anchor = day;
        _followToday = InView(Today);
        Render();

        if (Range() != before)
        {
            _ = RefreshAsync(force: false);
        }
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

    /// <summary>[S-11923] An agenda heading for its day; forgotten when the kit's list draws its rows again.</summary>
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

        // [S-15791] The event pointed at elsewhere drawn now: every part drawn before it dims too (they were drawn whole while
        // it was not yet here), not only those drawn after it.
        if (_linkedHover is { } hovered && !CalendarShare.Dimmed(hovered, item))
        {
            OnHoveredElsewhere(hovered);
        }
        else
        {
            element.Opacity = EventStrength(_linkedHover, item);
        }

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

        // [S-15791] A birthday's chip is no event: it is among the others while an event drawn here is the one.
        var others = OthersStrength(value);
        foreach (var mark in _otherMarks)
        {
            mark.Opacity = others;
        }
    }

    /// <summary>[S-15791] The parts that are no event (a birthday's chip or dot): in the set of marks, never the event pointed at.</summary>
    private readonly List<FrameworkElement> _otherMarks = [];

    /// <summary>[S-15791] A part that is no event, drawn now: whole, or the links' "less" while an event drawn here is pointed at elsewhere.</summary>
    private void LinkOther(FrameworkElement element)
    {
        _otherMarks.Add(element);
        element.Opacity = OthersStrength(_linkedHover);
    }

    private double OthersStrength(EventLink? hovered)
        => hovered is not null && _eventElements.Any(drawn => !CalendarShare.Dimmed(hovered, drawn.Event)) ? LinkEmphasis.Others(pointed: true, picked: false) : 1;

    /// <summary>
    /// [S-3623] [S-11902] An event's chip against the event pointed at elsewhere: whole, or the links' "less"
    /// (0.6) while another event this calendar draws is the one; an event it does not draw dims nothing.
    /// </summary>
    private double EventStrength(EventLink? hovered, CalendarEvent item)
        => CalendarShare.Dimmed(hovered, item) && _eventElements.Any(drawn => !CalendarShare.Dimmed(hovered, drawn.Event))
            ? LinkEmphasis.Others(pointed: true, picked: false)
            : 1;
}
