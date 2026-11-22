using Themia.Core.Series;

namespace Themia.Core.Model;

/// <summary>[S-15504] Which way a repeater's rows run.</summary>
public enum RepeatDirection
{
    /// <summary>Rows under each other (the default).</summary>
    Down = 0,

    /// <summary>Rows side by side.</summary>
    Across = 1,

    /// <summary>Across, then down: right to left in a right-to-left language.</summary>
    Wrap = 2,
}

/// <summary>
/// [S-15500]..[S-15532] A repeater's config contract, read and rewritten by the host's rules (Core's
/// <c>Repeaters</c>) without knowing the module, as a fence's <see cref="IHoldsDesktopItems"/>: the
/// widgets of its view it holds (its template row), which way and how far apart its rows stand, what
/// it repeats by and where the values come from, its scroll, and the slots taken out of the series by hand.
/// </summary>
public interface IRepeater
{
    /// <summary>[S-15500] The widgets of its view it holds, by id, in order: the template row.</summary>
    List<string> Members { get; set; }

    /// <summary>[S-15504] Down, Across or Wrap.</summary>
    RepeatDirection Direction { get; set; }

    /// <summary>[S-15503] Grid cells between rows (Wrap: between lines), 0 or more.</summary>
    int Spacing { get; set; }

    /// <summary>[S-15504] Wrap only: grid cells between columns, 0 or more.</summary>
    int ColumnSpacing { get; set; }

    /// <summary>[S-15511] What it repeats by; null until one is picked.</summary>
    SeriesType? RepeatBy { get; set; }

    /// <summary>[S-15511] The id of the source its values come from (<see cref="SeriesSources"/>); null for the type's default.</summary>
    string? Values { get; set; }

    /// <summary>[S-15513] A range's first value (a day range: the day from today).</summary>
    int Start { get; set; }

    /// <summary>[S-15513] A range's step, 1 or more.</summary>
    int Step { get; set; }

    /// <summary>[S-15513] A picked source's values as tokens, in order.</summary>
    List<string> Picked { get; set; }

    /// <summary>[S-15515] How many rows (Wrap: lines) are scrolled past.</summary>
    int Scroll { get; set; }

    /// <summary>[S-15512] Slots taken out of the series by hand: <c>widgetId/slotKey</c>.</summary>
    List<string> Unbound { get; set; }

    /// <summary>[S-15512] Of a widget with two slots of the type, the one chosen to take it: <c>widgetId/slotKey</c>.</summary>
    List<string> Chosen { get; set; }

    /// <summary>
    /// [S-15503] The day box's width in grid cells: what one slot is, repeated (owner, 2026-11-21: "One day's box, repeated");
    /// 0 for its parts' extent. Never less than its parts reach: a part moved or grown past it grows it.
    /// </summary>
    int DayWidth { get; set; }

    /// <summary>[S-15503] The day box's height in grid cells, as <see cref="DayWidth"/>.</summary>
    int DayHeight { get; set; }

    /// <summary>
    /// [S-13918] How many values it runs over at most (owner, 2026-11-21: "you should be able to set how many items the weather
    /// forecast should show. it should then get scrollable if it overflows"); 0 for all its source has.
    /// </summary>
    int Count => 0;

    /// <summary>
    /// [S-13915] The parts a new one starts with, in order, side by side from its corner (owner, 2026-11-21: "create a good
    /// looking weather forecast widget based on this system that can be edited afterwards"); none by default.
    /// </summary>
    IReadOnlyList<RepeaterPart> FirstParts => [];

    /// <summary>
    /// [S-15528] Whether it takes a widget of <paramref name="member"/>'s config into its day (owner, 2026-11-21: "there is
    /// basically a selection of widgets that make sense in that specific context"): by default one with a slot of what it
    /// repeats by.
    /// </summary>
    bool Accepts(WidgetConfig member) => RepeatBy is { } type && SeriesSlots.Of(member, type).Count > 0;

    /// <summary>[S-15511] The types its members' slots have, put there by the host before its menu is built: never stored.</summary>
    IReadOnlyList<SeriesType> Offered { get; set; }

    /// <summary>[S-15501] [S-15520] What its own view draws, put there by the host on the copy it hands the view: never stored.</summary>
    RepeaterShown? Shown { get; set; }
}

/// <summary>[S-13915] A part a container starts with: its kind and its size in whole grid cells.</summary>
public sealed record RepeaterPart(string Kind, int Width, int Height, ContentAlign? Align = null);

/// <summary>A box inside a repeater's card, as fractions of the card.</summary>
public readonly record struct RepeaterBox(double X, double Y, double W, double H);

/// <summary>
/// [S-15501] [S-15520] What a repeater's own view draws: in edit mode the outline of each slot it shows, and while it has
/// no value its source's sentence in the slot after its template.
/// </summary>
public sealed record RepeaterShown(IReadOnlyList<RepeaterBox> Slots, RepeaterBox? StatusSlot, string? Status, bool Editing);

/// <summary>[S-15534] How a repeater's copied rows look.</summary>
public static class RepeaterLook
{
    /// <summary>
    /// [S-15534] A copied row's opacity in edit mode (owner, 2026-11-21: "the repeaters should show the repeated items muted in
    /// edit mode or darker"): the template row, the one that is edited, stands out; outside edit mode the copies are whole.
    /// </summary>
    public const double CopyEditing = 0.4;
}
