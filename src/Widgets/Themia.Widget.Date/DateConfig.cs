using System.Text.Json.Serialization;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Widget.Date;

namespace Themia.Core.Model;

/// <summary>
/// [S-10801] The date widget's styles: today in four looks. The first is the default; an unknown stored name is
/// repaired to it ([S-6004]).
/// </summary>
[JsonConverter(typeof(StyleJsonConverter<DateWidgetStyle>))]
public enum DateWidgetStyle
{
    /// <summary>"Monday, 17 November": the weekday and the day, long.</summary>
    Long = 0,

    /// <summary>"Mon, Nov 17": the short names.</summary>
    Short = 1,

    /// <summary>"17.11.2026", "11/17/2026": the language's numeric date.</summary>
    Numeric = 2,

    /// <summary>A calendar page: the weekday over the day of the month, large, over the month.</summary>
    Page = 3,
}

/// <summary>
/// [S-10800] What one date widget remembers: its style. How big it is is the card's (no Size, [S-2449] retired).
/// Every property is stored; copied in <see cref="Clone"/>.
/// </summary>
public sealed class DateConfig : WidgetConfig, IStyled, IContentAligned
{
    private static readonly StyleOption[] DateStyles =
    [
        StyleOption.Of(DateWidgetStyle.Long, DateStr.Styles_Date_Long.Local(), 0xE8BF),
        StyleOption.Of(DateWidgetStyle.Short, DateStr.Styles_Date_Short.Local(), 0xE787),
        StyleOption.Of(DateWidgetStyle.Numeric, DateStr.Styles_Date_Numeric.Local(), 0xE8EF),
        StyleOption.Of(DateWidgetStyle.Page, DateStr.Styles_Date_Page.Local(), 0xE8D1),
    ];

    /// <summary>[S-10801] The look.</summary>
    public DateWidgetStyle Style { get; set; } = DateWidgetStyle.Long;

    IReadOnlyList<StyleOption> IStyled.Styles => DateStyles;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (DateWidgetStyle)value;
    }

    /// <summary>[S-6581] Its one block aligns; in the middle when nothing was chosen.</summary>
    ContentAlign IContentAligned.DefaultAlign => ContentAlign.Center;

    public override WidgetConfig Clone() => new DateConfig { Style = Style };
}
