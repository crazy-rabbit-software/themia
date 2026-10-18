using System.Text.Json.Serialization;

namespace Themia.Core.Model;

/// <summary>Where a widget draws its header.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<HeaderPlacement>))]
public enum HeaderPlacement
{
    Top = 0,
    Bottom = 1,

    /// <summary>No header. The widget is the content and nothing else.</summary>
    Hidden = 2,

    /// <summary>[S-404] A strip down the card's left edge, the title turned to read bottom to top.</summary>
    Left = 3,

    /// <summary>[S-404] A strip down the card's right edge, the title turned to read top to bottom.</summary>
    Right = 4,
}

/// <summary>Light/dark selection. <c>System</c> follows the Windows app theme.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AppTheme>))]
public enum AppTheme
{
    System = 0,
    Light = 1,
    Dark = 2,
}

/// <summary>How a widget card's border is drawn.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<WidgetBorderStyle>))]
public enum WidgetBorderStyle
{
    None = 0,
    Solid = 1,
    Dashed = 2,
    Dotted = 3,
}

/// <summary>
/// The one-shot animation widgets play when a view appears. A closed set, because each value picks
/// a different piece of code rather than a parameter.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<IntroAnimation>))]
public enum IntroAnimation
{
    None = 0,
    Fade = 1,
    Scale = 2,
    Slide = 3,

    /// <summary>Widgets fly in from the nearer side edge.</summary>
    Sides = 4,

    /// <summary>Widgets fly in from the nearer top or bottom edge.</summary>
    TopBottom = 5,

    Flip = 6,

    Blur = 7,

    /// <summary>A star-field overlay the widgets drop out of.</summary>
    Hyperspace = 8,

    /// <summary>Each widget types itself in, terminal style.</summary>
    Hacker = 9,
}
