using System.Text.Json.Serialization;
using Themia.Core.Office;

namespace Themia.Core.Model;

/// <summary>
/// [S-3600] The five ways a calendar widget lays out time, one widget kind each
/// (<see cref="CalendarConfig.Layout"/>). Never stored: the kind says it. Each sheds detail within
/// itself at every size, never turning into another ([S-2700]).
/// </summary>
public enum CalendarLayout
{
    /// <summary>The weeks of a month, the original's default.</summary>
    Month = 0,

    /// <summary>Seven day columns.</summary>
    Week = 1,

    /// <summary>One day on an hour timeline.</summary>
    Day = 2,

    /// <summary>A list of what is coming up, grouped by day.</summary>
    Agenda = 3,

    /// <summary>The twelve months of a year as small grids.</summary>
    Year = 4,
}

/// <summary>[S-564] Which day a week starts on: a closed set, never a stringly day name.</summary>
[JsonConverter(typeof(LenientEnumConverter<WeekStartDay>))]
public enum WeekStartDay
{
    Monday = 0,
    Sunday = 1,

    /// <summary>The Iranian and Afghan week (Persian), which Windows' fa-IR also starts on Saturday.</summary>
    Saturday = 2,
}

/// <summary>
/// [S-2719] [S-3637] Where the week calendar's first column is: the first day of the week (the shared
/// regional setting, [S-3770]), or today, for the days coming up.
/// </summary>
[JsonConverter(typeof(LenientEnumConverter<WeekFirstDay>))]
public enum WeekFirstDay
{
    /// <summary>The first day of the week, the regional setting ([S-3770]).</summary>
    Week = 0,

    Today = 1,
}

/// <summary>[S-581] The four ways the contacts widget lays out people.</summary>
[JsonConverter(typeof(LenientEnumConverter<ContactsLayout>))]
public enum ContactsLayout
{
    /// <summary>Avatar, name, job · company and the address or number.</summary>
    List = 0,

    /// <summary>A small avatar, the name and one secondary field.</summary>
    Compact = 1,

    /// <summary>A table of the chosen columns.</summary>
    Details = 2,

    /// <summary>Avatar cards.</summary>
    Grid = 3,
}

/// <summary>[S-582] A column of the contacts widget's details table.</summary>
[JsonConverter(typeof(LenientEnumConverter<ContactColumn>))]
public enum ContactColumn
{
    Name = 0,
    Email = 1,
    Phone = 2,
    Company = 3,
    Title = 4,
}

/// <summary>
/// [S-561] [S-3601] What every calendar widget shares: which accounts and calendars, their colours,
/// and whether birthdays are shown. Data comes from Themia's own accounts ([S-529]). Every value is
/// read leniently ([S-563]): a wrong one becomes its default rather than refusing the state file.
/// Each of the five calendar kinds adds only what it uses.
/// </summary>
public abstract class CalendarConfig : WidgetConfig
{
    /// <summary>[S-3600] The layout this kind draws: fixed by the kind, never stored.</summary>
    [JsonIgnore]
    public abstract CalendarLayout Layout { get; }

    /// <summary>[S-3600] A fresh config of the kind that draws <paramref name="layout"/>.</summary>
    public static CalendarConfig For(CalendarLayout layout) => layout switch
    {
        CalendarLayout.Week => new WeekCalendarConfig(),
        CalendarLayout.Day => new DayCalendarConfig(),
        CalendarLayout.Year => new YearCalendarConfig(),
        CalendarLayout.Agenda => new AgendaConfig(),
        _ => new MonthCalendarConfig(),
    };

    /// <summary>[S-562] The accounts to show; empty means every calendar account.</summary>
    [JsonConverter(typeof(LenientIdListConverter))]
    public List<string> AccountIds { get; set; } = [];

    /// <summary>
    /// [S-567] Per account, the calendars selected. An account absent here (or with an empty list)
    /// shows its default calendar, so a fresh widget starts primary-only.
    /// </summary>
    [JsonConverter(typeof(LenientCalendarMapConverter))]
    public Dictionary<string, List<string>> Calendars { get; set; } = [];

    /// <summary>[S-566] Colour overrides as <c>#rrggbb</c>, keyed by calendar id, or account id for a default calendar.</summary>
    [JsonConverter(typeof(LenientStringMapConverter))]
    public Dictionary<string, string> CalendarColors { get; set; } = [];

    /// <summary>[S-575] Show contacts' birthdays beside the events.</summary>
    public bool ShowBirthdays { get; set; } = true;

    /// <summary>
    /// [S-564] [S-3770] The week start the layout draws by: the shared regional setting
    /// (Settings › Widgets, else Windows'), never the widget's own.
    /// </summary>
    [JsonIgnore]
    public WeekStartDay EffectiveWeekStart => Localization.RegionFormat.Current.WeekStart;

