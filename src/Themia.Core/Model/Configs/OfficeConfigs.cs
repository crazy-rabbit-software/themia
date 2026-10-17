using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Office;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>
/// [S-3640] The five ways the calendar lays out time: its style ([S-6000]). The first is the
/// default; an unknown stored name is repaired to it ([S-6004]). Each sheds detail within itself at
/// every size, never turning into another ([S-2700]). The values are not the menu order
/// (<see cref="CalendarWidgetConfig"/> lists Month, Week, Day, Year, Agenda).
/// </summary>
[JsonConverter(typeof(StyleJsonConverter<CalendarStyle>))]
public enum CalendarStyle
{
    /// <summary>The weeks of a month, the original's default.</summary>
    Month = 0,

    /// <summary>Day columns, seven by default ([S-3637]).</summary>
    Week = 1,

    /// <summary>One day on an hour timeline.</summary>
    Day = 2,

    /// <summary>A list of what is coming up, grouped by day.</summary>
    Agenda = 3,

    /// <summary>The twelve months of a year as small grids.</summary>
    Year = 4,
}

/// <summary>[S-3601] A style's own row on the calendar's settings page, beside the accounts and birthdays every style shows.</summary>
public enum CalendarSetting
{
    /// <summary>[S-3637] Week: where the first column is.</summary>
    WeekFrom,

    /// <summary>[S-3637] Week: how many day columns.</summary>
    WeekDays,

    /// <summary>[S-3636] Agenda: how many days ahead.</summary>
    AgendaRange,

    /// <summary>[S-571] Agenda: how many entries.</summary>
    AgendaCount,
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
/// The calendar itself (<see cref="CalendarWidgetConfig"/>), Next meeting and the week planner build on it.
/// </summary>
public abstract class CalendarConfig : WidgetConfig
{
    /// <summary>[S-3640] The shape the widget draws: the calendar's style, a fixed one for the others. Never stored here.</summary>
    [JsonIgnore]
    public abstract CalendarStyle Layout { get; }

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
    /// (Settings › Language &amp; region, else Windows'), never the widget's own.
    /// </summary>
    [JsonIgnore]
    public WeekStartDay EffectiveWeekStart => Localization.RegionFormat.Current.WeekStart;

    /// <summary>
    /// [S-568] Everything beside the style and the anchor that decides the days the layout covers:
    /// the week start, and the week's and the agenda's own lengths where the style has them.
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
/// [S-3640] The calendar: one widget, five styles (month, week, day, year, agenda), what were five
/// kinds. It stores what every calendar shares and the union of what its styles use: the week's
/// length and first column ([S-3637]) and the agenda's range and length ([S-3636], [S-571]). A
/// style ignores the fields it does not use, and the settings page shows only its own
/// (<see cref="SettingsFor"/>).
/// </summary>
public sealed class CalendarWidgetConfig : CalendarConfig, IStyled, IShownSettings<CalendarSetting>
{
    /// <summary>[S-3637] A whole week of day columns, the week's default.</summary>
    public const int WeekDefaultDays = 7;

    public const int WeekMinDays = 1;

    public const int WeekMaxDays = 14;

    /// <summary>[S-3637] A working week, which settings name beside the whole one.</summary>
    public const int WorkWeekDays = 5;

    public const int AgendaDefaultCount = 20;

    /// <summary>[S-3636] The agenda looks this many days ahead, today included, unless set otherwise.</summary>
    public const int AgendaDefaultDays = 30;

    public const int AgendaMinDays = 1;

    /// <summary>[S-3636] A year: as far as the shared source keeps events ([S-588]).</summary>
    public const int AgendaMaxDays = 365;

    /// <summary>[S-3636] The ranges settings offer by name: today, today and tomorrow, 3, 7 and 30 days; any other is custom.</summary>
    public static IReadOnlyList<int> AgendaDayPresets { get; } = [1, 2, 3, 7, 30];

    private static readonly StyleOption[] CalendarStyles =
    [
        StyleOption.Of(CalendarStyle.Month, Str.Styles_Calendar_Month, 0xE787),
        StyleOption.Of(CalendarStyle.Week, Str.Styles_Calendar_Week, 0xE8C0),
        StyleOption.Of(CalendarStyle.Day, Str.Styles_Calendar_Day, 0xE8BF),
        StyleOption.Of(CalendarStyle.Year, Str.Styles_Calendar_Year, 0xEA89),
        StyleOption.Of(CalendarStyle.Agenda, Str.Styles_Calendar_Agenda, 0xE8FD),
    ];

    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.Calendar;

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("calendar"),
        Kind = WidgetKind.Calendar,
        ConfigType = typeof(CalendarWidgetConfig),
        CreateDefault = () => new CalendarWidgetConfig(),
        Json = WidgetConfigJsonContext.Default,
        // [S-3640] The five former calendar widgets' names and words find the one calendar.
        Keywords =
        [
            "events", "meetings", "appointments", "schedule", "outlook", "google calendar", "birthdays",
            "month calendar", "week calendar", "day calendar", "year calendar", "agenda", "upcoming", "next", "today",
            "timeline", "holidays", "overview",
        ],
        ProviderNames = ["Microsoft", "Google", "CalDAV"],
        LabelId = Str.Styles_Calendar_Label,
        DescriptionId = Str.Styles_Calendar_Desc,
        Glyph = Glyphs.Calendar,
        DefaultWidth = 340,
        DefaultHeight = 260,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
    };

