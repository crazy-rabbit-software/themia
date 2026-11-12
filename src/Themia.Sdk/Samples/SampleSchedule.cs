using Themia.App.Widgets;
using Themia.Core.Accounts;
using Themia.Core.Model;
using Themia.Core.Office;

namespace Themia.Sdk.Samples;

/// <summary>
/// The sample day ([S-11850], [S-2054]): Robin's calendar around the sample day, two calendars (Work on a
/// Microsoft account, Home on a CalDAV one: only what Themia offers, [S-3562]). Today is fixed: standup at nine, the design review at
/// eleven (the next meeting, eighteen minutes after the sample moment), lunch with Lena, a 1:1 with
/// Sam and the budget check-in with Northwind. Every other day of the four weeks around it follows its
/// weekday (two or three events on a workday, one or two at the weekend), with a few trips of several
/// days and a monthly all-hands across the year. The calendar, the week planner and the next meeting
/// all read it, so they name the same events.
/// </summary>
public static class SampleSchedule
{
    public const string WorkAccount = "sample:work";

    public const string HomeAccount = "sample:home";

    public const string HomeCalendar = "home";

    /// <summary>The next meeting's id, which a view link names.</summary>
    public const string NextMeetingId = "design-review-0";

    /// <summary>The next meeting's title.</summary>
    public const string NextMeetingSubject = "Design review: new onboarding";

    /// <summary>When the next meeting starts: eighteen minutes after the sample moment.</summary>
    public static TimeOnly NextMeetingStart { get; } = new(11, 0);

    /// <summary>How long the next meeting runs.</summary>
    public const int NextMeetingMinutes = 45;

    public const string NextMeetingRoom = "Room 4.12 · Microsoft Teams";

    /// <summary>The weekday events reach this many days either side of today; beyond, only the trips and the all-hands.</summary>
    private const int Reach = 27;

    public static IReadOnlyList<CalendarTarget> Targets { get; } = [new(WorkAccount, null), new(HomeAccount, HomeCalendar)];

    public static IReadOnlyDictionary<string, ProviderId> Providers { get; } = new Dictionary<string, ProviderId>(StringComparer.Ordinal)
    {
        [WorkAccount] = HostProviders.Microsoft,
        [HomeAccount] = HostProviders.CalDav,
    };

    public static IReadOnlyDictionary<string, string> Labels { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [WorkAccount] = "Work",
        [HomeAccount] = "Home",
    };

    private const string Teams = "Microsoft Teams";

    private const string JoinUrl = "https://teams.microsoft.com/l/meetup-join/19%3ameeting_sample%40thread.v2/0";

    /// <summary>One timed event of a day: which calendar (0 Work, 1 Home), a key for its id, and what it is.</summary>
    private readonly record struct Slot(int Calendar, string Key, string Subject, int Hour, int Minute, int Minutes, string? Location = null, string? Organizer = null, bool Joins = false);

    /// <summary>Today's events, the sample moment between the standup and the design review.</summary>
    private static readonly Slot[] TodaySlots =
    [
        new(0, "standup", "Standup", 9, 0, 15, Teams),
        new(0, "design-review", NextMeetingSubject, 11, 0, NextMeetingMinutes, NextMeetingRoom, SampleCast.Ana.Name, Joins: true),
        new(1, "lunch-lena", "Lunch with Lena", 12, 30, 60, "Café am See"),
        new(0, "one-on-one", "1:1 with Sam", 14, 0, 30, "Room 3.02", SampleCast.Owner.Name),
        new(0, "budget", "Budget check-in with Northwind", 16, 0, 30, Teams, SampleCast.Bea.Name, Joins: true),
    ];

