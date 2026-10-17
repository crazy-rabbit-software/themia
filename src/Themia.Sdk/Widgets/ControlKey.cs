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

    /// <summary>[S-4604] End a lap (the stopwatch).</summary>
    Lap,
}
