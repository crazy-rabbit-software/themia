using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Widget.Calendar;

/// <summary>
/// [S-3650] The agenda on the kit: its spec (the schedule look, paged, [S-7035], [S-7036]) and an
/// agenda row as a <see cref="KitItem"/>. Pure, so what a row says is tested here and not in a view.
/// </summary>
public static class AgendaSpec
{
    public static KitSpec Spec { get; } = new(KitRowLook.Schedule, KitShow.None, () => Str.Calendar_NoTitle.Text(), () => CalendarStr.Calendar_NoUpcoming.Text(), KitTime.None, KitOverflow.Page, MaxColumns: 2)
    {
        // [S-571] The widest the time column says: "All day" and "Now" run far longer than a clock
        // in some languages; the clock as the rows write it, in the language's digits and separator.
        TimeLabels = () =>
        [
            (Str.Calendar_AllDay.Text(), false),
            (CalendarStr.WidgetsA_Calendar_Now.Text(), true),
            (LocaleFormat.Time(Strings.Current, TimeOnly.MinValue, HourCycle.H24), false),
        ],
    };

    /// <summary>
    /// [S-571] One agenda row: an event's part on <paramref name="day"/> (a timed event over several
    /// days reads its own hours on its first and last day and "All day" between; "Now" in the strong
    /// step while it is under way), where and who; a birthday its gift, its name and "Birthday".
    /// </summary>
    /// <param name="mark">The calendar colour of an event, when colours show.</param>
    public static KitItem Item(DayItem item, DateOnly day, DateTimeOffset now, DateOnly? selected, Func<CalendarEvent, string>? mark)
    {

        var group = new KitGroup(day.ToString("O", System.Globalization.CultureInfo.InvariantCulture), Day: day, Selected: day == selected);

        if (item.Birthday is { } b)
        {
            return new KitItem(
                $"{group.Key}|birthday|{b.Name}",
                BirthdayRules.Label(b, day),
                Lead: new KitLead(KitLeadKind.Icon, nameof(KitIcon.Gift), KitTone.Caution),
                Tone: KitTone.Caution,
                Group: group,
                Schedule: new KitSchedule(null, Note: CalendarStr.WidgetsA_Calendar_Birthday.Text()));
        }

        var e = item.Event!;
        var part = CalendarRules.AgendaSpan(e, day, TimeZoneInfo.Local);
        var running = !part.WholeDay && AgendaRules.IsInProgress(false, part.Start, part.End, now);
        var start = part.WholeDay ? Str.Calendar_AllDay.Text() : running ? CalendarStr.WidgetsA_Calendar_Now.Text() : Clock(part.Start);
        var span = part.WholeDay || running || part.End <= part.Start ? start : $"{start}–{Clock(part.End)}";

        return new KitItem(
            $"{group.Key}|{e.AccountId}|{e.CalendarId}|{e.Id}",
            string.IsNullOrWhiteSpace(e.Subject) ? Str.Calendar_NoTitle.Text() : e.Subject,
            Group: group,
            Schedule: new KitSchedule(
                start,
                !part.WholeDay && part.End > part.Start ? Clock(part.End) : null,
                Strong: running,
                Word: part.WholeDay || running,
                Span: span,
                Where: e.Location,
                Who: e.Organizer,
                Mark: mark?.Invoke(e)));
    }

    private static string Clock(DateTimeOffset moment) => Formatting.Clock24(moment);
}
