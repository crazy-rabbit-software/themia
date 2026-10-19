using System.Text.Json.Serialization;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Widget.Clock;

namespace Themia.Core.Model;

/// <summary>
/// [S-6011] The clock's styles ([S-1483]): the same time, seven looks. The first is the
/// default; an unknown stored name is repaired to it ([S-6004]).
/// </summary>
[JsonConverter(typeof(StyleJsonConverter<ClockStyle>))]
public enum ClockStyle
{
    Digital = 0,
    Analog = 1,

    /// <summary>[S-1483] Hours over minutes (over seconds), the original's third style.</summary>
    Stacked = 2,

    /// <summary>[S-6012] Split-flap cards, the hours and the minutes, that flip when they change.</summary>
    Flip = 3,

    /// <summary>[S-6013] The time in words, in five-minute steps ("It's quarter past ten").</summary>
    Word = 4,

    /// <summary>[S-6014] The time alone, in a thin weight: no date, label, seconds or offset.</summary>
    Minimal = 5,

    /// <summary>[S-5300] The day without the time: the weekday, the day of the month large, the month.</summary>
    Date = 6,
}

/// <summary>
/// [S-1476] How large the clock sets the place label and the date (and the offset line) against
/// the time: a step of the type ramp down or up, never a free size.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ClockTextSize>))]
public enum ClockTextSize
{
    Default = 0,
    Small = 1,
    Large = 2,
}

/// <summary>[S-1485] The offset line under the date: none, from UTC, or from the machine's own zone.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ClockOffsetMode>))]
public enum ClockOffsetMode
{
    None = 0,
    Utc = 1,
    Local = 2,
}

/// <summary>[S-6074] The clock's rows that only some styles draw.</summary>
public enum ClockSetting
{
    /// <summary>Seconds: the faces that tick them ([S-6015]).</summary>
    Seconds,

    /// <summary>The date line: not the minimal clock, nor the date style, which is the date.</summary>
    Date,

    /// <summary>The size of the label, date and offset lines.</summary>
    TextSize,

    /// <summary>The offset line, under the zone.</summary>
    Offset,
}

public sealed class ClockConfig : WidgetConfig, IStyled, IShownSettings<ClockSetting>, IContentAligned
{
    /// <summary>[S-6581] Its one block aligns; where it sits when nothing was chosen.</summary>
    ContentAlign IContentAligned.DefaultAlign => ContentAlign.Center;

    /// <summary>[S-6000] The clock's look; what was its face.</summary>
    public ClockStyle Style { get; set; } = ClockStyle.Digital;

    private static readonly StyleOption[] ClockStyles =
    [
        StyleOption.Of(ClockStyle.Digital, ClockStr.Clock_StyleDigital.Local(), 0xEC92),
        StyleOption.Of(ClockStyle.Analog, ClockStr.Clock_StyleAnalog.Local(), 0xE917),
        StyleOption.Of(ClockStyle.Stacked, ClockStr.Clock_StyleStacked.Local(), 0xEA37),
        StyleOption.Of(ClockStyle.Flip, ClockStr.Styles_Clock_Flip.Local(), 0xE8A9),
        StyleOption.Of(ClockStyle.Word, ClockStr.Styles_Clock_Words.Local(), 0xE8D2),
        StyleOption.Of(ClockStyle.Minimal, ClockStr.Styles_Clock_Minimal.Local(), 0xE738),
        StyleOption.Of(ClockStyle.Date, ClockStr.Styles_Clock_Date.Local(), 0xE8D1),
    ];