    /// <summary>
    /// [S-568] Everything beside the kind and the anchor that decides the days the layout covers:
    /// the week start, and the week's and the agenda's own lengths where the kind has them.
    /// </summary>
    [JsonIgnore]
    public virtual CalendarPeriod Period => new(EffectiveWeekStart);

    /// <summary>[S-563] Bring every value back inside its range.</summary>
    public sealed override IReadOnlyList<string> Repair()
    {
        var repairs = new List<string>();

        var ids = (AccountIds ?? []).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList();
        if (AccountIds is null || ids.Count != AccountIds.Count)
        {
            repairs.Add("calendar widget had blank or repeated account ids");
        }

        AccountIds = ids;
        Calendars ??= [];
        CalendarColors ??= [];
        RepairOwn(repairs);
        return repairs;
    }

    /// <summary>The kind's own fields, after the shared ones.</summary>
    protected virtual void RepairOwn(List<string> repairs)
    {
    }

    /// <summary>Copy the shared fields onto <paramref name="target"/>.</summary>
    protected T CopySharedTo<T>(T target) where T : CalendarConfig
    {
        target.AccountIds = [.. AccountIds];
        target.Calendars = Calendars.ToDictionary(kv => kv.Key, kv => kv.Value.ToList(), StringComparer.Ordinal);
        target.CalendarColors = new Dictionary<string, string>(CalendarColors, StringComparer.Ordinal);
        target.ShowBirthdays = ShowBirthdays;
        return target;
    }
}

/// <summary>
/// [S-3601] A calendar that draws weeks (month, week, year). Its week start is the shared regional
/// setting ([S-3770]), so it adds no field of its own.
/// </summary>
public abstract class CalendarGridConfig : CalendarConfig
{
}

/// <summary>[S-3600] The month calendar.</summary>
public sealed class MonthCalendarConfig : CalendarGridConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.MonthCalendar;

    [JsonIgnore]
    public override CalendarLayout Layout => CalendarLayout.Month;

    public override WidgetConfig Clone() => CopySharedTo(new MonthCalendarConfig());
}

/// <summary>
/// [S-3600] The week calendar. [S-3637] Its own length and first column: a whole week (7) from the
/// regional week start by default, a working week (5), or the next few days from today.
/// </summary>
public sealed class WeekCalendarConfig : CalendarGridConfig
{
    public const int DefaultDays = 7;

    public const int MinDays = 1;

    public const int MaxDays = 14;

    /// <summary>[S-3637] A working week, which settings name beside the whole one.</summary>
    public const int WorkWeekDays = 5;

    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.WeekCalendar;

    [JsonIgnore]
    public override CalendarLayout Layout => CalendarLayout.Week;

    /// <summary>[S-3637] How many day columns, 1 to 14.</summary>
    public int Days { get; set; } = DefaultDays;

    /// <summary>[S-3637] Where the first column is: the week's first day, or today.</summary>
    public WeekFirstDay FirstDay { get; set; } = WeekFirstDay.Week;

    [JsonIgnore]
    public int EffectiveDays => Math.Clamp(Days, MinDays, MaxDays);

    [JsonIgnore]
    public override CalendarPeriod Period => new(EffectiveWeekStart, EffectiveDays, FirstDay);

    protected override void RepairOwn(List<string> repairs)
    {
        if (Days != EffectiveDays)
        {
            repairs.Add($"week calendar days {Days} was out of range");
            Days = EffectiveDays;
        }

        if (!Enum.IsDefined(FirstDay))
        {
            repairs.Add("week calendar first day was not a known one");
            FirstDay = WeekFirstDay.Week;
        }
    }

    public override WidgetConfig Clone()
    {
        var copy = CopySharedTo(new WeekCalendarConfig());
        copy.Days = Days;
        copy.FirstDay = FirstDay;
        return copy;
    }
}

/// <summary>[S-3600] The year calendar.</summary>
public sealed class YearCalendarConfig : CalendarGridConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.YearCalendar;

    [JsonIgnore]
    public override CalendarLayout Layout => CalendarLayout.Year;

    public override WidgetConfig Clone() => CopySharedTo(new YearCalendarConfig());
}

/// <summary>[S-3600] The day calendar: one day, no week, so no week start.</summary>
public sealed class DayCalendarConfig : CalendarConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.DayCalendar;

    [JsonIgnore]
    public override CalendarLayout Layout => CalendarLayout.Day;

    public override WidgetConfig Clone() => CopySharedTo(new DayCalendarConfig());
}

