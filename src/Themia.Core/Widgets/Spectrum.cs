using System.Buffers.Binary;

namespace Themia.Core.Widgets;

/// <summary>How a loopback capture's samples are encoded. Shared-mode mix formats are float in practice.</summary>
public enum SampleEncoding
{
    /// <summary>A format the analyser does not read; it contributes silence rather than noise.</summary>
    Unsupported,
    Float32,
    Int16,
    Int32,
}

/// <summary>
/// [S-3201] The audio spectrum the audio visualizer draws: a windowed FFT over the last
/// <see cref="FftSize"/> samples of what the PC is playing, grouped into <see cref="Bands"/>
/// log-spaced bands of 0..1 — the original's analyser, bin for bin.
/// <para>
/// Pure and allocation-free after construction, so the capture thread can run it thirty times a
/// second without feeding the GC, and a test can drive it with a sine.
/// </para>
/// </summary>
public sealed class Spectrum
{
    public const int FftSize = 1024;

    public const int Bands = 24;

    /// <summary>[S-3202] About thirty frames a second while there is something to draw.</summary>
    public const int FrameMs = 33;

    /// <summary>
    /// [S-3202] The wait between drains once the bars are down and nothing arrives: well inside the
    /// capture's 200 ms buffer, so the first packet of new sound is not lost, and five times fewer
    /// wake-ups on a silent desktop.
    /// </summary>
    public const int IdleFrameMs = 150;

    /// <summary>The original's sqrt-compression gain.</summary>
    private const float Gain = 3.5f;

    private readonly float[] _window = new float[FftSize];
    private readonly float[] _re = new float[FftSize];
    private readonly float[] _im = new float[FftSize];
    private readonly float[] _magnitudes = new float[FftSize / 2];
    private readonly (int Low, int High)[] _bins = new (int, int)[Bands];

    public Spectrum()
    {
        for (var i = 0; i < FftSize; i++)
        {
            _window[i] = 0.5f - (0.5f * MathF.Cos(2 * MathF.PI * i / (FftSize - 1)));
        }

        // Log-spaced from bin 1 to the Nyquist bin: the low bands are a bin or two wide, the top
        // band a few hundred, which is how a person hears the difference anyway.
        var half = FftSize / 2;

        for (var b = 0; b < Bands; b++)
        {
            var low = (int)Math.Pow(half, b / (double)Bands);
            var high = Math.Min(half, Math.Max(low + 1, (int)Math.Pow(half, (b + 1) / (double)Bands)));
            _bins[b] = (low, high);
        }
    }

    /// <summary>The bins band <paramref name="band"/> averages, [low, high).</summary>
    public (int Low, int High) BinsOf(int band) => _bins[band];

    /// <summary>
    /// Analyse the last <see cref="FftSize"/> mono samples into <paramref name="bands"/>. Fewer
    /// samples than that are zero-padded at the front (the start of a capture).
    /// </summary>
    public void Analyze(ReadOnlySpan<float> samples, Span<float> bands)
    {
        if (bands.Length < Bands)
        {
            throw new ArgumentException($"needs room for {Bands} bands", nameof(bands));
        }

        var take = Math.Min(samples.Length, FftSize);
        var pad = FftSize - take;
        var tail = samples[^take..];

        for (var i = 0; i < FftSize; i++)
        {
            _re[i] = i < pad ? 0 : tail[i - pad] * _window[i];
            _im[i] = 0;
        }

        Transform(_re, _im);

        for (var i = 0; i < _magnitudes.Length; i++)
        {
            _magnitudes[i] = MathF.Sqrt((_re[i] * _re[i]) + (_im[i] * _im[i]));
        }

        for (var b = 0; b < Bands; b++)
        {
            var (low, high) = _bins[b];
            var sum = 0f;

            for (var i = low; i < high; i++)
            {
                sum += _magnitudes[i];
            }

            var average = sum / Math.Max(1, high - low);
            bands[b] = Math.Min(1f, MathF.Sqrt(average / FftSize) * Gain);
        }
    }

    /// <summary>
    /// [S-3202] Bars rise at once and fall gently: each frame a bar is the new level or its last
    /// height less <paramref name="fall"/>, whichever is higher. Without it the bars flicker at the
    /// capture rate; the original leaned on a CSS transition for the same effect.
    /// </summary>
    public static void Settle(Span<float> shown, ReadOnlySpan<float> fresh, float fall)
    {
        for (var i = 0; i < shown.Length && i < fresh.Length; i++)
        {
            shown[i] = Math.Clamp(Math.Max(fresh[i], shown[i] - fall), 0, 1);
        }
    }

    /// <summary>
    /// [S-3202] How long the capture waits before its next drain: a frame while sound arrives or a
    /// bar is still up, the idle wait once everything has fallen and nothing came.
    /// </summary>
    public static int FrameWait(bool arrived, bool lit) => arrived || lit ? FrameMs : IdleFrameMs;

