using Themia.Core.Model;

namespace Themia.Core.Motion;

/// <summary>
/// The easing of one keyframe segment. A closed set: each maps to one compositor easing function.
/// </summary>
public enum MotionEase
{
    Linear,

    /// <summary>The original's intro curve, <c>cubic-bezier(0.22, 1, 0.36, 1)</c>: fast out, long settle.</summary>
    Settle,

    /// <summary>[S-1006] The warp-in's steep ease-in, <c>cubic-bezier(0.55, 0, 1, 0.45)</c>: still accelerating on impact.</summary>
    Crash,
}

/// <summary>What a rotation track turns about.</summary>
public enum IntroAxis
{
    /// <summary>No rotation track.</summary>
    None,

    /// <summary>In the screen plane, about the card's centre (the warp-in's judder).</summary>
    Roll,

    /// <summary>About the card's top edge, towards the viewer (Flip).</summary>
    Tilt,
}

/// <summary>
/// One keyframe of a scalar track. <paramref name="At"/> is the fraction of the widget's intro (0..1);
/// <paramref name="Ease"/> is the easing of the segment <b>arriving</b> at this key — the compositor's
/// convention — and is ignored on the first key.
/// </summary>
public readonly record struct MotionKey(double At, double Value, MotionEase Ease = MotionEase.Settle);

/// <summary>
/// One keyframe of the translation track: <paramref name="From"/> of the widget's own start offset
/// (its sheet's edge, the warp core) plus a fixed nudge in DIPs.
/// </summary>
public readonly record struct MoveKey(double At, double From, double X, double Y, MotionEase Ease = MotionEase.Settle);

/// <summary>One widget's look at a moment of its intro: what a debug render paints and what tests pin.</summary>
public readonly record struct IntroPose(double Opacity, double Scale, double X, double Y, double Rotation)
{
    /// <summary>[S-1017] At rest: where every track ends, and what a stopped show leaves every card at.</summary>
    public static IntroPose Rest { get; } = new(1, 1, 0, 0, 0);
}

