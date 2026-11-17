namespace Themia.Core.Shell;

/// <summary>
/// [S-2347] How a settings page scrolls to show what was just opened (a card, a picker, a thing, a deep link's row),
/// Karpo's §671 (karpo-app, <c>EditDeck.Reveal</c>/<c>Revealing</c>): only as far as it takes to bring the opened item
/// whole into sight, a gap off the viewport's ends; where it is taller than the viewport, its head at the top; nothing at
/// all where it is in sight already. The page glides there over <see cref="Ms"/>, easing out (a cubic), as WinUI's
/// BringIntoView does; at once while animations are off. Pure.
/// </summary>
public static class PageReveal
{
    /// <summary>[S-2347] How long the glide takes, ms (Karpo's <c>RevealMs</c>).</summary>
    public const double Ms = 250;

    /// <summary>[S-2347] A frame of the glide, ms (Karpo's 16 ms timer).</summary>
    public const double FrameMs = 16;

    /// <summary>
    /// [S-2347] Where the page is scrolled to show the item at <paramref name="itemTop"/> of <paramref name="itemHeight"/>
    /// (page DIPs, the opened content with its head) whole, with the viewport at <paramref name="viewportTop"/> of
    /// <paramref name="viewportHeight"/> over a page <paramref name="contentHeight"/> tall, <paramref name="gap"/> kept off
    /// the viewport's ends: unchanged where it is in sight; else as little as it takes (its foot up to the viewport's foot,
    /// its head down to the viewport's top), its head at the top where it does not fit; within the page's scroll.
    /// </summary>
    public static double ScrollIntoView(double viewportTop, double viewportHeight, double itemTop, double itemHeight, double contentHeight, double gap = 0)
    {
        var most = Math.Max(0, contentHeight - viewportHeight);
        var bottom = itemTop + itemHeight;
        var to = bottom + gap > viewportTop + viewportHeight ? Math.Min(itemTop - gap, bottom + gap - viewportHeight)
            : itemTop - gap < viewportTop ? itemTop - gap
            : viewportTop;
        return Math.Clamp(to, 0, most);
    }

    /// <summary>[S-2347] Whether a scroll to <paramref name="to"/> from <paramref name="from"/> moves at all (less than a DIP does not).</summary>
    public static bool Moves(double from, double to) => Math.Abs(to - from) >= 1;

    /// <summary>
    /// [S-2347] Where the glide from <paramref name="from"/> to <paramref name="to"/> stands <paramref name="elapsedMs"/>
    /// in: a cubic ease-out over <see cref="Ms"/>, at <paramref name="to"/> from then on.
    /// </summary>
    public static double Glide(double from, double to, double elapsedMs)
    {
        var t = Math.Clamp(elapsedMs / Ms, 0, 1);
        var eased = 1 - Math.Pow(1 - t, 3);
        return from + ((to - from) * eased);
    }

    /// <summary>[S-2347] Whether the glide is done <paramref name="elapsedMs"/> in.</summary>
    public static bool Done(double elapsedMs) => elapsedMs >= Ms;
}
