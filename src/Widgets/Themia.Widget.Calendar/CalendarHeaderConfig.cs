using System.Text.Json.Serialization;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Widget.Calendar;

/// <summary>
/// [S-14902] The period a calendar header pages by: its style ([S-6000]), the first the default. A week is the whole week
/// from the regional first day of the week ([S-3770]).
/// </summary>
[JsonConverter(typeof(StyleJsonConverter<CalendarHeaderStyle>))]
public enum CalendarHeaderStyle
{
    Month = 0,
    Week = 1,
    Day = 2,
    Year = 3,
}

/// <summary>
/// [S-14900] What a calendar header remembers: the period it pages by, its style. It reads no account and never waits
/// for one: the period is the view's ([S-3626]), the events the calendars'.
/// </summary>
public sealed class CalendarHeaderConfig : WidgetConfig, IStyled
{
    private static readonly StyleOption[] HeaderStyles =
    [
        StyleOption.Of(CalendarHeaderStyle.Month, CalendarStr.Styles_Calendar_Month.Local(), 0xE787),
        StyleOption.Of(CalendarHeaderStyle.Week, CalendarStr.Styles_Calendar_Week.Local(), 0xE8C0),
        StyleOption.Of(CalendarHeaderStyle.Day, CalendarStr.Styles_Calendar_Day.Local(), 0xE8BF),
        StyleOption.Of(CalendarHeaderStyle.Year, CalendarStr.Styles_Calendar_Year.Local(), 0xEA89),
    ];

    /// <summary>[S-14902] The period it pages by.</summary>
    public CalendarHeaderStyle Style { get; set; } = CalendarHeaderStyle.Month;

    IReadOnlyList<StyleOption> IStyled.Styles => HeaderStyles;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (CalendarHeaderStyle)value;
    }

    /// <summary>[S-14902] Its period as the calendar's rules name it: a month, a week, a day or a year.</summary>
    [JsonIgnore]
    public CalendarStyle Unit => Style switch
    {
        CalendarHeaderStyle.Week => CalendarStyle.Week,
        CalendarHeaderStyle.Day => CalendarStyle.Day,
        CalendarHeaderStyle.Year => CalendarStyle.Year,
        _ => CalendarStyle.Month,
    };

    /// <summary>[S-14902] A week from the regional first day of the week, seven days; a month, a day or a year the week start alone.</summary>
    [JsonIgnore]
    public CalendarPeriod Period => new(RegionFormat.Current.WeekStart);

    public override WidgetConfig Clone() => new CalendarHeaderConfig { Style = Style };
}
