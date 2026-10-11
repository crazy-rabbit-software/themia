using Themia.Core.Office;

namespace Themia.Core.Sync;

/// <summary>[S-2630] One calendar account's shelf: its calendar list, each synced calendar's window of events, and birthdays.</summary>
public sealed class CalendarShelf : ISyncShelf
{
    public const int Version = 1;

    public int SchemaVersion { get; set; } = Version;

    public string Owner { get; set; } = string.Empty;

    public List<StoredCalendar>? Calendars { get; set; }

    public DateTimeOffset? CalendarsAt { get; set; }

    public List<CalendarSpan> Spans { get; set; } = [];

    public List<StoredBirthday>? Birthdays { get; set; }

    public DateTimeOffset? BirthdaysAt { get; set; }
}

public sealed class StoredCalendar
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool IsDefault { get; set; }

    public string? Color { get; set; }
}

/// <summary>
/// One calendar's events over a window. <see cref="NearAt"/> is the last refresh of the weeks around
/// today, <see cref="FullAt"/> the last refresh of the whole window ([S-2631]).
/// </summary>
public sealed class CalendarSpan
{
    /// <summary>Null for the account's default calendar.</summary>
    public string? CalendarId { get; set; }

    public DateTimeOffset From { get; set; }

    public DateTimeOffset To { get; set; }

    public DateTimeOffset NearAt { get; set; }

    public DateTimeOffset FullAt { get; set; }

    public List<StoredEvent> Events { get; set; } = [];
}

public sealed class StoredEvent
{
    public string Id { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public DateTimeOffset Start { get; set; }

    public DateTimeOffset End { get; set; }

    public bool AllDay { get; set; }

    public string? Location { get; set; }

    public string? Organizer { get; set; }

    public string? WebLink { get; set; }
}

public sealed class StoredBirthday
{
    public string Name { get; set; } = string.Empty;

    public int Month { get; set; }

    public int Day { get; set; }

    public int? Year { get; set; }
}

/// <summary>[S-2640] One address book's shelf: the people the contacts widget lists.</summary>
public sealed class ContactsShelf : ISyncShelf
{
    public const int Version = 1;

    public int SchemaVersion { get; set; } = Version;

    public string Owner { get; set; } = string.Empty;

    public DateTimeOffset? At { get; set; }

    public List<StoredContact> Contacts { get; set; } = [];
}

public sealed class StoredContact
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public string? JobTitle { get; set; }

    public string? Company { get; set; }
}

/// <summary>[S-2630]..[S-2641] Bounds, validation and the conversions of the calendar and contacts shelves.</summary>
public static class OfficeShelves
{
    /// <summary>[S-2630] The most events kept for one calendar's window.</summary>
    public const int MaxEvents = 5000;

    /// <summary>[S-2640] The most people kept per address book: the most the contacts widget ever asks for ([S-583]).</summary>
    public const int MaxContacts = 1000;

    public const int MaxBirthdays = 5000;

    public const int MaxCalendars = 200;

    private const int MaxText = 1000;
    private const int MaxLink = 2048;

    public static StoredEvent Store(CalendarEvent item) => new()
    {
        Id = item.Id,
        Subject = Cut(item.Subject),
        Start = item.Start,
        End = item.End,
        AllDay = item.AllDay,
        Location = CutOrNull(item.Location),
        Organizer = CutOrNull(item.Organizer),
        WebLink = item.WebLink is { Length: <= MaxLink } link ? link : null,
    };

    public static CalendarEvent Restore(StoredEvent item) => new()
    {
        Id = item.Id,
        Subject = item.Subject,
        Start = item.Start,
        End = item.End,
        AllDay = item.AllDay,
        Location = item.Location,
        Organizer = item.Organizer,
        WebLink = item.WebLink,
    };

    public static StoredCalendar Store(CalendarInfo calendar) => new()
    {
        Id = calendar.Id,
        Name = Cut(calendar.Name),
        IsDefault = calendar.IsDefault,
        Color = CalendarRules.NormalizeColor(calendar.Color),
    };

    public static CalendarInfo Restore(StoredCalendar calendar) => new(calendar.Id, calendar.Name, calendar.IsDefault, calendar.Color);

    public static StoredBirthday Store(Birthday birthday) => new() { Name = Cut(birthday.Name), Month = birthday.Month, Day = birthday.Day, Year = birthday.Year };

    public static Birthday Restore(StoredBirthday birthday) => new(birthday.Name, birthday.Month, birthday.Day, birthday.Year);

    public static StoredContact Store(Contact contact) => new()
    {
        Id = contact.Id,
        Name = Cut(contact.Name),
        Email = CutOrNull(contact.Email),
        Phone = CutOrNull(contact.Phone),
        JobTitle = CutOrNull(contact.JobTitle),
        Company = CutOrNull(contact.Company),
    };

    public static Contact Restore(StoredContact contact, string accountId) => new()
    {
        Id = contact.Id,
        Name = contact.Name,
        Email = contact.Email,
        Phone = contact.Phone,
        JobTitle = contact.JobTitle,
        Company = contact.Company,
        AccountId = accountId,
    };

