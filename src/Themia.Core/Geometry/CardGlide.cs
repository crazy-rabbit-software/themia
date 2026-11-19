using Themia.Core.Shell;

namespace Themia.Core.Geometry;

/// <summary>
/// [S-13028] A card gliding from one box to another (a fence rolled up or let down, the owner, 2026-11-19: "rolling up a
/// fence should be nicely animated"): every edge moves by the shell's one glide (<see cref="PageReveal.Glide"/>: a cubic
/// ease-out over <see cref="Ms"/>), so the card's foot rises or falls smoothly and its top-left stays. At once while
/// animations are off (the caller does not glide). Pure.
/// </summary>
public static class CardGlide
{
    /// <summary>[S-13028] How long a glide takes, ms: the shell's one glide.</summary>
    public const double Ms = PageReveal.Ms;

    /// <summary>[S-13028] Where the card stands <paramref name="elapsedMs"/> into the glide from <paramref name="from"/> to <paramref name="to"/>.</summary>
    public static FracRect At(FracRect from, FracRect to, double elapsedMs)
        => Done(elapsedMs) || !double.IsFinite(elapsedMs)
            ? to
            : new FracRect(
                PageReveal.Glide(from.X, to.X, elapsedMs),
                PageReveal.Glide(from.Y, to.Y, elapsedMs),
                PageReveal.Glide(from.W, to.W, elapsedMs),
                PageReveal.Glide(from.H, to.H, elapsedMs));

    /// <summary>[S-13028] Whether the glide is over <paramref name="elapsedMs"/> in.</summary>
    public static bool Done(double elapsedMs) => PageReveal.Done(elapsedMs);
}