    /// <summary>Every other day, by its weekday.</summary>
    private static Slot[] Weekday(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday =>
        [
            new(0, "standup", "Standup", 9, 0, 15, Teams),
            new(0, "planning", "Sprint planning", 10, 0, 60, "Room 4.12", SampleCast.Dana.Name),
            new(1, "climbing", "Climbing", 18, 30, 90),
        ],
        DayOfWeek.Tuesday =>
        [
            new(0, "standup", "Standup", 9, 0, 15, Teams),
            new(0, "crit", "Design crit", 13, 30, 60, "Room 4.12", SampleCast.Ana.Name),
            new(1, "yoga", "Yoga", 18, 0, 60),
        ],
        DayOfWeek.Wednesday =>
        [
            new(0, "standup", "Standup", 9, 0, 15, Teams),
            new(0, "product-sync", "Product sync", 11, 0, 45, Teams, SampleCast.Priya.Name, Joins: true),
            new(1, "dinner-mia", "Dinner with Mia", 19, 30, 120),
        ],
        DayOfWeek.Thursday =>
        [
            new(0, "standup", "Standup", 9, 0, 15, Teams),
            new(0, "northwind", "Northwind status call", 14, 0, 60, Teams, SampleCast.Liam.Name, Joins: true),
            new(0, "release", "Release check", 14, 30, 30, "Room 3.02", SampleCast.Dana.Name),
        ],
        DayOfWeek.Friday =>
        [
            new(0, "standup", "Standup", 9, 0, 15, Teams),
            new(0, "retro", "Retro", 15, 0, 60, "Room 4.12", SampleCast.Dana.Name),
        ],
        DayOfWeek.Saturday =>
        [
            new(1, "market", "Farmers' market", 9, 30, 60),
            new(1, "hike", "Hike with Lena", 13, 0, 180),
        ],
        _ =>
        [
            new(1, "brunch", "Brunch with Jonas", 11, 0, 90),
        ],
    };

    /// <summary>
    /// The events from a year before <paramref name="today"/> to a year after, in no order. With
    /// <paramref name="freeToday"/> today has nothing on it (the week planner's free day).
    /// </summary>
    public static IReadOnlyList<CalendarEvent> Events(DateOnly today, TimeZoneInfo zone, bool freeToday = false)
    {
        ArgumentNullException.ThrowIfNull(zone);
        var events = new List<CalendarEvent>();

        // The trips of several days; a trip's days have nothing else on them.
        var toSaturday = ((int)DayOfWeek.Saturday - (int)today.DayOfWeek + 7) % 7;
        var weekend = today.AddDays(toSaturday == 0 ? 7 : toSaturday);
        (int Calendar, string Key, string Subject, DateOnly First, int Days)[] trips =
        [
            (1, "mountains", "Weekend in the mountains", weekend, 2),
            (0, "offsite", "Offsite in Lisbon", weekend.AddDays(9), 3),
            (0, "conference", "Design conference", today.AddDays(-12), 2),
            (1, "summer", "Summer holiday", today.AddDays(-100), 10),
            (1, "ski", "Ski week", today.AddDays(90), 6),
        ];

        foreach (var trip in trips)
        {
            events.Add(CalendarEvent.AllDayEvent($"{trip.Key}-{trip.First.DayNumber - today.DayNumber}", trip.Subject, trip.First, trip.First.AddDays(trip.Days))
                with { AccountId = Targets[trip.Calendar].AccountId, CalendarId = Targets[trip.Calendar].CalendarId, SourceIndex = trip.Calendar });
        }

        bool Away(DateOnly day) => trips.Any(t => day >= t.First && day < t.First.AddDays(t.Days));

        for (var offset = -Reach; offset <= Reach; offset++)
        {
            var day = today.AddDays(offset);
            Slot[] slots = offset == 0 ? (freeToday ? [] : TodaySlots) : Away(day) ? [] : Weekday(day.DayOfWeek);
            events.AddRange(slots.Select(slot => Timed(slot, day, offset, zone)));
        }

        // An overnight train home after the offsite, across midnight.
        var train = weekend.AddDays(11);
        events.Add(Timed(new Slot(1, "night-train", "Night train to Zürich", 21, 40, 600), train, train.DayNumber - today.DayNumber, zone));

        // The all-hands on the first Tuesday of every month, a year either way (not today: today is full).
        for (var month = -12; month <= 12; month++)
        {
            var first = new DateOnly(today.Year, today.Month, 1).AddMonths(month);
            var tuesday = first.AddDays(((int)DayOfWeek.Tuesday - (int)first.DayOfWeek + 7) % 7);
            if (!Away(tuesday) && tuesday != today)
            {
                events.Add(Timed(new Slot(0, "all-hands", "All-hands", 16, 30, 60, Teams, Joins: true), tuesday, tuesday.DayNumber - today.DayNumber, zone));
            }
        }

        return events;
    }

