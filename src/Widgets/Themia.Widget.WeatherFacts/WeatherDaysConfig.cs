using System.Text.Json.Serialization;
using Themia.Core.Model;
using Themia.Core.Series;
using Themia.Sdk;

namespace Themia.Widget.WeatherFacts;

/// <summary>
/// [S-13910] What the weather forecast remembers (owner, 2026-11-21): its one place (the weather family's, [S-2840]: no part
/// sets one, "settings like location should not be settable per widget"), the widgets of its day (its parts: the day facts
/// and the design elements, laid on the desktop's grid in its day box), the day box's size, how its days are laid (rows,
/// columns, a grid) and how far apart, and its first day. It repeats days, always: a day from today, one apart.
/// </summary>
public sealed class WeatherDaysConfig : WeatherConfig, IRepeater
{
    /// <summary>[S-13910] Its parts, by widget id, in order.</summary>
    public List<string> Members { get; set; } = [];

    /// <summary>[S-13911] Rows (down), Columns (across) or Grid (across, then down).</summary>
    public RepeatDirection Direction { get; set; }

    /// <summary>[S-13911] Grid cells between days (Grid: between lines).</summary>
    public int Spacing { get; set; }

    /// <summary>[S-13911] Grid only: grid cells between columns.</summary>
    public int ColumnSpacing { get; set; }

    /// <summary>It repeats days, always.</summary>
    [JsonIgnore]
    public SeriesType? RepeatBy
    {
        get => SeriesType.Day;
        set { }
    }

    /// <summary>The day range: the one source of days.</summary>
    [JsonIgnore]
    public string? Values
    {
        get => null;
        set { }
    }

    /// <summary>[S-13911] Its first day: 0 today, 1 tomorrow.</summary>
    public int Start { get; set; }

    /// <summary>A day a day.</summary>
    [JsonIgnore]
    public int Step
    {
        get => 1;
        set { }
    }

    /// <summary>Days are a range: nothing is picked.</summary>
    [JsonIgnore]
    public List<string> Picked { get; set; } = [];

    /// <summary>[S-15515] How many days its card scrolled past.</summary>
    public int Scroll { get; set; }

    /// <summary>[S-15512] A part's day slot taken out of the days by hand: <c>widgetId/slotKey</c>.</summary>
    public List<string> Unbound { get; set; } = [];

    [JsonIgnore]
    public List<string> Chosen { get; set; } = [];

    /// <summary>[S-13918] How many days it shows, from its first (owner: "you should be able to set how many items the weather
    /// forecast should show. it should then get scrollable if it overflows"): a week by default.</summary>
    public int Days { get; set; } = DefaultDays;

    /// <summary>[S-13918] A new one's days.</summary>
    public const int DefaultDays = 7;

    [JsonIgnore]
    public int Count => Days;

    /// <summary>
    /// [S-13915] What a new one shows each day (owner: "create a good looking weather forecast widget based on this system"):
    /// the day's name, its sky, its low, its range on the week's scale and its high, side by side, one row of four cells.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<RepeaterPart> FirstParts { get; } =
    [
        new("weatherDay", 9, 4, ContentAlign.Left),
        new("weatherSky", 4, 4),
        new("weatherLow", 7, 4, ContentAlign.Right),
        new("weatherRange", 10, 4),
        new("weatherHigh", 7, 4, ContentAlign.Left),
    ];

    /// <summary>[S-13912] The day box's size in grid cells; 0 its parts' extent.</summary>
    public int DayWidth { get; set; }

    /// <summary>[S-13912] The day box's height in grid cells; 0 its parts' extent.</summary>
    public int DayHeight { get; set; }

    [JsonIgnore]
    public IReadOnlyList<SeriesType> Offered { get; set; } = [];

    [JsonIgnore]
    public RepeaterShown? Shown { get; set; }

    /// <summary>
    /// [S-13910] [S-15528] What its day takes (owner, 2026-11-21: "there is basically a selection of widgets that make sense in
    /// that specific context"): a fact a day ahead means something for (one with a day slot) and the design elements.
    /// </summary>
    public bool Accepts(WidgetConfig member)
        => member is IDesignElement || (member is not IRepeater && SeriesSlots.Of(member, SeriesType.Day).Count > 0);

    public override IReadOnlyList<string> Repair()
    {
        var notes = new List<string>(base.Repair());
        Members ??= [];
        Unbound ??= [];
        if (!Enum.IsDefined(Direction))
        {
            Direction = RepeatDirection.Down;
            notes.Add($"{nameof(WeatherDaysConfig)}: unknown layout; rows");
        }

        if (Days < 1 || Days > DayRangeSource.Reach)
        {
            notes.Add($"{nameof(WeatherDaysConfig)}: {Days} days is not a count it shows; {DefaultDays}");
            Days = DefaultDays;
        }

        if (Start < 0 || Start >= DayRangeSource.Reach)
        {
            notes.Add($"{nameof(WeatherDaysConfig)}: first day {Start} is not one it shows; today");
            Start = 0;
        }

        (Spacing, ColumnSpacing, Scroll, DayWidth, DayHeight) = (Math.Max(0, Spacing), Math.Max(0, ColumnSpacing), Math.Max(0, Scroll), Math.Max(0, DayWidth), Math.Max(0, DayHeight));
        return notes;
    }

    public override WidgetConfig Clone()
    {
        var copy = new WeatherDaysConfig
        {
            Members = [.. Members],
            Direction = Direction,
            Spacing = Spacing,
            ColumnSpacing = ColumnSpacing,
            Start = Start,
            Days = Days,
            Scroll = Scroll,
            Unbound = [.. Unbound],
            DayWidth = DayWidth,
            DayHeight = DayHeight,
            Offered = Offered,
            Shown = Shown,
        };
        CopyTo(copy);
        return copy;
    }
}