    /// <summary>[S-3640] The layout; what were five widget kinds.</summary>
    public CalendarStyle Style { get; set; } = CalendarStyle.Month;

    IReadOnlyList<StyleOption> IStyled.Styles => CalendarStyles;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (CalendarStyle)value;
    }

    [JsonIgnore]
    public override CalendarStyle Layout => Style;

    /// <summary>[S-3637] Week: how many day columns, 1 to 14.</summary>
    public int WeekDays { get; set; } = WeekDefaultDays;

    /// <summary>[S-3637] Week: where the first column is, the week's first day or today.</summary>
    public WeekFirstDay WeekFrom { get; set; } = WeekFirstDay.Week;

    /// <summary>[S-571] Agenda: how many entries it lists, 1 to 100.</summary>
    public int AgendaCount { get; set; } = AgendaDefaultCount;

    /// <summary>[S-3636] Agenda: how many days it covers, today first, 1 to 365.</summary>
    public int AgendaDays { get; set; } = AgendaDefaultDays;

    [JsonIgnore]
    public int EffectiveWeekDays => Math.Clamp(WeekDays, WeekMinDays, WeekMaxDays);

    [JsonIgnore]
    public int EffectiveAgendaCount => Math.Clamp(AgendaCount, 1, 100);

    [JsonIgnore]
    public int EffectiveAgendaDays => Math.Clamp(AgendaDays, AgendaMinDays, AgendaMaxDays);

    /// <summary>[S-568] The style's own lengths only: a month, a day or a year is the week start alone.</summary>
    [JsonIgnore]
    public override CalendarPeriod Period => Style switch
    {
        CalendarStyle.Week => new(EffectiveWeekStart, EffectiveWeekDays, WeekFrom),
        CalendarStyle.Agenda => new(EffectiveWeekStart, AgendaDays: EffectiveAgendaDays),
        _ => new(EffectiveWeekStart),
    };

    /// <summary>
    /// [S-3601] [S-6070] The rows the settings page shows for the current style after the accounts,
    /// in page order: only what that style uses (birthdays follow for every style). Picking another
    /// style rebuilds the page ([S-6071]).
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<CalendarSetting> ShownSettings => Style switch
    {
        CalendarStyle.Week => [CalendarSetting.WeekFrom, CalendarSetting.WeekDays],
        CalendarStyle.Agenda => [CalendarSetting.AgendaRange, CalendarSetting.AgendaCount],
        _ => [],
    };

    protected override void RepairOwn(List<string> repairs)
    {
        if (WeekDays != EffectiveWeekDays)
        {
            repairs.Add($"calendar week days {WeekDays} was out of range");
            WeekDays = EffectiveWeekDays;
        }

        if (!Enum.IsDefined(WeekFrom))
        {
            repairs.Add("calendar week first day was not a known one");
            WeekFrom = WeekFirstDay.Week;
        }

        if (AgendaCount != EffectiveAgendaCount)
        {
            repairs.Add($"calendar agenda count {AgendaCount} was out of range");
            AgendaCount = EffectiveAgendaCount;
        }

        if (AgendaDays != EffectiveAgendaDays)
        {
            repairs.Add($"calendar agenda days {AgendaDays} was out of range");
            AgendaDays = EffectiveAgendaDays;
        }
    }

    public override WidgetConfig Clone()
    {
        var copy = CopySharedTo(new CalendarWidgetConfig());
        copy.Style = Style;
        copy.WeekDays = WeekDays;
        copy.WeekFrom = WeekFrom;
        copy.AgendaCount = AgendaCount;
        copy.AgendaDays = AgendaDays;
        return copy;
    }
}

/// <summary>[S-6074] The contacts rows that only some layouts draw with.</summary>
public enum ContactsSetting
{
    /// <summary>The details table's columns.</summary>
    Columns,
}

/// <summary>
/// [S-580] The contacts widget's settings: which accounts, the layout, the columns of the details
/// table, how many to list and a search to start with. Data comes from Themia's own accounts.
/// </summary>
public sealed class ContactsConfig : WidgetConfig, IShownSettings<ContactsSetting>
{
    /// <summary>The columns of a fresh details table, in canonical order.</summary>
    public static IReadOnlyList<ContactColumn> AllColumns { get; } =
        [ContactColumn.Name, ContactColumn.Email, ContactColumn.Phone, ContactColumn.Company, ContactColumn.Title];

    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.Contacts;

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("contacts"),
        Kind = WidgetKind.Contacts,
        ConfigType = typeof(ContactsConfig),
        CreateDefault = () => new ContactsConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["address book", "people", "phone numbers", "directory"],
        ProviderNames = ["Microsoft", "Google", "CardDAV"],
        LabelId = Str.Widget_Contacts_Label,
        DescriptionId = Str.WidgetsA_Catalog_Contacts_Description,
        Glyph = Glyphs.Contact,
        DefaultWidth = 320,
        DefaultHeight = 260,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
    };

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

    /// <summary>[S-582] [S-6074] The columns are the details table's: the page offers them for that layout.</summary>
    [JsonIgnore]
    public IReadOnlyList<ContactsSetting> ShownSettings => EffectiveLayout == ContactsLayout.Details ? [ContactsSetting.Columns] : [];

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