/// <summary>
/// [S-1003] A style's keyframes, as data: the original's CSS keyframes, one track per animated
/// property, so the compositor animates each property on its own and a test can read any frame.
/// <para>
/// Every track ends on the identity (opacity 1, scale 1, no offset, no rotation): an intro is purely
/// cosmetic, and a widget must come to rest exactly where its geometry says it is.
/// </para>
/// </summary>
public sealed record IntroTracks(
    MotionKey[] Opacity,
    MotionKey[] Scale,
    MoveKey[] Move,
    MotionKey[] Rotation,
    IntroAxis Axis,
    bool Perspective)
{
    /// <summary>[S-1003] Flip's perspective distance, in DIPs: the original's <c>perspective(900px)</c>.</summary>
    public const double PerspectiveDips = 900;

    private static readonly MotionKey[] NoKeys = [];
    private static readonly MoveKey[] NoMoves = [];

    private static readonly MotionKey[] FadeIn = [new(0, 0), new(1, 1)];

    /// <summary>
    /// The tracks for <paramref name="style"/>, or null for a style with no widget keyframes: None,
    /// and Hacker, whose cover tile is the reveal ([S-1007]).
    /// </summary>
    public static IntroTracks? For(IntroAnimation style) => style switch
    {
        IntroAnimation.Fade => new(FadeIn, NoKeys, NoMoves, NoKeys, IntroAxis.None, false),

        IntroAnimation.Scale => new(
            FadeIn,
            [new(0, 0.94), new(1, 1)],
            [new(0, 0, 0, 16), new(1, 0, 0, 0)],
            NoKeys, IntroAxis.None, false),

        IntroAnimation.Slide => new(
            FadeIn,
            NoKeys,
            [new(0, 0, 0, 40), new(1, 0, 0, 0)],
            NoKeys, IntroAxis.None, false),

        // [S-1004] Opaque by 12%: the widget starts off-screen, so a long fade would only make it
        // arrive translucent.
        IntroAnimation.Sides or IntroAnimation.TopBottom => new(
            [new(0, 0), new(0.12, 1), new(1, 1)],
            NoKeys,
            [new(0, 1, 0, 0), new(1, 0, 0, 0)],
            NoKeys, IntroAxis.None, false),

        IntroAnimation.Flip => new(
            FadeIn,
            NoKeys,
            NoMoves,
            [new(0, -32), new(1, 0)],
            IntroAxis.Tilt, true),

        // The original also blurred 12px → 0. A live XAML element cannot be blurred without
        // re-rendering it into an effect surface every frame, so the fade and the settle carry it.
        IntroAnimation.Blur => new(
            FadeIn,
            [new(0, 1.04), new(1, 1)],
            NoMoves,
            NoKeys, IntroAxis.None, false),

        // [S-1006] Flies in from the warp core at near-zero scale, still accelerating when it hits its
        // slot (the Crash segment), an over-scale impact at 38%, then a decaying judder in place.
        IntroAnimation.Hyperspace => new(
            [new(0, 0), new(0.08, 1, MotionEase.Crash), new(0.38, 1, MotionEase.Linear), new(1, 1, MotionEase.Linear)],
            [
                new(0, 0.02), new(0.38, 1.05, MotionEase.Crash), new(0.46, 0.99, MotionEase.Linear),
                new(0.56, 1.01, MotionEase.Linear), new(0.68, 1, MotionEase.Linear), new(1, 1, MotionEase.Linear),
            ],
            [
                new(0, 1, 0, 0), new(0.38, 0, 0, 0, MotionEase.Crash), new(0.46, 0, -7, 5, MotionEase.Linear),
                new(0.56, 0, 5, -4, MotionEase.Linear), new(0.68, 0, -3, 2, MotionEase.Linear),
                new(0.80, 0, 2, -1, MotionEase.Linear), new(0.90, 0, -1, 0.5, MotionEase.Linear),
                new(1, 0, 0, 0, MotionEase.Linear),
            ],
            [
                new(0, 0), new(0.38, 0, MotionEase.Crash), new(0.46, -0.5, MotionEase.Linear),
                new(0.56, 0.35, MotionEase.Linear), new(0.68, -0.2, MotionEase.Linear),
                new(0.80, 0.1, MotionEase.Linear), new(0.90, 0, MotionEase.Linear), new(1, 0, MotionEase.Linear),
            ],
            IntroAxis.Roll, false),

        _ => null,
    };

    /// <summary>
    /// The pose at <paramref name="progress"/> (0..1) for a widget whose own start offset is
    /// <paramref name="start"/>. An untracked property sits at its identity.
    /// </summary>
    public IntroPose At(double progress, (double X, double Y) start)
    {
        var t = double.IsFinite(progress) ? Math.Clamp(progress, 0, 1) : 1;
        var (from, x, y) = Moved(t);
        return new IntroPose(
            Sample(Opacity, t, 1),
            Sample(Scale, t, 1),
            (from * start.X) + x,
            (from * start.Y) + y,
            Sample(Rotation, t, 0));
    }

    /// <summary>A scalar track's value at <paramref name="t"/>, or <paramref name="rest"/> for an empty track.</summary>
    public static double Sample(MotionKey[] keys, double t, double rest)
    {
        ArgumentNullException.ThrowIfNull(keys);
        if (keys.Length == 0)
        {
            return rest;
        }

        if (t <= keys[0].At)
        {
            return keys[0].Value;
        }

        for (var i = 1; i < keys.Length; i++)
        {
            if (t > keys[i].At)
            {
                continue;
            }

            var a = keys[i - 1];
            var b = keys[i];
            var f = Eased(b.Ease, Fraction(a.At, b.At, t));
            return a.Value + ((b.Value - a.Value) * f);
        }

        return keys[^1].Value;
    }

    private (double From, double X, double Y) Moved(double t)
    {
        if (Move.Length == 0)
        {
            return (0, 0, 0);
        }

        if (t <= Move[0].At)
        {
            return (Move[0].From, Move[0].X, Move[0].Y);
        }

        for (var i = 1; i < Move.Length; i++)
        {
            if (t > Move[i].At)
            {
                continue;
            }

            var a = Move[i - 1];
            var b = Move[i];
            var f = Eased(b.Ease, Fraction(a.At, b.At, t));
            return (Lerp(a.From, b.From, f), Lerp(a.X, b.X, f), Lerp(a.Y, b.Y, f));
        }

        return (Move[^1].From, Move[^1].X, Move[^1].Y);
    }

    private static double Fraction(double a, double b, double t) => b <= a ? 1 : (t - a) / (b - a);

    private static double Lerp(double a, double b, double f) => a + ((b - a) * f);

    /// <summary>The control points the compositor's cubic Bézier gets for an easing, or null for linear.</summary>
    public static (double X1, double Y1, double X2, double Y2)? Bezier(MotionEase ease) => ease switch
    {
        MotionEase.Settle => (0.22, 1, 0.36, 1),
        MotionEase.Crash => (0.55, 0, 1, 0.45),
        _ => null,
    };

    /// <summary>An easing applied to a segment fraction, as CSS and the compositor evaluate a cubic Bézier.</summary>
    public static double Eased(MotionEase ease, double f)
    {
        if (!(f > 0))
        {
            return 0;
        }

        if (f >= 1)
        {
            return 1;
        }

        return Bezier(ease) is { } c ? CubicBezier(c.X1, c.Y1, c.X2, c.Y2, f) : f;
    }

    /// <summary>
    /// y at x on a cubic Bézier from (0,0) to (1,1): x is monotonic for these curves, so a bisection
    /// on the parameter converges without Newton's failure cases.
    /// </summary>
    private static double CubicBezier(double x1, double y1, double x2, double y2, double x)
    {
        static double Coord(double p1, double p2, double s)
        {
            var u = 1 - s;
            return (3 * u * u * s * p1) + (3 * u * s * s * p2) + (s * s * s);
        }

        double lo = 0, hi = 1, s = x;
        for (var i = 0; i < 40; i++)
        {
            s = (lo + hi) / 2;
            if (Coord(x1, x2, s) < x)
            {
                lo = s;
            }
            else
            {
                hi = s;
            }
        }

        return Coord(y1, y2, s);
    }
}
