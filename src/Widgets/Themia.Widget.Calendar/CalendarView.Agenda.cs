using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.Calendar;

namespace Themia.App.Widgets;

/// <summary>
/// [S-571] / [S-2709] [S-11540] The agenda: what is coming up, grouped by day, in pages of the config's
/// rows (one column, its lines), paged with the header's arrows and the keys rather than scrolled. The rows are the kit's (<see cref="KitList"/>,
/// the schedule look on pages with day groups, [S-7035], [S-7036]); what an item says is
/// <see cref="AgendaSpec"/>'s.
/// </summary>
internal sealed partial class CalendarView
{
    /// <summary>
    /// The calendar's event rows: the kit's paged list, the agenda style's, and under a narrow week the
    /// chosen day's ([S-2285]): one row path.
    /// </summary>
    private readonly KitList _agenda;

    /// <summary>The agenda's items on screen by id, for the rows' hooks and Enter.</summary>
    private readonly Dictionary<string, DayItem> _agendaItems = new(StringComparer.Ordinal);

    /// <summary>[S-11923] The day each row on screen is listed under.</summary>
    private readonly Dictionary<string, DateOnly> _agendaDays = new(StringComparer.Ordinal);

    /// <summary>
    /// The room over the rows: none in the agenda, the strip and its gap in a narrow week ([S-2283]).
    /// A height, not a margin, so the rows stand on the family's spaces.
    /// </summary>
    private readonly Border _rowsTop = new() { Height = 0 };

    /// <summary>The rows under <see cref="_rowsTop"/>; with no background, so a click over the strip reaches it.</summary>
    private Grid RowsHost()
    {
        var host = new Grid();
        host.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        host.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(_agenda, 1);
        host.Children.Add(_rowsTop);
        host.Children.Add(_agenda);
        return host;
    }

    private KitList NewAgenda(Themia.Sdk.WinUI.IWidgetHost host) => new(host, AgendaSpec.Spec, AgendaActions, RunAgenda)
    {
        Decorate = DecorateAgendaRow,
        DecorateHeading = (group, heading) =>
        {
            if (group.Day is { } day)
            {
                LinkRowDay(heading, day);
            }
        },
        Redrawing = ForgetRowDays,
        Visibility = Visibility.Collapsed,
    };

    private void DrawAgenda(double width, double height)
    {
        var today = Today;
        var (first, last) = CalendarRules.ViewRange(CalendarStyle.Agenda, _anchor, today, _config.Period);
        var groups = CalendarRules.Agenda(_events, _birthdays, first, last, TimeZoneInfo.Local, _config.EffectiveAgendaCount, _clock());
        var rows = groups.SelectMany(g => g.Items.Select(i => (g.Day, Item: i))).ToList();

        _agendaItems.Clear();
        _agendaDays.Clear();

        if (rows.Count == 0)
        {
            HideAgenda();
            _agendaPages = 1;
            _agendaPage = 0;
            _state.Show(CalendarStr.Calendar_NoUpcoming.Text(), offerAccounts: false);
            return;
        }

        var showColors = ShowColors;
        var now = _clock();
        var items = new List<KitItem>(rows.Count);
        foreach (var (day, item) in rows)
        {
            var kit = AgendaSpec.Item(item, day, now, _selected, showColors ? MarkOf : null);
            _agendaItems[kit.Id] = item;
            _agendaDays[kit.Id] = day;
            items.Add(kit);
        }

        var area = new Themia.Core.Widgets.WidgetArea(width, height);
        // [S-11540] The config's rows and lines at every size: the height was made for them.
        var plan = KitPlanner.Plan(area, AgendaSpec.Spec, new KitChoices(KitShow.None) { Rows = Themia.Widget.Calendar.CalendarSizing.AgendaShown(_config) });
        _rowsTop.Height = 0;
        _agenda.Visibility = Visibility.Visible;
        _agenda.Page = _agendaPage;
        _agenda.Show(items, plan, area, now);

        // [S-3622] A day selected on the view turns the agenda to the page that holds it.
        if (_agendaJump is { } jump)
        {
            _agendaJump = null;
            if (CalendarShare.AgendaPageOf([.. rows.Select(r => r.Day)], _agenda.PageStarts, jump) is { } page && page != _agenda.Page)
            {
                _agenda.Page = page;
                _agenda.Show(items, plan, area, now);
            }
        }

        _agendaPage = _agenda.Page;
        _agendaPages = _agenda.PageCount;
    }

    /// <summary>[S-566] An event's calendar colour as the user chose it, resolved at draw time so a settings change repaints at once.</summary>
    private string MarkOf(CalendarEvent item)
        => CalendarRules.ResolveColor(CalendarRules.ColorKey(item.AccountId, item.CalendarId), item.SourceIndex, _config.CalendarColors);

    /// <summary>What only the calendar knows of a row: its tooltip and its links across widgets ([S-3623]); the kit's list opens it ([S-3652]).</summary>
    private void DecorateAgendaRow(KitItem item, FrameworkElement row)
    {
        if (_agendaItems.TryGetValue(item.Id, out var day) && day.Event is { } ev)
        {
            Describe(row, ev);

            // [S-11940] An event's row is its organiser as well: pointed at, and lit while they are.
            People.Row(row, CalendarShare.Person(ev));
        }

        // [S-11923] An event's row is its day: the pointer on it publishes that day, that day pointed at lights it.
        if (_agendaDays.TryGetValue(item.Id, out var listed))
        {
            LinkRowDay(row, listed);
        }
    }

    /// <summary>[S-3652] A row's actions: Open, for an event with a page to open.</summary>
    private IReadOnlyList<KitAction> AgendaActions(KitItem item)
        => _agendaItems.TryGetValue(item.Id, out var day) ? AgendaSpec.Actions(day, day.Event is { } ev && Openable(ev)) : [];

    private void RunAgenda(KitItem item, KitAction action)
    {
        if (action.Id == AgendaSpec.OpenId && _agendaItems.TryGetValue(item.Id, out var day) && day.Event is { } ev)
        {
            Open(ev);
        }
    }

    /// <summary>Take the agenda's rows down (another style, or a state with no list).</summary>
    private void HideAgenda()
    {
        _agenda.Clear();
        _agenda.Visibility = Visibility.Collapsed;
        _agendaItems.Clear();
        _agendaDays.Clear();
    }
}
