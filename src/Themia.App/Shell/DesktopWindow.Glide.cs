using System.Diagnostics;
using Microsoft.UI.Xaml.Media;
using Themia.App.Shell.Motion;
using Themia.Core.Geometry;

namespace Themia.App.Shell;

/// <summary>
/// [S-13028] A card gliding from the box it had to the one its widget has now (a fence rolled up or let down, the owner,
/// 2026-11-19: "rolling up a fence should be nicely animated"): the stored rect changes at once, and for
/// <see cref="CardGlide.Ms"/> the card is drawn on the way there, frame by frame, its foot rising or falling as the shell's
/// one glide eases (<see cref="CardGlide.At"/>); a layout pass meanwhile draws it where the glide stands, never jumps.
/// Nothing glides while Windows' animations are off.
/// </summary>
internal sealed partial class DesktopWindow
{
    /// <summary>[S-13028] The cards gliding now, by widget id: where each came from and when it began.</summary>
    private readonly Dictionary<string, (FracRect From, long Began)> _glides = new(StringComparer.Ordinal);

    /// <summary>
    /// [S-13028] Glide the card of <paramref name="widgetId"/> from <paramref name="from"/> (the rect it was drawn in) to the
    /// one its widget holds now. Nothing while animations are off, or for a widget this surface does not draw.
    /// </summary>
    internal void GlideCard(string widgetId, FracRect from)
    {
        if (_closed || !SystemMotion.AnimationsEnabled || CardFor(widgetId) is not { } card)
        {
            return;
        }

        var start = _glides.TryGetValue(widgetId, out var running) ? GlideAt(running, card.Model.Rect) : from;
        if (_glides.Count == 0)
        {
            CompositionTarget.Rendering += OnGlideFrame;
        }

        _glides[widgetId] = (start, Stopwatch.GetTimestamp());
        PositionCard(card, card.Model.Rect);
    }

    /// <summary>[S-13028] Where a glide toward <paramref name="to"/> stands now.</summary>
    private static FracRect GlideAt((FracRect From, long Began) glide, FracRect to)
        => CardGlide.At(glide.From, to, Stopwatch.GetElapsedTime(glide.Began).TotalMilliseconds);

    /// <summary>[S-13028] The rect <paramref name="card"/> is drawn in: its glide's place while one runs, else <paramref name="rect"/>.</summary>
    private FracRect Gliding(WidgetCard card, FracRect rect)
        => _glides.TryGetValue(card.Model.Id, out var glide) ? GlideAt(glide, rect) : rect;

    /// <summary>[S-13028] One frame: every gliding card a step on; the ones done land where they belong and their joins come back.</summary>
    private void OnGlideFrame(object? sender, object e)
    {
        if (_closed)
        {
            StopGlides();
            return;
        }

        var landed = false;
        foreach (var (id, glide) in _glides.ToList())
        {
            var done = CardGlide.Done(Stopwatch.GetElapsedTime(glide.Began).TotalMilliseconds);
            if (done)
            {
                _glides.Remove(id);
                landed = true;
            }

            if (CardFor(id) is { } card)
            {
                PositionCard(card, card.Model.Rect);
            }
        }

        if (landed)
        {
            JoinCards();
        }

        if (_glides.Count == 0)
        {
            CompositionTarget.Rendering -= OnGlideFrame;
        }
    }

    /// <summary>[S-13028] Every glide ends where it was going (the surface closing).</summary>
    private void StopGlides()
    {
        if (_glides.Count > 0)
        {
            _glides.Clear();
            CompositionTarget.Rendering -= OnGlideFrame;
        }
    }
}
