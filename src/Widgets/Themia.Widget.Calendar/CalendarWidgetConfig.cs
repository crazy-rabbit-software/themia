using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Accounts;
using Themia.Core.Localization;
using Themia.Core.Office;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Widget.Calendar;

namespace Themia.Core.Model;

/// <summary>
/// [S-3640] The calendar: one widget, five styles (month, week, day, year, agenda), what were five
/// kinds. It stores what every calendar shares and the union of what its styles use: the week's
/// length and first column ([S-3637]) and the agenda's range and length ([S-3636], [S-571]). A
/// style ignores the fields it does not use, and the properties menu shows only its own
/// (<see cref="SettingsFor"/>).
/// </summary>
public sealed class CalendarWidgetConfig : CalendarConfig, IStyled, IShownSettings<CalendarSetting>
{
    private static readonly StyleOption[] CalendarStyles =
    [
        StyleOption.Of(CalendarStyle.Month, CalendarStr.Styles_Calendar_Month.Local(), 0xE787),
        StyleOption.Of(CalendarStyle.Week, CalendarStr.Styles_Calendar_Week.Local(), 0xE8C0),
        StyleOption.Of(CalendarStyle.Day, CalendarStr.Styles_Calendar_Day.Local(), 0xE8BF),
        StyleOption.Of(CalendarStyle.Year, CalendarStr.Styles_Calendar_Year.Local(), 0xEA89),
        StyleOption.Of(CalendarStyle.Agenda, CalendarStr.Styles_Calendar_Agenda.Local(), 0xE8FD),
    ];

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
    /// [S-3601] [S-6070] The lines the properties menu shows for the current style after the
    /// accounts, in menu order: only what that style uses (birthdays follow for every style).
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
