namespace Themia.Core.Widgets;

/// <summary>What a key asks of a widget that is one control rather than a list: a timer, a slideshow, a quote, a stream.</summary>
public enum ControlKey
{
    /// <summary>The key means nothing here: it goes on to the surface.</summary>
    None,

    /// <summary>Start or pause (the timer, the slideshow, the stream).</summary>
    Toggle,

    /// <summary>One step back: the previous slide, the previous quote.</summary>
    Previous,

    /// <summary>One step on: the next slide, quote, or the pomodoro's next phase.</summary>
    Next,

    /// <summary>The first item (the slideshow's first picture).</summary>
    First,

    /// <summary>The last item.</summary>
    Last,

    /// <summary>Open what is shown (the slideshow's picture, in its app).</summary>
    Open,

    /// <summary>Back to the start (the pomodoro's reset).</summary>
    Reset,
}

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

    /// <summary>
    /// [S-1990] Quote: Right, Space or Enter the next quote (what a tap does), Left the one before.
    /// One quote has nowhere to go, so nothing answers.
    /// </summary>
    public static ControlKey Quote(WidgetKey key, int quotes) => quotes <= 1 ? ControlKey.None : key switch
    {
        WidgetKey.Right or WidgetKey.Space or WidgetKey.Enter => ControlKey.Next,
        WidgetKey.Left => ControlKey.Previous,
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