    IReadOnlyList<StyleOption> IStyled.Styles => ClockStyles;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (ClockStyle)value;
    }

    public bool ShowSeconds { get; set; }

    public bool ShowDate { get; set; } = true;

    /// <summary>
    /// [S-1480] IANA id of the zone to show ("Asia/Tokyo"), or null for the machine's own zone. Resolved
    /// through <see cref="ZoneCatalog"/> (IANA ids do not resolve under <c>InvariantGlobalization</c>).
    /// </summary>
    public string? TimeZoneId { get; set; }

    public ClockOffsetMode OffsetMode { get; set; } = ClockOffsetMode.None;

    /// <summary>[S-1476] The label and date size (user request: both were hard to read).</summary>
    public ClockTextSize TextSize { get; set; } = ClockTextSize.Default;

    public override WidgetConfig Clone() => new ClockConfig
    {
        Style = Style,
        ShowSeconds = ShowSeconds,
        ShowDate = ShowDate,
        TimeZoneId = TimeZoneId,
        OffsetMode = OffsetMode,
        TextSize = TextSize,
    };

    /// <summary>
    /// [S-53] The zone to draw. An unset, unknown or (on this Windows) missing zone is the local
    /// one; it never throws, because this runs on every tick.
    /// </summary>
    public TimeZoneInfo ResolveTimeZone() => ZoneCatalog.Resolve(TimeZoneId) ?? TimeZoneInfo.Local;

    /// <summary>True when a zone is chosen and resolves: what makes the clock a world clock.</summary>
    [JsonIgnore]
    public bool IsWorldClock => ZoneCatalog.Resolve(TimeZoneId) is not null;

    /// <summary>
    /// [S-6074] The rows the menu offers: seconds where the face ticks them; the date and offset
    /// lines wherever there are lines under the time, so neither on the minimal clock ([S-6014]) nor
    /// on the date style ([S-5303]). [S-1476] The text size sizes the label, the date and the offset
    /// line, so it is offered only while one of them is drawn: a city's label, the date switched on,
    /// or an offset chosen. The zone applies to every style; the alignment is the properties panel's ([S-6580]).
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<ClockSetting> ShownSettings
    {
        get
        {
            if (Style is ClockStyle.Date or ClockStyle.Minimal)
            {
                return [];
            }

            var shown = new List<ClockSetting>(4);
            if (Style != ClockStyle.Word)
            {
                shown.Add(ClockSetting.Seconds);
            }

            shown.Add(ClockSetting.Date);
            if (ShowDate || OffsetMode != ClockOffsetMode.None || IsWorldClock)
            {
                shown.Add(ClockSetting.TextSize);
            }

            shown.Add(ClockSetting.Offset);
            return shown;
        }
    }

    /// <summary>
    /// [S-6015] Whether this style draws seconds at all: words come in five-minute steps, the
    /// minimal clock is the time alone and the date style has no time ([S-6013], [S-6014], [S-5300]).
    /// </summary>
    [JsonIgnore]
    public bool DrawsSeconds => ShowSeconds && Style is not (ClockStyle.Word or ClockStyle.Minimal or ClockStyle.Date);

    /// <summary>
    /// The tick cadence this config actually needs. A clock without seconds wakes once a minute (the
    /// date style too: it turns over within a minute of midnight, [S-5304]); an analog face sweeps
    /// its hands every second.
    /// </summary>
    [JsonIgnore]
    public TimeSpan TickInterval => DrawsSeconds || Style == ClockStyle.Analog
        ? TimeSpan.FromSeconds(1)
        : TimeSpan.FromMinutes(1);

    /// <summary>
    /// [S-1486] Back inside the invariants after a hand edit: an enum value outside its set falls back
    /// to the default (the style is the registry's, [S-6004]) and an id the catalog does not know means "local".
    /// </summary>
    public override IReadOnlyList<string> Repair()
    {
        var repairs = new List<string>();

        if (!Enum.IsDefined(OffsetMode))
        {
            repairs.Add($"clock offset mode {(int)OffsetMode} unknown; none");
            OffsetMode = ClockOffsetMode.None;
        }

        if (!Enum.IsDefined(TextSize))
        {
            repairs.Add($"clock text size {(int)TextSize} unknown; default");
            TextSize = ClockTextSize.Default;
        }

        if (!string.IsNullOrWhiteSpace(TimeZoneId) && ZoneCatalog.Find(TimeZoneId) is null)
        {
            repairs.Add($"clock time zone '{TimeZoneId}' unknown; local");
            TimeZoneId = null;
        }

        return repairs;
    }
}