    /// <summary>
    /// Mix interleaved frames down to mono and append them to <paramref name="destination"/>.
    /// Returns how many samples were written. A silent packet (the capture flags it) and an
    /// unsupported encoding both write zeros, so time keeps moving and the bars fall.
    /// </summary>
    public static int MixDown(ReadOnlySpan<byte> data, SampleEncoding encoding, int channels, int frames, bool silent, Span<float> destination)
    {
        channels = Math.Max(1, channels);
        frames = Math.Min(frames, destination.Length);
        var width = encoding switch
        {
            SampleEncoding.Float32 or SampleEncoding.Int32 => 4,
            SampleEncoding.Int16 => 2,
            _ => 0,
        };

        if (silent || width == 0 || data.Length < frames * channels * width)
        {
            destination[..frames].Clear();
            return frames;
        }

        for (var f = 0; f < frames; f++)
        {
            var sum = 0f;

            for (var c = 0; c < channels; c++)
            {
                var at = ((f * channels) + c) * width;
                sum += encoding switch
                {
                    SampleEncoding.Float32 => BinaryPrimitives.ReadSingleLittleEndian(data[at..]),
                    SampleEncoding.Int16 => BinaryPrimitives.ReadInt16LittleEndian(data[at..]) / 32768f,
                    _ => BinaryPrimitives.ReadInt32LittleEndian(data[at..]) / 2147483648f,
                };
            }

            var mono = sum / channels;
            destination[f] = float.IsFinite(mono) ? mono : 0;
        }

        return frames;
    }

    /// <summary>In-place iterative radix-2 FFT.</summary>
    private static void Transform(float[] re, float[] im)
    {
        var n = re.Length;

        for (int i = 1, j = 0; i < n; i++)
        {
            var bit = n >> 1;

            for (; (j & bit) != 0; bit >>= 1)
            {
                j ^= bit;
            }

            j ^= bit;

            if (i < j)
            {
                (re[i], re[j]) = (re[j], re[i]);
                (im[i], im[j]) = (im[j], im[i]);
            }
        }

        for (var length = 2; length <= n; length <<= 1)
        {
            var angle = -2 * Math.PI / length;
            var wRe = (float)Math.Cos(angle);
            var wIm = (float)Math.Sin(angle);

            for (var start = 0; start < n; start += length)
            {
                var curRe = 1f;
                var curIm = 0f;

                for (var k = 0; k < length / 2; k++)
                {
                    var a = start + k;
                    var b = a + (length / 2);
                    var tRe = (re[b] * curRe) - (im[b] * curIm);
                    var tIm = (re[b] * curIm) + (im[b] * curRe);

                    re[b] = re[a] - tRe;
                    im[b] = im[a] - tIm;
                    re[a] += tRe;
                    im[a] += tIm;

                    var nextRe = (curRe * wRe) - (curIm * wIm);
                    curIm = (curRe * wIm) + (curIm * wRe);
                    curRe = nextRe;
                }
            }
        }
    }
}

/// <summary>What a change to the capture's subscribers asks of the worker.</summary>
public enum CaptureAction
{
    /// <summary>Nothing: the worker already runs (or still does) for the others.</summary>
    None,

    /// <summary>Start a worker: the first subscriber arrived, or the last worker died under live subscribers.</summary>
    Start,

    /// <summary>Stop the worker: the last subscriber left.</summary>
    Stop,
}

/// <summary>
/// [S-3203] The shared loopback capture's reference count (the original's [S-SPEC-05]). One worker
/// serves every audio visualizer; it starts with the first subscriber and stops with the last, so a
/// second tile, a view switch or a remount that overlaps old and new never kills it for the others.
/// A worker that died is restarted by the next arrival, not left wedged. Not thread-safe: the
/// owner holds a lock around it.
/// </summary>
public sealed class CaptureLease
{
    public int Subscribers { get; private set; }

    public bool Running { get; private set; }

    /// <summary>
    /// [S-3204] Whether the current worker reached the device: null until it has tried, false after
    /// a capture failed, true once one started. A new worker starts unknown.
    /// </summary>
    public bool? Available { get; private set; }

    /// <summary>[S-3204] The worker's news about the device. True when it changed what subscribers were told.</summary>
    public bool Report(bool available)
    {
        if (!Running || Available == available)
        {
            return false;
        }

        Available = available;
        return true;
    }

    public CaptureAction Add()
    {
        Subscribers++;

        if (Running)
        {
            return CaptureAction.None;
        }

        Running = true;
        Available = null;
        return CaptureAction.Start;
    }

    public CaptureAction Remove()
    {
        // An over-release (a stray second Remove) never goes negative or stops a worker twice.
        if (Subscribers == 0)
        {
            return CaptureAction.None;
        }

        Subscribers--;

        if (Subscribers > 0 || !Running)
        {
            return CaptureAction.None;
        }

        Running = false;
        Available = null;
        return CaptureAction.Stop;
    }

    /// <summary>The worker ended on its own. The next <see cref="Add"/> starts another.</summary>
    public void WorkerEnded()
    {
        Running = false;
        Available = null;
    }
}
