using System.Text.Json.Serialization;
using Themia.Core.Widgets;

namespace Themia.Core.Model;

/// <summary>[S-1484] Where the clock's column sits across the tile.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ClockAlign>))]
public enum ClockAlign
{
    Center = 0,
    Left = 1,
    Right = 2,
}

/// <summary>[S-1485] The offset line under the date: none, from UTC, or from the machine's own zone.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ClockOffsetMode>))]
public enum ClockOffsetMode
{
    None = 0,
    Utc = 1,
    Local = 2,
}

public sealed class ClockConfig : WidgetConfig
{
    /// <summary>[S-1482] A label longer than this is a sentence, not a place.</summary>
    public const int MaxLabelLength = 40;

    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.Clock;

    public ClockFace Face { get; set; } = ClockFace.Digital;

    public bool ShowSeconds { get; set; }

    public bool ShowDate { get; set; } = true;

    /// <summary>
    /// [S-1480] IANA id of the zone to show ("Asia/Tokyo"), or null for the machine's own zone. Resolved
    /// through <see cref="ZoneCatalog"/> (IANA ids do not resolve under <c>InvariantGlobalization</c>).
    /// </summary>
    public string? TimeZoneId { get; set; }

    /// <summary>[S-1482] The label over the time. Empty: the zone's city, or nothing for the local zone.</summary>
    public string? CustomLabel { get; set; }

    public ClockAlign Align { get; set; } = ClockAlign.Center;

    public ClockOffsetMode OffsetMode { get; set; } = ClockOffsetMode.None;

    public override WidgetConfig Clone() => new ClockConfig
    {
        Face = Face,
        ShowSeconds = ShowSeconds,
        ShowDate = ShowDate,
        TimeZoneId = TimeZoneId,
        CustomLabel = CustomLabel,
        Align = Align,
        OffsetMode = OffsetMode,
    };

    /// <summary>
    /// [S-53] The zone to draw. An unset, unknown or (on this Windows) missing zone is the local
    /// one; it never throws, because this runs on every tick.
    /// </summary>
    public TimeZoneInfo ResolveTimeZone() => ZoneCatalog.Resolve(TimeZoneId) ?? TimeZoneInfo.Local;

    /// <summary>True when a zone is chosen and resolves: what makes the clock a world clock.</summary>
    [JsonIgnore]
    public bool IsWorldClock => ZoneCatalog.Resolve(TimeZoneId) is not null;

    /// <summary>The tick cadence this config actually needs. A clock without seconds wakes once a minute.</summary>
    [JsonIgnore]
    public TimeSpan TickInterval => ShowSeconds || Face == ClockFace.Analog
        ? TimeSpan.FromSeconds(1)
        : TimeSpan.FromMinutes(1);

    /// <summary>
    /// [S-1486] Back inside the invariants after a hand edit: an enum value outside its set falls back
    /// to the default, an id the catalog does not know means "local", and an over-long label is cut.
    /// </summary>
    public override IReadOnlyList<string> Repair()
    {
        var repairs = new List<string>();

        if (!Enum.IsDefined(Face))
        {
            repairs.Add($"clock face {(int)Face} unknown; digital");
            Face = ClockFace.Digital;
        }

        if (!Enum.IsDefined(Align))
        {
            repairs.Add($"clock alignment {(int)Align} unknown; centred");
            Align = ClockAlign.Center;
        }

        if (!Enum.IsDefined(OffsetMode))
        {
            repairs.Add($"clock offset mode {(int)OffsetMode} unknown; none");
            OffsetMode = ClockOffsetMode.None;
        }

        if (!string.IsNullOrWhiteSpace(TimeZoneId) && ZoneCatalog.Find(TimeZoneId) is null)
        {
            repairs.Add($"clock time zone '{TimeZoneId}' unknown; local");
            TimeZoneId = null;
        }

        if (CustomLabel is { Length: > MaxLabelLength })
        {
            repairs.Add($"clock label longer than {MaxLabelLength}; cut");
            CustomLabel = CustomLabel[..MaxLabelLength];
        }

        return repairs;
    }
}
