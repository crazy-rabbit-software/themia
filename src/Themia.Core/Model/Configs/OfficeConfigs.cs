using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Accounts;
using Themia.Core.Localization;
using Themia.Core.Office;
using Themia.Core.Storage;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>
/// [S-3640] The calendar: one widget, five styles (month, week, day, year, agenda), what were five
/// kinds. It stores what every calendar shares and the union of what its styles use: the week's
/// length and first column ([S-3637]) and the agenda's range and length ([S-3636], [S-571]). A
/// style ignores the fields it does not use, and the settings page shows only its own
/// (<see cref="SettingsFor"/>).
/// </summary>
public sealed class CalendarWidgetConfig : CalendarConfig, IStyled, IShownSettings<CalendarSetting>
{
    private static readonly StyleOption[] CalendarStyles =
    [
        StyleOption.Of(CalendarStyle.Month, Str.Styles_Calendar_Month.Local(), 0xE787),
        StyleOption.Of(CalendarStyle.Week, Str.Styles_Calendar_Week.Local(), 0xE8C0),
        StyleOption.Of(CalendarStyle.Day, Str.Styles_Calendar_Day.Local(), 0xE8BF),
        StyleOption.Of(CalendarStyle.Year, Str.Styles_Calendar_Year.Local(), 0xEA89),
        StyleOption.Of(CalendarStyle.Agenda, Str.Styles_Calendar_Agenda.Local(), 0xE8FD),
    ];


    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("calendar"),
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
        Providers = [HostProviders.Microsoft, HostProviders.Google, HostProviders.CalDav],
        Label = Str.Styles_Calendar_Label.Local(),
        Description = Str.Styles_Calendar_Desc.Local(),
        Glyph = Glyphs.Calendar,
        DefaultWidth = 340,
        DefaultHeight = 260,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.CalendarMail, 0, 0, Popular: 3),
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
    public int WeekDays { get; set; } = CalendarPeriod.WeekDefaultDays;

    /// <summary>[S-3637] Week: where the first column is, the week's first day or today.</summary>
    public WeekFirstDay WeekFrom { get; set; } = WeekFirstDay.Week;

    /// <summary>[S-571] Agenda: how many entries it lists, 1 to 100.</summary>
    public int AgendaCount { get; set; } = CalendarPeriod.AgendaDefaultCount;

    /// <summary>[S-3636] Agenda: how many days it covers, today first, 1 to 365.</summary>
    public int AgendaDays { get; set; } = CalendarPeriod.AgendaDefaultDays;

    [JsonIgnore]
    public int EffectiveWeekDays => Math.Clamp(WeekDays, CalendarPeriod.WeekMinDays, CalendarPeriod.WeekMaxDays);

    [JsonIgnore]
    public int EffectiveAgendaCount => Math.Clamp(AgendaCount, 1, 100);

    [JsonIgnore]
    public int EffectiveAgendaDays => Math.Clamp(AgendaDays, CalendarPeriod.AgendaMinDays, CalendarPeriod.AgendaMaxDays);

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
public sealed class ContactsConfig : WidgetConfig, IShownSettings<ContactsSetting>, ISetupRule, IInterests
{
    /// <summary>[S-2608] What the background sync keeps fresh for it.</summary>
    public void AddInterests(InterestsBuilder interests) => interests.Of<ContactsInterest>().Add(new ContactsInterest([.. AccountIds]));

    /// <summary>[S-6906] It waits for setup while this holds.</summary>
    public bool NeedsSetup(FeatureSet signedIn) => !signedIn.Has(HostFeatures.Contacts);



    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("contacts"),
        ConfigType = typeof(ContactsConfig),
        CreateDefault = () => new ContactsConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["address book", "people", "phone numbers", "directory"],
        Providers = [HostProviders.Microsoft, HostProviders.Google, HostProviders.CardDav],
        Label = Str.Widget_Contacts_Label.Local(),
        Description = Str.WidgetsA_Catalog_Contacts_Description.Local(),
        Glyph = Glyphs.Contact,
        DefaultWidth = 320,
        DefaultHeight = 260,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.CalendarMail, 1, 2),
    };

    /// <summary>The accounts to show; empty means every contacts account.</summary>
    [JsonConverter(typeof(LenientIdListConverter))]
    public List<string> AccountIds { get; set; } = [];

    public ContactsLayout Layout { get; set; } = ContactsLayout.List;

    /// <summary>[S-582] The details table's columns; kept in canonical order with at least one.</summary>
    [JsonConverter(typeof(LenientEnumListConverter<ContactColumn>))]
    public List<ContactColumn> Columns { get; set; } = [.. ContactColumns.All];

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
            var chosen = ContactColumns.All.Where(c => Columns?.Contains(c) == true).ToList();
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
        Columns ??= [.. ContactColumns.All];

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
