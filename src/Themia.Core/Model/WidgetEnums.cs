using System.Text.Json.Serialization;

namespace Themia.Core.Model;

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
/// [S-2123] What a widget card's surface is made of: a solid fill over the frost (every style but one), or
/// Liquid glass, Apple's (iOS 26, macOS 26): a clear tint that rises as far as the text needs over what lies
/// behind, a bright rim lit from the top left, a specular sheen, an inner lens edge and a soft lift
/// (<see cref="Widgets.LiquidGlass"/>).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<CardMaterial>))]
public enum CardMaterial
{
    Solid = 0,
    LiquidGlass = 1,
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
