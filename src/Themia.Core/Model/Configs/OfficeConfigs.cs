using System.Text.Json.Serialization;
using Themia.Core.Office;

namespace Themia.Core.Model;

/// <summary>
/// [S-2700] The five ways the calendar widget lays out time. The widget draws only the configured
/// one, at every size: each layout sheds detail within itself, never turning into another.
/// </summary>
[JsonConverter(typeof(LenientEnumConverter<CalendarLayout>))]
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
/// [S-561] The calendar widget's settings: which accounts and calendars, how time is laid out, and
/// whether birthdays are shown. Data comes from Themia's own accounts ([S-529]). Every value is
/// read leniently ([S-563]): a wrong one becomes its default rather than refusing the state file.
/// </summary>
public sealed class CalendarConfig : WidgetConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.Calendar;

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

    /// <summary>[S-2700] The layout the widget draws, whatever its size.</summary>
    public CalendarLayout Layout { get; set; } = CalendarLayout.Month;

    public WeekStartDay WeekStart { get; set; } = WeekStartDay.Monday;

    /// <summary>[S-575] Show contacts' birthdays beside the events.</summary>
    public bool ShowBirthdays { get; set; } = true;

    /// <summary>How many entries the agenda lists, 1 to 100.</summary>
    public int AgendaCount { get; set; } = 20;

    [JsonIgnore]
    public int EffectiveAgendaCount => Math.Clamp(AgendaCount, 1, 100);

    /// <summary>The layout, narrowed to the closed set.</summary>
    [JsonIgnore]
    public CalendarLayout EffectiveLayout => Enum.IsDefined(Layout) ? Layout : CalendarLayout.Month;

    [JsonIgnore]
    public WeekStartDay EffectiveWeekStart => Enum.IsDefined(WeekStart) ? WeekStart : WeekStartDay.Monday;

    /// <summary>[S-563] Bring every value back inside its range.</summary>
    public override IReadOnlyList<string> Repair()
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

        if (AgendaCount != EffectiveAgendaCount)
        {
            repairs.Add($"calendar widget agenda count {AgendaCount} was out of range");
            AgendaCount = EffectiveAgendaCount;
        }

        if (!Enum.IsDefined(Layout) || !Enum.IsDefined(WeekStart))
        {
            repairs.Add("calendar widget had an unknown layout or week start");
            Layout = EffectiveLayout;
            WeekStart = EffectiveWeekStart;
        }

        return repairs;
    }

    public override WidgetConfig Clone() => new CalendarConfig
    {
        AccountIds = [.. AccountIds],
        Calendars = Calendars.ToDictionary(kv => kv.Key, kv => kv.Value.ToList(), StringComparer.Ordinal),
        CalendarColors = new Dictionary<string, string>(CalendarColors, StringComparer.Ordinal),
        Layout = Layout,
        WeekStart = WeekStart,
        ShowBirthdays = ShowBirthdays,
        AgendaCount = AgendaCount,
    };
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

    /// <summary>[S-584] A search the widget starts with.</summary>
    public string DefaultSearch { get; set; } = string.Empty;

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
        DefaultSearch ??= string.Empty;

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
        DefaultSearch = DefaultSearch,
    };
}
