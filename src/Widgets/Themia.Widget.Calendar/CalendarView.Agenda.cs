using Microsoft.UI.Xaml;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.Calendar;

namespace Themia.App.Widgets;

/// <summary>
/// [S-571] / [S-2709] The agenda: what is coming up, grouped by day, in pages of the rows that fit
/// whole — a small tile shows a couple of rows, a large one two columns of them — paged with the
/// header's arrows and the keys rather than scrolled. The rows are the kit's (<see cref="KitList"/>,
/// the schedule look on pages with day groups, [S-7035], [S-7036]); what an item says is
/// <see cref="AgendaSpec"/>'s.
/// </summary>
internal sealed partial class CalendarView
{
    /// <summary>The agenda's rows: the kit's paged list, shown only in the agenda style.</summary>
    private readonly KitList _agenda;

    /// <summary>The agenda's items on screen by id, for the rows' hooks and Enter.</summary>
    private readonly Dictionary<string, DayItem> _agendaItems = new(StringComparer.Ordinal);

    private KitList NewAgenda(Themia.Sdk.WinUI.IWidgetHost host) => new(host, AgendaSpec.Spec, _ => [], (_, _) => { })
    {
        Decorate = DecorateAgendaRow,
        Visibility = Visibility.Collapsed,
    };

    private void DrawAgenda(double width, double height)
    {
        var today = Today;
        var (first, last) = CalendarRules.ViewRange(CalendarStyle.Agenda, _anchor, today, _config.Period);
        var groups = CalendarRules.Agenda(_events, _birthdays, first, last, TimeZoneInfo.Local, _config.EffectiveAgendaCount, DateTimeOffset.Now);
        var rows = groups.SelectMany(g => g.Items.Select(i => (g.Day, Item: i))).ToList();

        _agendaFirst = null;
        _agendaItems.Clear();

        if (rows.Count == 0)
        {
            HideAgenda();
            _agendaPages = 1;
            _agendaPage = 0;
            _state.Show(CalendarStr.Calendar_NoUpcoming.Text(), offerAccounts: false);
            return;
        }

        var showColors = ShowColors;
        var now = DateTimeOffset.Now;
        var items = new List<KitItem>(rows.Count);
        foreach (var (day, item) in rows)
        {
            var kit = AgendaSpec.Item(item, day, now, _selected, showColors ? MarkOf : null);
            _agendaItems[kit.Id] = item;
            items.Add(kit);
        }

        var area = new Themia.Core.Widgets.WidgetArea(width, height);
        var plan = KitLadder.Plan(area, AgendaSpec.Spec, KitShow.None);
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
        // [S-2710] Enter's event: the page's first that is an event (a birthday opens nothing).
        var starts = _agenda.PageStarts;
        var end = _agendaPage + 1 < starts.Count ? starts[_agendaPage + 1] : items.Count;
        _agendaFirst = items.Skip(starts[_agendaPage]).Take(end - starts[_agendaPage])
            .Select(i => _agendaItems[i.Id].Event).FirstOrDefault(e => e is not null);
    }

    /// <summary>[S-566] An event's calendar colour as the user chose it, resolved at draw time so a settings change repaints at once.</summary>
    private string MarkOf(CalendarEvent item)
        => CalendarRules.ResolveColor(CalendarRules.ColorKey(item.AccountId, item.CalendarId), item.SourceIndex, _config.CalendarColors);

    /// <summary>What only the calendar knows of a row: its tooltip, its links across widgets ([S-3623]) and the click that opens it.</summary>
    private void DecorateAgendaRow(KitItem item, FrameworkElement row)
    {
        if (_agendaItems.TryGetValue(item.Id, out var day) && day.Event is { } ev)
        {
            MakeOpenable(row, ev);
        }
    }

    /// <summary>Take the agenda's rows down (another style, or a state with no list).</summary>
    private void HideAgenda()
    {
        _agenda.Clear();
        _agenda.Visibility = Visibility.Collapsed;
        _agendaItems.Clear();
    }
}
