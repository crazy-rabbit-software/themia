using Microsoft.UI.Xaml;
using Themia.Core.Links;
using Themia.Core.Model;
using Themia.Core.Office;

namespace Themia.App.Widgets;

/// <summary>
/// [S-3620]..[S-3625] The calendar's place on its view's links ([S-3300]): the selected day it shares
/// with the other calendar widgets there, the range it was paged to, and the event under the pointer.
/// Who follows what is Core (<see cref="CalendarShare"/>); this publishes, listens and redraws.
/// Without a board (a host with no view) the selection is the widget's own.
/// </summary>
internal sealed partial class CalendarView
{
    /// <summary>[S-3623] How far an event another widget does not point at fades: the screen-time widgets' dim.</summary>
    private const double DimOpacity = 0.35;

    /// <summary>[S-3623] Every event element drawn now, so a hover from another widget dims without a redraw.</summary>
    private readonly List<(FrameworkElement Element, CalendarEvent Event)> _eventElements = [];

    private LinkMember? _member;

    /// <summary>[S-3620] The selected day, this widget's or another's on the view; also the keys' cursor day ([S-3624]).</summary>
    private DateOnly? _selected;

    /// <summary>[S-3623] The event another widget's pointer is on.</summary>
    private EventLink? _linkedHover;

    /// <summary>[S-3622] A day the agenda turns its page to on its next draw, once it knows its pages.</summary>
    private DateOnly? _agendaJump;

    /// <summary>Whether the selected day is in the period on screen.</summary>
    private bool SelectedInView => _selected is { } day && InView(day);

    private void JoinLinks(LinkBoard? board)
    {
        LeaveLinks();

        if (board is null)
        {
            return;
        }

        _member = board.Join();
        _linkedHover = _member.Get(LinkTopics.HoveredEvent);
        _member.Watch(LinkTopics.SelectedDate, OnSelectedElsewhere);
        _member.Watch(LinkTopics.HoveredEvent, OnHoveredElsewhere);

        // [S-3305] A view shown again: what was selected there is followed at once.
        if (_member.Get(LinkTopics.SelectedDate) is { } selected)
        {
            _selected = selected.Date;
            Follow(selected.Date);
        }
    }

    /// <summary>Leave the board (Detach): this widget's hover goes with it, the selection stays ([S-3304]).</summary>
    private void LeaveLinks()
    {
        _member?.Dispose();
        _member = null;
        _linkedHover = null;
    }

    /// <summary>
    /// [S-3620] Select a day (null: let the selection go), here and on the view: every calendar there
    /// follows it ([S-3621]).
    /// </summary>
    private void Select(DateOnly? day)
    {
        _selected = day;
        _member?.Set(LinkTopics.SelectedDate, day is { } d ? new DateLink(d) : null);

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
        if (_detached)
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
        if (Layout == CalendarLayout.Agenda)
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
        if (_member is not { } member || Layout == CalendarLayout.Agenda)
        {
            return;
        }

        var (first, last) = Range();
        member.Set(LinkTopics.VisibleRange, new RangeLink(first, last));
    }

    /// <summary>[S-3623] An event drawn: it lights the same event in the other calendars while pointed at, and dims when another is.</summary>
    private void LinkEvent(FrameworkElement element, CalendarEvent item)
    {
        _eventElements.Add((element, item));
        element.Opacity = CalendarShare.Dimmed(_linkedHover, item) ? DimOpacity : 1;

        var key = CalendarShare.Key(item);
        element.PointerEntered += (_, _) =>
        {
            // The pointer here replaces whatever another widget lit: nothing here stays dimmed by it.
            _member?.Set(LinkTopics.HoveredEvent, key);
            OnHoveredElsewhere(null);
        };


        // [S-3304] Only this widget's own hover is taken away by its pointer leaving.
        element.PointerExited += (_, _) => _member?.Clear(LinkTopics.HoveredEvent);
    }

    private void OnHoveredElsewhere(EventLink? value)
    {
        _linkedHover = value;

        foreach (var (element, item) in _eventElements)
        {
            element.Opacity = CalendarShare.Dimmed(value, item) ? DimOpacity : 1;
        }
    }
}
