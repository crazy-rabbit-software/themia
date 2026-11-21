using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Themia.App.Widgets;
using Themia.Core.Links;

namespace Themia.Sdk.WinUI.Kit;

/// <summary>
/// [S-6756] [S-11910] [S-11911] The one path a chart's moments take on its view's links, beside the Chart block: the
/// pointer over the chart's surface is a moment (the kind's own rule, <c>at</c>: the bar, the day or the slot under it),
/// published as <see cref="LinkTopics.HoveredInstant"/> with the kind's subject while the pointer is there and taken back
/// when it leaves; a moment another widget of the view points at is followed (<see cref="Followed"/>, and handed to the
/// kind as it arrives: a reading standing at it, [S-11912]). The moment the chart shows (<see cref="Shown"/>: its own while
/// the pointer is on it, else the one followed) goes to the kind's mark (a hairline, a band's crosshair) on every change.
/// UI thread, as the board is.
/// </summary>
public sealed class ChartMoments
{
    private readonly WidgetLinks _links;
    private readonly LinkTopic<InstantLink> _topic;
    private readonly Func<PointerRoutedEventArgs, DateTimeOffset?> _at;
    private readonly Func<string> _subject;
    private readonly Action<DateTimeOffset?> _mark;
    private readonly Func<bool> _active;

    /// <param name="links">The widget's place on its view's links (joined and left by its view).</param>
    /// <param name="topic">The moment's topic the kind names (<see cref="LinkTopics.HoveredInstant"/>).</param>
    /// <param name="surface">What takes the pointer: the chart's own box (hit-testable).</param>
    /// <param name="at">The moment under the pointer, or null where there is none (no samples, nothing laid out yet).</param>
    /// <param name="subject">What the moment is of (<see cref="LinkInstants.Subject(string, string)"/>), asked as it is published.</param>
    /// <param name="mark">Draws the moment shown, or none.</param>
    /// <param name="followed">Hears another widget's moment (or its letting go) after it is marked; null: the mark is all.</param>
    /// <param name="active">Whether the chart takes the pointer now (a meter draws a history only in its Graphs look); null: always.</param>
    public ChartMoments(
        WidgetLinks links,
        LinkTopic<InstantLink> topic,
        UIElement surface,
        Func<PointerRoutedEventArgs, DateTimeOffset?> at,
        Func<string> subject,
        Action<DateTimeOffset?> mark,
        Action<InstantLink?>? followed = null,
        Func<bool>? active = null)
    {
        ArgumentNullException.ThrowIfNull(links);
        ArgumentNullException.ThrowIfNull(surface);
        _links = links;
        _topic = topic ?? throw new ArgumentNullException(nameof(topic));
        _at = at ?? throw new ArgumentNullException(nameof(at));
        _subject = subject ?? throw new ArgumentNullException(nameof(subject));
        _mark = mark ?? throw new ArgumentNullException(nameof(mark));
        _active = active ?? (() => true);

        links.Follow(topic, moment =>
        {
            Followed = moment;
            Mark();
            followed?.Invoke(moment);
        });
        surface.PointerMoved += OnPointerMoved;
        surface.PointerExited += OnPointerGone;
        surface.PointerCanceled += OnPointerGone;
        surface.PointerCaptureLost += OnPointerGone;
    }

    /// <summary>The moment under the pointer on this chart, while it is there.</summary>
    public DateTimeOffset? Own { get; private set; }

    /// <summary>The moment another widget of the view points at, as last heard.</summary>
    public InstantLink? Followed { get; private set; }

    /// <summary>The moment the chart shows: its own while the pointer is on it, else the one followed.</summary>
    public DateTimeOffset? Shown => Own ?? Followed?.At;

    /// <summary>Draw the moment shown again (the chart was laid out anew).</summary>
    public void Mark() => _mark(Shown);

    private void OnPointerMoved(object sender, PointerRoutedEventArgs args)
    {
        if (!_active() || _at(args) is not { } at)
        {
            return;
        }

        Own = at;
        _links.Set(_topic, new InstantLink(at, _subject()));
        Mark();
    }

    private void OnPointerGone(object sender, PointerRoutedEventArgs args)
    {
        if (Own is null)
        {
            return;
        }

        Own = null;
        _links.Set(_topic, null);
        Mark();
    }
}
