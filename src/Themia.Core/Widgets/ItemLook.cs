using Themia.Core.Model;

namespace Themia.Core.Widgets;

/// <summary>[S-6513] What an item a widget draws is, for its fill: one of four shapes.</summary>
public enum ItemRole
{
    /// <summary>A list row, a grid cell, a crumb, a row-like button: clear at rest, lit under the pointer.</summary>
    Row,

    /// <summary>A control-like tile: a toggle's or a power button's face, a mixer's or a player's button.</summary>
    Tile,

    /// <summary>A chip standing on the card: a calendar event, a day cell, a flip digit.</summary>
    Chip,

    /// <summary>The empty part of a bar, a ring or a heat cell: what a value is drawn over.</summary>
    Track,
}

/// <summary>[S-6513] The state an item is drawn in. Each role answers every state.</summary>
public enum ItemState
{
    Rest,
    Hover,
    Pressed,
    Selected,

    /// <summary>Selected and under the pointer.</summary>
    SelectedHover,
    Disabled,
}

/// <summary>
/// [S-6513] What one item fill is drawn in: a colour of the user's own (<see cref="Colour"/>), or a
/// Fluent theme brush by key; either way at <see cref="Opacity"/>, which a theme brush multiplies
/// with its own.
/// </summary>
public readonly record struct ItemInk(RgbColor? Colour, string? ThemeKey, double Opacity);

/// <summary>
/// [S-6510]..[S-6514] The item layer's rules: which layer a widget's items take, and what each item
/// fill is in every state. The App half (<c>Widgets/ItemPalette</c>) only turns these answers into
/// brushes, one set per card.
/// </summary>
public static class ItemLook
{
    /// <summary>How far a state moves a colour of the user's own toward the theme's contrast (white in dark, black in light).</summary>
    public const double HoverShift = 0.08;

    public const double PressedShift = 0.04;

    public const double SelectedShift = 0.14;

    public const double SelectedHoverShift = 0.20;

    /// <summary>A track stands off the item it is drawn on, or a bar inside a filled row would vanish.</summary>
    public const double TrackShift = 0.20;

    /// <summary>A disabled item is the rest fill at half its opacity.</summary>
    public const double DisabledFade = 0.5;

    /// <summary>
    /// [S-6512] The layer a widget's items are drawn with: the widget's own when it has one, whole
    /// (its colour and its opacity together), else the app-wide one. Null widget: the app-wide one.
    /// </summary>
    public static ItemLayer Effective(DesignState design, WidgetInstance? widget)
    {
        ArgumentNullException.ThrowIfNull(design);
        return widget?.ItemBackground ?? design.ItemBackground ?? new ItemLayer();
    }

    /// <summary>
    /// [S-6514] The Fluent fill each role takes in each state — the fills the widgets used before the
    /// layer existed, so the default layer looks exactly as they did.
    /// </summary>
    public static string ThemeKey(ItemRole role, ItemState state) => role switch
    {
        ItemRole.Row => state switch
        {
            ItemState.Hover or ItemState.Selected => "SubtleFillColorSecondaryBrush",
            ItemState.Pressed or ItemState.SelectedHover => "SubtleFillColorTertiaryBrush",
            ItemState.Disabled => "SubtleFillColorDisabledBrush",
            _ => "SubtleFillColorTransparentBrush",
        },
        ItemRole.Tile => state switch
        {
            ItemState.Hover or ItemState.Selected => "ControlFillColorSecondaryBrush",
            ItemState.Pressed or ItemState.SelectedHover => "ControlFillColorTertiaryBrush",
            ItemState.Disabled => "ControlFillColorDisabledBrush",
            _ => "ControlFillColorDefaultBrush",
        },
        ItemRole.Chip => state switch
        {
            ItemState.Hover or ItemState.Pressed or ItemState.Selected or ItemState.SelectedHover => "SubtleFillColorTertiaryBrush",
            ItemState.Disabled => "SubtleFillColorDisabledBrush",
            _ => "SubtleFillColorSecondaryBrush",
        },
        _ => state == ItemState.Disabled ? "ControlStrongFillColorDisabledBrush" : "ControlStrongFillColorDefaultBrush",
    };