    /// <summary>
    /// [S-2632] A calendar shelf that opened: calendars without an id dropped and the list bounded; a
    /// span whose window is empty or repeated dropped; events without an id dropped, an end before its
    /// start set to the start, at most <see cref="MaxEvents"/> a span; birthdays outside the calendar
    /// dropped.
    /// </summary>
    public static CalendarShelf Validate(CalendarShelf shelf, ref int dropped)
    {
        ArgumentNullException.ThrowIfNull(shelf);

        List<StoredCalendar>? calendars = null;
        if (shelf.Calendars is { } stored)
        {
            calendars = [];
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var calendar in stored)
            {
                if (calendar is null || string.IsNullOrEmpty(calendar.Id) || !ids.Add(calendar.Id) || calendars.Count >= MaxCalendars)
                {
                    dropped++;
                    continue;
                }

                calendars.Add(new StoredCalendar
                {
                    Id = calendar.Id,
                    Name = Cut(calendar.Name),
                    IsDefault = calendar.IsDefault,
                    Color = CalendarRules.NormalizeColor(calendar.Color),
                });
            }
        }

        var spans = new List<CalendarSpan>();
        var calendarIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var span in shelf.Spans ?? [])
        {
            if (span is null || span.To <= span.From || !calendarIds.Add(span.CalendarId ?? string.Empty))
            {
                dropped++;
                continue;
            }

            var events = new List<StoredEvent>();
            var eventIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in span.Events ?? [])
            {
                if (item is null || string.IsNullOrEmpty(item.Id) || !eventIds.Add(item.Id) || events.Count >= MaxEvents)
                {
                    dropped++;
                    continue;
                }

                events.Add(new StoredEvent
                {
                    Id = item.Id,
                    Subject = Cut(item.Subject),
                    Start = item.Start,
                    End = item.End < item.Start ? item.Start : item.End,
                    AllDay = item.AllDay,
                    Location = CutOrNull(item.Location),
                    Organizer = CutOrNull(item.Organizer),
                    WebLink = item.WebLink is { Length: <= MaxLink } link ? link : null,
                });
            }

            spans.Add(new CalendarSpan
            {
                CalendarId = span.CalendarId,
                From = span.From,
                To = span.To,
                NearAt = span.NearAt,
                FullAt = span.FullAt,
                Events = events,
            });
        }

        List<StoredBirthday>? birthdays = null;
        if (shelf.Birthdays is { } held)
        {
            birthdays = [];
            foreach (var birthday in held)
            {
                if (birthday is null || string.IsNullOrWhiteSpace(birthday.Name) || birthday.Month is < 1 or > 12
                    || birthday.Day < 1 || birthday.Day > DateTime.DaysInMonth(2024, birthday.Month) || birthdays.Count >= MaxBirthdays)
                {
                    dropped++;
                    continue;
                }

                birthdays.Add(new StoredBirthday { Name = Cut(birthday.Name), Month = birthday.Month, Day = birthday.Day, Year = birthday.Year });
            }
        }

        return new CalendarShelf
        {
            Owner = shelf.Owner,
            Calendars = calendars,
            CalendarsAt = calendars is null ? null : shelf.CalendarsAt,
            Spans = spans,
            Birthdays = birthdays,
            BirthdaysAt = birthdays is null ? null : shelf.BirthdaysAt,
        };
    }

    /// <summary>[S-2641] A contacts shelf that opened: people need an id and a name; repeats and the overflow are dropped.</summary>
    public static ContactsShelf Validate(ContactsShelf shelf, ref int dropped)
    {
        ArgumentNullException.ThrowIfNull(shelf);

        var contacts = new List<StoredContact>();
        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var contact in shelf.Contacts ?? [])
        {
            if (contact is null || string.IsNullOrEmpty(contact.Id) || string.IsNullOrWhiteSpace(contact.Name)
                || !ids.Add(contact.Id) || contacts.Count >= MaxContacts)
            {
                dropped++;
                continue;
            }

            contacts.Add(new StoredContact
            {
                Id = contact.Id,
                Name = Cut(contact.Name),
                Email = CutOrNull(contact.Email),
                Phone = CutOrNull(contact.Phone),
                JobTitle = CutOrNull(contact.JobTitle),
                Company = CutOrNull(contact.Company),
            });
        }

        return new ContactsShelf { Owner = shelf.Owner, At = contacts.Count > 0 || shelf.At is not null ? shelf.At : null, Contacts = contacts };
    }

    private static string Cut(string? text) => text is null ? string.Empty : text.Length <= MaxText ? text : text[..MaxText];

    private static string? CutOrNull(string? text) => text is null ? null : Cut(text);
}