    /// <summary>The address book's birthdays: Mia's today, Jonas's in three days, Lena's in forty.</summary>
    public static IReadOnlyList<Birthday> Birthdays(DateOnly today) =>
    [
        new(SampleCast.Mia.Name, today.Month, today.Day, 1994),
        new(SampleCast.Jonas.Name, today.AddDays(3).Month, today.AddDays(3).Day, null),
        new(SampleCast.Lena.Name, today.AddDays(40).Month, today.AddDays(40).Day, 1988),
    ];

    private static CalendarEvent Timed(Slot slot, DateOnly day, int offset, TimeZoneInfo zone)
    {
        var start = SampleDay.At(day, new TimeOnly(slot.Hour, slot.Minute), zone);
        return new CalendarEvent
        {
            Id = $"{slot.Key}-{offset}",
            Subject = slot.Subject,
            Start = start,
            End = start.AddMinutes(slot.Minutes),
            Location = slot.Location,
            Organizer = slot.Organizer,
            OrganizerAddress = SampleCast.Contacts.Append(SampleCast.Owner).FirstOrDefault(p => p.Name == slot.Organizer)?.Email,
            JoinUrl = slot.Joins ? JoinUrl : null,
            AccountId = Targets[slot.Calendar].AccountId,
            CalendarId = Targets[slot.Calendar].CalendarId,
            SourceIndex = slot.Calendar,
        };
    }
}

/// <summary>
/// The sample day ([S-11850], [S-2054]): <see cref="SampleSchedule"/> as a calendar widget's data seam, for
/// the calendar, the week planner and the next meeting. <c>empty</c> holds no event, <c>freeToday</c>
/// nothing today, <c>stale</c> the Work calendar's last sync failed (no network) twenty minutes ago.
/// </summary>
public sealed class SampleCalendarData(bool empty = false, bool freeToday = false, bool stale = false) : ICalendarData
{
    public event Action? Changed
    {
        add { }
        remove { }
    }

    public Task<CalendarSlice> LoadAsync(CalendarConfig config, DateOnly first, DateOnly last, bool force, CancellationToken cancellation)
        => Task.FromResult(Slice(first, last, TimeZoneInfo.Local));

    /// <summary>The slice for <paramref name="first"/> to <paramref name="last"/> (inclusive, local days).</summary>
    public CalendarSlice Slice(DateOnly first, DateOnly last, TimeZoneInfo zone)
    {
        var today = SampleDay.Today;
        var (from, to) = CalendarRules.FetchWindow(first, last, zone);
        IReadOnlyList<CalendarEvent> events = empty ? [] : [.. SampleSchedule.Events(today, zone, freeToday)
            .Where(e => e.AllDay ? e.FirstDay <= last && e.EndDay > first : e.Start < to && e.End > from)];
        IReadOnlyList<(string Account, SyncProblem Problem, DateTimeOffset? At)> problems = stale
            ? [(SampleSchedule.WorkAccount, SyncProblem.Offline, SampleDay.MinutesAgo(20))]
            : [];
        return new CalendarSlice(CalendarSliceState.Asked, events, empty ? [] : SampleSchedule.Birthdays(today), SampleSchedule.Targets,
            SampleSchedule.Providers, SampleSchedule.Labels, problems, true);
    }
}