    /// <summary>
    /// [S-6513] The fill of <paramref name="role"/> in <paramref name="state"/> for a layer, in the
    /// effective theme. Without a colour of its own the item keeps its Fluent fill
    /// (<see cref="ThemeKey"/>), faded by the layer's opacity. With one, every role rests on that
    /// colour at the layer's opacity, and a state moves it toward the theme's contrast by a fixed
    /// step (<see cref="HoverShift"/> and its siblings) — so hover, press and selection are derived
    /// from the item colour, never from the card's. A track stands <see cref="TrackShift"/> off it.
    /// Disabled is the rest fill at <see cref="DisabledFade"/> of the opacity.
    /// </summary>
    public static ItemInk Ink(ItemRole role, ItemState state, ItemLayer layer, bool dark)
    {
        ArgumentNullException.ThrowIfNull(layer);

        var opacity = Opacity(layer.Opacity) * (state == ItemState.Disabled ? DisabledFade : 1);

        if (!HexColor.TryParse(layer.Color, out var r, out var g, out var b))
        {
            return new ItemInk(null, ThemeKey(role, state), Opacity(layer.Opacity));
        }

        var shift = role == ItemRole.Track ? TrackShift : state switch
        {
            ItemState.Hover => HoverShift,
            ItemState.Pressed => PressedShift,
            ItemState.Selected => SelectedShift,
            ItemState.SelectedHover => SelectedHoverShift,
            _ => 0,
        };

        var colour = new RgbColor(r, g, b);
        var contrast = dark ? new RgbColor(255, 255, 255) : new RgbColor(0, 0, 0);
        var shown = shift > 0 ? DesignLook.Over(DesignLook.Alpha(shift), contrast, colour) : colour;
        return new ItemInk(shown, null, opacity);
    }

    /// <summary>[S-6511] A layer opacity: not finite is 1, the rest clamped to 0..1.</summary>
    public static double Opacity(double value) => double.IsFinite(value) ? Math.Clamp(value, 0, 1) : 1;

    /// <summary>
    /// [S-6541] The colour the text on a widget's items is drawn in: the widget's own when it has one,
    /// else the app-wide one, else empty (the theme's text colours). Apart from the item background:
    /// a widget with a background of its own and no text colour of its own takes the app-wide text
    /// colour. Null widget: the app-wide one. Always a normalised <c>#RRGGBB</c> or empty.
    /// </summary>
    public static string TextColour(DesignState design, WidgetInstance? widget)
    {
        ArgumentNullException.ThrowIfNull(design);
        var own = HexColor.Normalise(widget?.ItemTextColor);
        return own.Length > 0 ? own : HexColor.Normalise(design.ItemTextColor);
    }

    /// <summary>
    /// [S-6543] Bring a loaded item text colour inside its invariant: <c>#RRGGBB</c> (normalised) or
    /// empty. Something that is not a colour is empty (the theme's, or the app-wide one for a
    /// widget), with what changed for the load log; null when it was fine as it was.
    /// </summary>
    public static (string Colour, string? Repair) RepairTextColour(string? stored)
    {
        var colour = HexColor.Normalise(stored);
        var repair = !string.IsNullOrWhiteSpace(stored) && colour.Length == 0
            ? $"item text colour '{stored}' is not a colour; using the default"
            : null;
        return (colour, repair);
    }

    /// <summary>[S-6542] The Fluent text colour each tier is without an item text colour.</summary>
    public static string TextThemeKey(ItemTextTier tier) => tier switch
    {
        ItemTextTier.Secondary => "TextFillColorSecondaryBrush",
        ItemTextTier.Tertiary => "TextFillColorTertiaryBrush",
        ItemTextTier.Disabled => "TextFillColorDisabledBrush",
        _ => "TextFillColorPrimaryBrush",
    };

    /// <summary>
    /// [S-6542] How opaque a tier is next to the primary text, in the theme: Fluent's own text alphas
    /// (WinUI <c>TextFillColor*</c>: dark #FF, #C5, #87, #5D white; light #E4, #9E, #72, #5C black)
    /// divided by its primary's, so a colour of the user's keeps the hierarchy Fluent draws.
    /// </summary>
    public static double TextOpacity(ItemTextTier tier, bool dark)
    {
        var (primary, alpha) = dark
            ? (0xFF, tier switch { ItemTextTier.Secondary => 0xC5, ItemTextTier.Tertiary => 0x87, ItemTextTier.Disabled => 0x5D, _ => 0xFF })
            : (0xE4, tier switch { ItemTextTier.Secondary => 0x9E, ItemTextTier.Tertiary => 0x72, ItemTextTier.Disabled => 0x5C, _ => 0xE4 });
        return alpha / (double)primary;
    }

    /// <summary>
    /// [S-6542] What text of <paramref name="tier"/> on an item is drawn in: without a colour
    /// (<paramref name="textColour"/> empty or not a colour) the theme's text colour for the tier,
    /// as it is; with one, that colour, the primary at full strength and the others at
    /// <see cref="TextOpacity"/>. Never faded by the item layer's opacity: that is the fill's.
    /// </summary>
    public static ItemInk TextInk(ItemTextTier tier, string? textColour, bool dark)
        => HexColor.TryParse(textColour, out var r, out var g, out var b)
            ? new ItemInk(new RgbColor(r, g, b), null, TextOpacity(tier, dark))
            : new ItemInk(null, TextThemeKey(tier), 1);
}

/// <summary>
/// [S-6542] The text colours Fluent draws in, from the most to the least prominent. Text on an item
/// keeps its tier when an item text colour replaces the theme's ([S-6540]).
/// </summary>
public enum ItemTextTier
{
    Primary,
    Secondary,
    Tertiary,
    Disabled,
}
