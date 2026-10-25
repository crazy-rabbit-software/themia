using System.Text.Json.Serialization;
using Themia.Core.Office;
using Themia.Core.Sync;
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

/// <summary>[S-582] The details table's columns.</summary>
public static class ContactColumns
{
    /// <summary>The columns of a fresh details table, in canonical order.</summary>
    public static IReadOnlyList<ContactColumn> All { get; } =
        [ContactColumn.Name, ContactColumn.Email, ContactColumn.Phone, ContactColumn.Company, ContactColumn.Title];
}

/// <summary>
/// [S-561] [S-3601] What every calendar widget shares: which accounts and calendars, their colours,
/// and whether birthdays are shown. Data comes from Themia's own accounts ([S-529]). Every value is
/// read leniently ([S-563]): a wrong one becomes its default rather than refusing the state file.
/// The calendar itself (<see cref="CalendarWidgetConfig"/>), Next meeting and the week planner build on it.
/// </summary>
public abstract class CalendarConfig : WidgetConfig, ISetupRule, IInterests
{
    /// <summary>[S-2608] What the background sync keeps fresh for it.</summary>
    public virtual void AddInterests(InterestsBuilder interests) => interests.Of<CalendarInterest>().Add(new CalendarInterest([.. AccountIds], Calendars, ShowBirthdays));

    /// <summary>[S-6906] It waits for setup while this holds.</summary>
    public virtual bool NeedsSetup(FeatureSet signedIn) => !signedIn.Has(HostFeatures.Calendar);

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
    /// (⚙ › Language &amp; region, else Windows'), never the widget's own.
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