/// <summary>
/// [S-2631] The window each calendar is synced over, and how a refresh of part of it is folded in.
/// <para>
/// A calendar is held from a year before today to a year after it, so every layout — month, week,
/// day, agenda, a year — draws from the shelf without asking. Refreshing two years of events every
/// few minutes would be wasteful, so the weeks around today (a week back, six ahead) are refreshed
/// often and the whole window rarely; a near refresh replaces exactly the events it covers.
/// </para>
/// </summary>
public static class CalendarSync
{
    /// <summary>How far back and ahead of today the whole window reaches.</summary>
    public const int WindowDays = 365;

    public const int NearBackDays = 7;

    public const int NearAheadDays = 42;

    /// <summary>How often the weeks around today are asked again: the old poll ([S-588]).</summary>
    public static readonly TimeSpan NearInterval = TimeSpan.FromMinutes(5);

    /// <summary>How often the whole window is asked again.</summary>
    public static readonly TimeSpan FullInterval = TimeSpan.FromHours(6);

    public static readonly TimeSpan CalendarsInterval = TimeSpan.FromHours(6);

    public static readonly TimeSpan BirthdaysInterval = TimeSpan.FromHours(24);

    /// <summary>The whole window: local midnight a year back to local midnight a year ahead.</summary>
    public static (DateTimeOffset From, DateTimeOffset To) Window(DateOnly today, TimeZoneInfo zone)
        => CalendarRules.FetchWindow(today.AddDays(-WindowDays), today.AddDays(WindowDays), zone);

    public static (DateTimeOffset From, DateTimeOffset To) Near(DateOnly today, TimeZoneInfo zone)
        => CalendarRules.FetchWindow(today.AddDays(-NearBackDays), today.AddDays(NearAheadDays), zone);

    /// <summary>Whether a span answers a request for [<paramref name="from"/>, <paramref name="to"/>) on its own.</summary>
    public static bool Covers(CalendarSpan span, DateTimeOffset from, DateTimeOffset to)
    {
        ArgumentNullException.ThrowIfNull(span);
        return span.From <= from && to <= span.To;
    }

    /// <summary>Whether an event shows anywhere in [<paramref name="from"/>, <paramref name="to"/>): the providers' own overlap test.</summary>
    public static bool Overlaps(StoredEvent item, DateTimeOffset from, DateTimeOffset to)
    {
        ArgumentNullException.ThrowIfNull(item);

        // A zero-length event (a reminder at 09:00) is in the window that holds its start.
        return item.End > item.Start
            ? item.Start < to && item.End > from
            : item.Start >= from && item.Start < to;
    }

    /// <summary>The events of a span that a request for [<paramref name="from"/>, <paramref name="to"/>) is answered with.</summary>
    public static IReadOnlyList<CalendarEvent> Slice(CalendarSpan span, DateTimeOffset from, DateTimeOffset to)
    {
        ArgumentNullException.ThrowIfNull(span);
        return [.. span.Events.Where(e => Overlaps(e, from, to)).Select(OfficeShelves.Restore)];
    }

    /// <summary>
    /// Fold a fetch of [<paramref name="from"/>, <paramref name="to"/>) into a span. The fetch is the
    /// truth for what it covers: every held event that overlaps it, or that it returns by id, is
    /// replaced; the rest of the window is kept. A fetch that does not touch the held window (the
    /// window moved on by more than its length) starts the span again from the fetch alone, since a
    /// gap cannot be represented. The result keeps at most <see cref="OfficeShelves.MaxEvents"/>,
    /// nearest the fetch first.
    /// </summary>
    public static CalendarSpan Merge(CalendarSpan? held, string? calendarId, IReadOnlyList<CalendarEvent> fetched, DateTimeOffset from, DateTimeOffset to, DateTimeOffset now, bool full)
    {
        ArgumentNullException.ThrowIfNull(fetched);

        var incoming = fetched
            .Where(e => !string.IsNullOrEmpty(e.Id))
            .GroupBy(e => e.Id, StringComparer.Ordinal)
            .Select(g => OfficeShelves.Store(g.First()))
            .ToList();

        if (held is null || full || held.To < from || held.From > to)
        {
            return new CalendarSpan
            {
                CalendarId = calendarId,
                From = from,
                To = to,
                NearAt = now,
                FullAt = full ? now : held?.FullAt ?? default,
                Events = Bound(incoming, from),
            };
        }

        var replaced = new HashSet<string>(incoming.Select(e => e.Id), StringComparer.Ordinal);
        var kept = held.Events.Where(e => !replaced.Contains(e.Id) && !Overlaps(e, from, to));

        return new CalendarSpan
        {
            CalendarId = calendarId,
            From = held.From < from ? held.From : from,
            To = held.To > to ? held.To : to,
            NearAt = now,
            FullAt = held.FullAt,
            Events = Bound([.. incoming, .. kept], from),
        };
    }

    private static List<StoredEvent> Bound(List<StoredEvent> events, DateTimeOffset anchor)
        => events.Count <= OfficeShelves.MaxEvents
            ? [.. events.OrderBy(e => e.Start)]
            : [.. events.OrderBy(e => Math.Abs((e.Start - anchor).Ticks)).Take(OfficeShelves.MaxEvents).OrderBy(e => e.Start)];
}
