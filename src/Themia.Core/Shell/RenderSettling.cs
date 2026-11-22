namespace Themia.Core.Shell;

/// <summary>[S-15900] What a render's settle does after one look at its page.</summary>
public enum SettleStep
{
    /// <summary>Something under the page is still loading: wait for that work (or the next frame when it has no task).</summary>
    AwaitWork,

    /// <summary>Nothing is loading, but the picture is not yet the same frame after frame: look again next frame.</summary>
    NextFrame,

    /// <summary>Nothing loading and the picture unchanged over <see cref="RenderSettling.QuietFrames"/> frames: take it.</summary>
    Settled,

    /// <summary>The ceiling passed first: say what was still busy, loudly, and take the picture as it stands.</summary>
    CeilingHit,
}

/// <summary>
/// [S-15900] A render or check waits for its page to be finished, never for a fixed time ([S-69]). One settle looks at the
/// page once a frame: while anything under it is still loading (an image, a view's own load, a replayed recording, a
/// surface's intro) it waits for that; then it waits until the picture is the same <see cref="QuietFrames"/> frames in a
/// row (an entrance, a glide or a fade has ended); all under one ceiling, past which what was still busy is said loudly
/// and the picture is taken as it stands. The decision is this class; the looking is the app's (<c>RenderSettle</c>).
/// Pure, one instance per settle.
/// </summary>
public sealed class RenderSettling
{
    /// <summary>The frames in a row the picture must be unchanged for (two comparisons, three looks).</summary>
    public const int QuietFrames = 2;

    /// <summary>The longest one settle waits by default: past it, what is still busy is named and the picture taken.</summary>
    public static readonly TimeSpan DefaultCeiling = TimeSpan.FromSeconds(15);

    private readonly TimeSpan _ceiling;
    private readonly int _quiet;
    private int _sameInARow;

    /// <summary>A settle under <paramref name="ceiling"/> that needs <paramref name="quietFrames"/> unchanged frames in a row.</summary>
    public RenderSettling(TimeSpan ceiling, int quietFrames = QuietFrames)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(ceiling, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(quietFrames, 1);
        _ceiling = ceiling;
        _quiet = quietFrames;
    }

    /// <summary>The ceiling of this settle.</summary>
    public TimeSpan Ceiling => _ceiling;

    /// <summary>The unchanged frames seen in a row so far.</summary>
    public int SameInARow => _sameInARow;

    /// <summary>
    /// The next step after one look: <paramref name="elapsed"/> since the settle began, <paramref name="pending"/> things
    /// still loading, and whether the picture was <paramref name="same"/> as the last look's (null: not compared, as on
    /// the first look or while something loads). Work still loading resets the quiet frames: what it draws is a change.
    /// </summary>
    public SettleStep Next(TimeSpan elapsed, int pending, bool? same)
    {
        if (pending > 0)
        {
            _sameInARow = 0;
            return elapsed >= _ceiling ? SettleStep.CeilingHit : SettleStep.AwaitWork;
        }

        _sameInARow = same == true ? _sameInARow + 1 : 0;
        if (_sameInARow >= _quiet)
        {
            return SettleStep.Settled;
        }

        return elapsed >= _ceiling ? SettleStep.CeilingHit : SettleStep.NextFrame;
    }

    /// <summary>
    /// The loud line of a settle that hit its ceiling: <paramref name="what"/> (the picture or check), the ceiling, and
    /// what was still busy (<paramref name="pending"/>; none: the picture was still changing).
    /// </summary>
    public static string CeilingLine(string what, TimeSpan ceiling, IEnumerable<string> pending)
    {
        ArgumentNullException.ThrowIfNull(pending);
        var busy = string.Join(", ", pending.Distinct(StringComparer.Ordinal));
        return $"settle: {what} NOT SETTLED after {ceiling.TotalSeconds:0.#} s: "
            + (busy.Length == 0 ? "the picture was still changing" : $"still loading {busy}")
            + "; drawn as it stands";
    }

    /// <summary>
    /// [S-15901] Whether two looks at a page are the same picture: the same size and every byte the same, outside
    /// <paramref name="masks"/> (what turns forever on purpose, an indeterminate ring, in pixels of the look). Rows of
    /// <paramref name="width"/> pixels of <paramref name="bytesPerPixel"/> bytes.
    /// </summary>
    public static bool SamePicture(ReadOnlySpan<byte> last, ReadOnlySpan<byte> now, int width, int bytesPerPixel, IReadOnlyList<PixelBox> masks)
    {
        ArgumentNullException.ThrowIfNull(masks);
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(bytesPerPixel, 1);
        if (last.Length != now.Length || now.Length % (width * bytesPerPixel) != 0)
        {
            return false;
        }

        var stride = width * bytesPerPixel;
        var height = now.Length / stride;
        if (masks.Count == 0)
        {
            return last.SequenceEqual(now);
        }

        for (var y = 0; y < height; y++)
        {
            var row = y * stride;
            var x = 0;
            while (x < width)
            {
                // The run of this row up to the next masked pixel, compared at once.
                var end = width;
                var skipTo = x;
                foreach (var box in masks)
                {
                    if (y < box.Y || y >= box.Y + box.Height || box.X + box.Width <= x)
                    {
                        continue;
                    }

                    if (box.X <= x)
                    {
                        skipTo = Math.Max(skipTo, Math.Min(width, box.X + box.Width));
                    }
                    else
                    {
                        end = Math.Min(end, box.X);
                    }
                }

                if (skipTo > x)
                {
                    x = skipTo;
                    continue;
                }

                var from = row + (x * bytesPerPixel);
                var length = (end - x) * bytesPerPixel;
                if (!last.Slice(from, length).SequenceEqual(now.Slice(from, length)))
                {
                    return false;
                }

                x = end;
            }
        }

        return true;
    }
}

/// <summary>[S-15901] A box of pixels of a look at a page: left, top, width and height.</summary>
public readonly record struct PixelBox(int X, int Y, int Width, int Height);
