namespace Themia.Core.Widgets;

/// <summary>
/// [S-1987] [S-1990]..[S-1992] The key maps of the widgets that are one control, each a pure function of the
/// key so the map is tested here and the view only carries the answer out. Keys a widget has no use
/// for are <see cref="ControlKey.None"/> and carry on to the surface (Tab still moves focus); Escape is
/// never one of them, it ends the lease ([S-802]).
/// </summary>
public static class ControlKeys
{
    /// <summary>
    /// [S-1987] Pomodoro: Space or Enter starts and pauses (the accent button), Right skips to the next
    /// phase, Home resets the cycle — the three buttons, in the order they sit.
    /// </summary>
    public static ControlKey Pomodoro(WidgetKey key) => key switch
    {
        WidgetKey.Space or WidgetKey.Enter => ControlKey.Toggle,
        WidgetKey.Right => ControlKey.Next,
        WidgetKey.Home => ControlKey.Reset,
        _ => ControlKey.None,
    };

    /// <summary>[S-1991] Video stream: Space pauses and resumes, as the overlay's button does.</summary>
    public static ControlKey Stream(WidgetKey key) => key == WidgetKey.Space ? ControlKey.Toggle : ControlKey.None;

    /// <summary>
    /// [S-811]/[S-1992] Pictures slideshow: Left / Right step, Space pauses (the original's S-29),
    /// Home / End go to the first and last picture, Enter opens the one shown. No pictures, no keys.
    /// </summary>
    public static ControlKey Slideshow(WidgetKey key, int pictures) => pictures <= 0 ? ControlKey.None : key switch
    {
        WidgetKey.Left => ControlKey.Previous,
        WidgetKey.Right => ControlKey.Next,
        WidgetKey.Space => ControlKey.Toggle,
        WidgetKey.Home => ControlKey.First,
        WidgetKey.End => ControlKey.Last,
        WidgetKey.Enter => ControlKey.Open,
        _ => ControlKey.None,
    };
}