/// <summary>[S-3600] The agenda: a list, so a length and a date range instead of a week start.</summary>
public sealed class AgendaConfig : CalendarConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.Agenda;

    [JsonIgnore]
    public override CalendarLayout Layout => CalendarLayout.Agenda;

    public const int DefaultCount = 20;

    /// <summary>[S-3636] The agenda looks this many days ahead, today included, unless set otherwise.</summary>
    public const int DefaultDays = 30;

    public const int MinDays = 1;

    /// <summary>[S-3636] A year: as far as the shared source keeps events ([S-588]).</summary>
    public const int MaxDays = 365;

    /// <summary>[S-3636] The ranges settings offer by name: today, today and tomorrow, 3, 7 and 30 days; any other is custom.</summary>
    public static IReadOnlyList<int> DayPresets { get; } = [1, 2, 3, 7, 30];

    /// <summary>[S-571] How many entries the agenda lists, 1 to 100.</summary>
    public int AgendaCount { get; set; } = DefaultCount;

    /// <summary>[S-3636] How many days the agenda covers, today first, 1 to 365.</summary>
    public int AgendaDays { get; set; } = DefaultDays;

    [JsonIgnore]
    public int EffectiveAgendaCount => Math.Clamp(AgendaCount, 1, 100);

    [JsonIgnore]
    public int EffectiveAgendaDays => Math.Clamp(AgendaDays, MinDays, MaxDays);

    [JsonIgnore]
    public override CalendarPeriod Period => new(EffectiveWeekStart, AgendaDays: EffectiveAgendaDays);

    protected override void RepairOwn(List<string> repairs)
    {
        if (AgendaCount != EffectiveAgendaCount)
        {
            repairs.Add($"agenda count {AgendaCount} was out of range");
            AgendaCount = EffectiveAgendaCount;
        }

        if (AgendaDays != EffectiveAgendaDays)
        {
            repairs.Add($"agenda days {AgendaDays} was out of range");
            AgendaDays = EffectiveAgendaDays;
        }
    }

    public override WidgetConfig Clone()
    {
        var copy = CopySharedTo(new AgendaConfig());
        copy.AgendaCount = AgendaCount;
        copy.AgendaDays = AgendaDays;
        return copy;
    }
}

/// <summary>
/// [S-580] The contacts widget's settings: which accounts, the layout, the columns of the details
/// table, how many to list and a search to start with. Data comes from Themia's own accounts.
/// </summary>
public sealed class ContactsConfig : WidgetConfig
{
    /// <summary>The columns of a fresh details table, in canonical order.</summary>
    public static IReadOnlyList<ContactColumn> AllColumns { get; } =
        [ContactColumn.Name, ContactColumn.Email, ContactColumn.Phone, ContactColumn.Company, ContactColumn.Title];

    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.Contacts;

    /// <summary>The accounts to show; empty means every contacts account.</summary>
    [JsonConverter(typeof(LenientIdListConverter))]
    public List<string> AccountIds { get; set; } = [];

    public ContactsLayout Layout { get; set; } = ContactsLayout.List;

    /// <summary>[S-582] The details table's columns; kept in canonical order with at least one.</summary>
    [JsonConverter(typeof(LenientEnumListConverter<ContactColumn>))]
    public List<ContactColumn> Columns { get; set; } = [.. AllColumns];

    /// <summary>[S-583] How many people to list: at least five, in steps of five, no upper bound.</summary>
    public int Count { get; set; } = 20;

    [JsonIgnore]
    public int EffectiveCount => Math.Max(5, Count);

    [JsonIgnore]
    public ContactsLayout EffectiveLayout => Enum.IsDefined(Layout) ? Layout : ContactsLayout.List;

    /// <summary>[S-582] The columns as shown: canonical order, unknown values dropped, never none.</summary>
    [JsonIgnore]
    public IReadOnlyList<ContactColumn> EffectiveColumns
    {
        get
        {
            var chosen = AllColumns.Where(c => Columns?.Contains(c) == true).ToList();
            return chosen.Count > 0 ? chosen : [ContactColumn.Name, ContactColumn.Email, ContactColumn.Phone];
        }
    }

    /// <summary>[S-563] Bring every value back inside its range.</summary>
    public override IReadOnlyList<string> Repair()
    {
        var repairs = new List<string>();

        var ids = (AccountIds ?? []).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList();
        if (AccountIds is null || ids.Count != AccountIds.Count)
        {
            repairs.Add("contacts widget had blank or repeated account ids");
        }

        AccountIds = ids;
        Columns ??= [.. AllColumns];

        if (Count != EffectiveCount)
        {
            repairs.Add($"contacts widget count {Count} was below five");
            Count = EffectiveCount;
        }

        if (!Enum.IsDefined(Layout))
        {
            repairs.Add("contacts widget had an unknown layout");
            Layout = ContactsLayout.List;
        }

        return repairs;
    }

    public override WidgetConfig Clone() => new ContactsConfig
    {
        AccountIds = [.. AccountIds],
        Layout = Layout,
        Columns = [.. Columns],
        Count = Count,
    };
}
