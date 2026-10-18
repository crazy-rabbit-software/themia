namespace Themia.Core.Widgets;

/// <summary>
/// [S-3214] One frame of the shared loopback capture: the spectrum's bands ([S-3201]) and the
/// waveform's trace, both settled. Shared between subscribers: read it, never keep or change it.
/// </summary>
/// <param name="Bands"><see cref="Spectrum.Bands"/> levels, 0..1.</param>
/// <param name="Trace"><see cref="Oscilloscope.Points"/> signed levels, −1..1.</param>
public sealed record AudioFrame(float[] Bands, float[] Trace);

/// <summary>
/// [S-3214] The waveform the Scope design draws ([S-3212]), from the same analysis window the
/// spectrum reads ([S-3201]): no second capture, no second buffer. A trigger on the newest rising
/// zero crossing holds a steady tone still instead of letting it crawl across the tile, and each
/// point keeps the loudest sample of its stretch so a peak between two points is not lost.
/// <para>
/// Pure and allocation-free, like <see cref="Spectrum"/>: the capture thread runs it once a frame.
/// </para>
/// </summary>
public static class Oscilloscope
{
    /// <summary>The trace's resolution: the widest tile's points ([S-3213]) and no more.</summary>
    public const int Points = 128;

    /// <summary>The stretch of samples shown, the newest that a trigger leaves: about 11 ms at 48 kHz, a few cycles of a voice.</summary>
    public const int Span = 512;

    /// <summary>[S-3215] How much of its swing the line keeps per frame once nothing arrives: flat in about half a second.</summary>
    public const float Fade = 0.8f;

    /// <summary>[S-3215] Under this a point is at rest: the line is flat and the worker may go idle.</summary>
    public const float Rest = 0.01f;

    /// <summary>
    /// [S-3214] Trace the newest <see cref="Span"/> samples of <paramref name="window"/> (oldest
    /// first) into <paramref name="trace"/>: from the newest rising zero crossing that still leaves a
    /// whole span after it (else from the newest span), each point the loudest sample of its stretch,
    /// compressed by a square root as the spectrum is, so a quiet passage still moves the line.
    /// </summary>
    public static void Trace(ReadOnlySpan<float> window, Span<float> trace)
    {
        if (trace.Length == 0)
        {
            return;
        }

        var span = Math.Min(Span, window.Length);
        if (span == 0)
        {
            trace.Clear();
            return;
        }

        var latest = window.Length - span;
        var start = latest;

        for (var i = latest; i > 0; i--)
        {
            if (window[i - 1] < 0 && window[i] >= 0)
            {
                start = i;
                break;
            }
        }

        Peaks(window.Slice(start, span), trace);

        for (var p = 0; p < trace.Length; p++)
        {
            trace[p] = Compress(trace[p]);
        }
    }

    /// <summary>
    /// [S-3215] Settle the shown trace: the fresh one while sound arrives, else the last one fading
    /// by <see cref="Fade"/> until every point is under <see cref="Rest"/> and the line lies flat.
    /// Returns whether any point is still off the centre line.
    /// </summary>
    public static bool Settle(Span<float> shown, ReadOnlySpan<float> fresh, bool arrived)
    {
        var moving = false;

        for (var i = 0; i < shown.Length; i++)
        {
            var next = arrived && i < fresh.Length ? fresh[i] : shown[i] * Fade;
            next = float.IsFinite(next) ? Math.Clamp(next, -1, 1) : 0;

            if (Math.Abs(next) < Rest)
            {
                next = 0;
            }

            shown[i] = next;
            moving |= next != 0;
        }

        return moving;
    }

    /// <summary>
    /// [S-3213] Fit a trace to a tile's point count: each point the loudest of the trace's points it
    /// covers (signed), so a narrow tile keeps the peaks a wide one shows. Bad values are silence.
    /// </summary>
    public static void Resample(ReadOnlySpan<float> trace, Span<float> into) => Peaks(trace, into);

    /// <summary>The signed sample of largest magnitude in each of <paramref name="into"/>'s stretches of <paramref name="from"/>.</summary>
    private static void Peaks(ReadOnlySpan<float> from, Span<float> into)
    {
        if (into.Length == 0)
        {
            return;
        }

        if (from.Length == 0)
        {
            into.Clear();
            return;
        }

        for (var p = 0; p < into.Length; p++)
        {
            var low = (int)((long)p * from.Length / into.Length);
            var high = Math.Max(low + 1, (int)((long)(p + 1) * from.Length / into.Length));
            var peak = 0f;

            for (var i = low; i < high && i < from.Length; i++)
            {
                var value = float.IsFinite(from[i]) ? from[i] : 0;
                if (Math.Abs(value) > Math.Abs(peak))
                {
                    peak = value;
                }
            }

            into[p] = Math.Clamp(peak, -1, 1);
        }
    }

    /// <summary>Square-root compression keeping the sign: 0.01 → 0.1, 0.25 → 0.5, 1 → 1.</summary>
    private static float Compress(float value)
    {
        if (!float.IsFinite(value))
        {
            return 0;
        }

        var magnitude = MathF.Sqrt(Math.Min(1, Math.Abs(value)));
        return value < 0 ? -magnitude : magnitude;
    }
}
