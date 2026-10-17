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
