namespace Themia.Core.Office;

/// <summary>
/// [S-560] One calendar event, already read off whichever provider sent it and reduced to what the
/// widget draws.
/// <para>
/// A timed event carries two instants. An all-day event carries two <b>dates</b> — the first day and
/// the exclusive end, as RFC 5545, Graph and Google all mean it — held in <see cref="Start"/> and
/// <see cref="End"/> at midnight with a zero offset. Those are date carriers, never instants: an
/// all-day event is a label on a square of the wall calendar and stays on that square in every zone,
/// so nothing may convert them. <see cref="FirstDay"/> and <see cref="EndDay"/> are the only reads.
/// </para>
/// </summary>
public sealed record CalendarEvent
{
    /// <summary>The provider's id: a Graph or Google event id, or the iCalendar UID (plus the occurrence).</summary>
    public required string Id { get; init; }

    public string Subject { get; init; } = string.Empty;

    /// <summary>The start instant, or the first day at midnight with a zero offset when <see cref="AllDay"/>.</summary>
    public required DateTimeOffset Start { get; init; }

    /// <summary>The end instant, or the exclusive last day when <see cref="AllDay"/>.</summary>
    public required DateTimeOffset End { get; init; }

    public bool AllDay { get; init; }

    public string? Location { get; init; }

    public string? Organizer { get; init; }

    /// <summary>The provider's own page for the event; null for CalDAV, which has none.</summary>
    public string? WebLink { get; init; }

    /// <summary>[S-4822] The online meeting's join link, already through <see cref="MeetingLinks"/>; null when there is none.</summary>
    public string? JoinUrl { get; init; }

    /// <summary>[S-4821] The signed-in user declined it (Graph's or Google's response status). CalDAV cannot say.</summary>
    public bool Declined { get; init; }

    /// <summary>The account it came from; stamped by the widget, which knows which target it asked.</summary>
    public string AccountId { get; init; } = string.Empty;

    /// <summary>The calendar it came from, or null for the account's default calendar.</summary>
    public string? CalendarId { get; init; }

    /// <summary>The position of its fetch target, which picks its palette colour ([S-566]).</summary>
    public int SourceIndex { get; init; }

    /// <summary>The first day an all-day event covers. Meaningless for a timed event.</summary>
    public DateOnly FirstDay => DateOnly.FromDateTime(Start.DateTime);

    /// <summary>
    /// The exclusive end day of an all-day event, never before the day after <see cref="FirstDay"/>:
    /// a provider that sends an end equal to the start (some do) still means one day.
    /// </summary>
    public DateOnly EndDay
    {
        get
        {
            var end = DateOnly.FromDateTime(End.DateTime);
            return end > FirstDay ? end : FirstDay.AddDays(1);
        }
    }

    /// <summary>An all-day event, from its first day and its exclusive end day.</summary>
    public static CalendarEvent AllDayEvent(string id, string subject, DateOnly first, DateOnly endExclusive) => new()
    {
        Id = id,
        Subject = subject,
        Start = DateCarrier(first),
        End = DateCarrier(endExclusive),
        AllDay = true,
    };

    /// <summary>A date as the midnight-UTC carrier all-day events hold.</summary>
    public static DateTimeOffset DateCarrier(DateOnly day) => new(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
}

/// <summary>[S-567] One calendar an account exposes, for the per-account calendar picker.</summary>
/// <param name="Id">Graph or Google calendar id, or the absolute CalDAV collection URL.</param>
/// <param name="Name">What the provider calls it.</param>
/// <param name="IsDefault">The account's default (primary) calendar, which a fresh widget shows.</param>
/// <param name="Color">The provider's own colour as <c>#rrggbb</c>, or null ([S-566]).</param>
public sealed record CalendarInfo(string Id, string Name, bool IsDefault, string? Color);

/// <summary>One (account, calendar) pair the calendar widget fetches. A null calendar is the default one.</summary>
public readonly record struct CalendarTarget(string AccountId, string? CalendarId);

/// <summary>[S-575] A contact's birthday. The year is optional: many address books do not hold one.</summary>
public sealed record Birthday(string Name, int Month, int Day, int? Year);

/// <summary>[S-580] One person in an address book, reduced to the fields the contacts widget shows.</summary>
public sealed record Contact
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public string? Email { get; init; }

    public string? Phone { get; init; }

    public string? JobTitle { get; init; }

    public string? Company { get; init; }

    public string AccountId { get; init; } = string.Empty;
}

/// <summary>An event or a birthday on one day, in the order a day lists them.</summary>
public readonly record struct DayItem(CalendarEvent? Event, Birthday? Birthday)
{
    public bool IsBirthday => Birthday is not null;
}
