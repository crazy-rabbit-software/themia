using System.Buffers.Binary;

namespace Themia.Core.Widgets;

/// <summary>What one voice of a synthesised cue is made of. A closed set, so the renderer's dispatch is exhaustive.</summary>
public enum VoiceShape
{
    Sine,
    Square,
    Sawtooth,
    Triangle,

    /// <summary>White noise through a band-pass filter; the voice's pitch envelope is the filter's centre.</summary>
    Noise,
}

/// <summary>One breakpoint of an envelope: a value at a time, in milliseconds from the voice's start.</summary>
public readonly record struct EnvPoint(double Ms, double Value);

/// <summary>
/// One voice of a cue: an oscillator (or filtered noise) with a pitch envelope and a gain envelope,
/// both piecewise linear between their breakpoints, sounding from <paramref name="StartMs"/> to
/// <paramref name="EndMs"/> of the cue.
/// </summary>
public sealed record SoundVoice(VoiceShape Shape, double StartMs, double EndMs, EnvPoint[] Pitch, EnvPoint[] Gain);

/// <summary>
/// [S-1462] A tiny declarative score — the same model the original's Web Audio player replayed —
/// rendered here into PCM rather than scheduled onto an audio graph.
/// <para>
/// Synthesised, never sampled: the original shipped no audio assets (every cue was built from
/// oscillators), so there is nothing to bundle, and a plan is plain data that tests can pin without
/// a sound card. The renderer is the one imperative part, and it is a loop over samples.
/// </para>
/// </summary>
public sealed class SoundPlan
{
    /// <summary>Samples per second. CD rate: every Windows audio path takes it without resampling.</summary>
    public const int SampleRate = 44100;

    /// <summary>Quiet tail after the last voice, so the last release is not cut off.</summary>
    public const double TailMs = 250;

    /// <summary>The noise filter's quality factor, the original's band-pass Q.</summary>
    private const double NoiseQ = 0.8;

    /// <summary>The noise generator's fixed seed: the same plan renders the same bytes every time.</summary>
    private const uint NoiseSeed = 0x9E3779B9;

    public static SoundPlan Empty { get; } = new([]);

    public SoundPlan(IReadOnlyList<SoundVoice> voices)
    {
        ArgumentNullException.ThrowIfNull(voices);
        Voices = voices;
        var end = voices.Count == 0 ? 0 : voices.Max(v => v.EndMs);
        TotalMs = end > 0 ? end + TailMs : 0;
    }

    public IReadOnlyList<SoundVoice> Voices { get; }

    /// <summary>The cue's length: the last voice's end plus the tail, or zero for no voices.</summary>
    public double TotalMs { get; }

    public bool IsEmpty => Voices.Count == 0;

    /// <summary>
    /// The largest sum of concurrent voice gains: an upper bound on the cue's amplitude, since the
    /// waveforms only reach it when they line up in phase. Every envelope is piecewise linear, so
    /// the sum peaks at one of the breakpoints, which makes evaluating them all exact.
    /// </summary>
    public double PeakBound()
    {
        var peak = 0.0;

        foreach (var at in Voices.SelectMany(v => v.Gain.Select(p => v.StartMs + p.Ms).Append(v.EndMs)))
        {
            var sum = Voices.Sum(v => GainAt(v, at));
            peak = Math.Max(peak, sum);
        }

        return peak;
    }

    /// <summary>
    /// [S-1463] Scale every gain so the peak bound lands on <paramref name="target"/>. Without it a
    /// plan with four stacked partials comes out several times louder than a single square beep, and
    /// the hand-tuned ones clip at full volume.
    /// </summary>
    public SoundPlan Normalised(double target)
    {
        var bound = PeakBound();

        if (bound <= 0)
        {
            return this;
        }

        var factor = target / bound;

        return new SoundPlan(Voices
            .Select(v => v with { Gain = [.. v.Gain.Select(p => p with { Value = p.Value * factor })] })
            .ToArray());
    }

    /// <summary>
    /// The cue at <paramref name="volume"/> (0..1) as a complete 16-bit mono RIFF/WAVE file — a file
    /// image because the player (<c>PlaySound</c> with <c>SND_MEMORY</c>) takes one. Empty plans and
    /// a zero volume give an empty array: there is nothing to play.
    /// </summary>
    public byte[] RenderWave(double volume)
    {
        volume = double.IsFinite(volume) ? Math.Clamp(volume, 0, 1) : 0;

        if (IsEmpty || volume <= 0)
        {
            return [];
        }

        var samples = Render(volume);
        const int HeaderBytes = 44;
        var dataBytes = samples.Length * 2;
        var buffer = new byte[HeaderBytes + dataBytes];

        WriteHeader(buffer, dataBytes);

        for (var i = 0; i < samples.Length; i++)
        {
            var value = Math.Clamp(samples[i], -1, 1);
            BinaryPrimitives.WriteInt16LittleEndian(buffer.AsSpan(HeaderBytes + (i * 2)), (short)Math.Round(value * short.MaxValue));
        }

        return buffer;
    }

    /// <summary>The mixed cue as floating-point samples at <see cref="SampleRate"/>.</summary>
    public double[] Render(double volume)
    {
        var total = (int)Math.Ceiling(TotalMs * SampleRate / 1000);
        var mix = new double[total];

        foreach (var voice in Voices)
        {
            AddVoice(mix, voice, volume);
        }

        return mix;
    }

    private static void AddVoice(double[] mix, SoundVoice voice, double volume)
    {
        var first = Math.Max(0, (int)Math.Floor(voice.StartMs * SampleRate / 1000));
        var last = Math.Min(mix.Length, (int)Math.Ceiling(voice.EndMs * SampleRate / 1000));

        var phase = 0.0;
        var noise = new NoiseBand(NoiseSeed ^ (uint)first);

        for (var i = first; i < last; i++)
        {
            var ms = (i * 1000.0 / SampleRate) - voice.StartMs;
            var gain = Envelope(voice.Gain, ms);

            if (voice.Shape == VoiceShape.Noise)
            {
                mix[i] += noise.Next(Math.Max(1, Envelope(voice.Pitch, ms))) * gain * volume;
                continue;
            }

            var step = Math.Max(0, Envelope(voice.Pitch, ms)) / SampleRate;
            mix[i] += Oscillator(voice.Shape, phase, step) * gain * volume;

            phase += step;
            phase -= Math.Floor(phase);
        }
    }

    /// <summary>
    /// One sample of a periodic waveform at <paramref name="phase"/> (0..1). The square and the
    /// sawtooth are band-limited with PolyBLEP: a naive edge at 2 kHz aliases into an audible whine,
    /// which a Web Audio oscillator never had.
    /// </summary>
    private static double Oscillator(VoiceShape shape, double phase, double step) => shape switch
    {
        VoiceShape.Sine => Math.Sin(2 * Math.PI * phase),
        VoiceShape.Square => (phase < 0.5 ? 1.0 : -1.0) + PolyBlep(phase, step) - PolyBlep((phase + 0.5) % 1, step),
        VoiceShape.Sawtooth => (2 * phase) - 1 - PolyBlep(phase, step),
        VoiceShape.Triangle => phase < 0.5 ? (4 * phase) - 1 : 3 - (4 * phase),
        _ => 0,
    };

    private static double PolyBlep(double t, double dt)
    {
        if (dt <= 0)
        {
            return 0;
        }

        if (t < dt)
        {
            t /= dt;
            return t + t - (t * t) - 1;
        }

        if (t > 1 - dt)
        {
            t = (t - 1) / dt;
            return (t * t) + t + t + 1;
        }

        return 0;
    }

    /// <summary>
    /// An envelope's value at <paramref name="ms"/> from the voice's start: linear between
    /// breakpoints and held at the end values outside them, as an AudioParam ramp behaves.
    /// </summary>
    public static double Envelope(EnvPoint[] envelope, double ms)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        if (envelope.Length == 0)
        {
            return 0;
        }

        if (ms <= envelope[0].Ms)
        {
            return envelope[0].Value;
        }

        for (var i = 1; i < envelope.Length; i++)
        {
            var (t1, v1) = (envelope[i].Ms, envelope[i].Value);

            if (ms > t1)
            {
                continue;
            }

            var (t0, v0) = (envelope[i - 1].Ms, envelope[i - 1].Value);
            return t1 <= t0 ? v1 : v0 + ((v1 - v0) * (ms - t0) / (t1 - t0));
        }

        return envelope[^1].Value;
    }

    /// <summary>A voice's gain at an absolute cue time; nothing outside the voice.</summary>
    private static double GainAt(SoundVoice voice, double atMs)
        => atMs < voice.StartMs || atMs > voice.EndMs ? 0 : Envelope(voice.Gain, atMs - voice.StartMs);

    private static void WriteHeader(byte[] buffer, int dataBytes)
    {
        const short Channels = 1;
        const short BitsPerSample = 16;
        const short BlockAlign = Channels * BitsPerSample / 8;

        var span = buffer.AsSpan();

        "RIFF"u8.CopyTo(span);
        BinaryPrimitives.WriteInt32LittleEndian(span[4..], 36 + dataBytes);
        "WAVE"u8.CopyTo(span[8..]);

        "fmt "u8.CopyTo(span[12..]);
        BinaryPrimitives.WriteInt32LittleEndian(span[16..], 16);       // PCM fmt chunk size
        BinaryPrimitives.WriteInt16LittleEndian(span[20..], 1);        // PCM, uncompressed
        BinaryPrimitives.WriteInt16LittleEndian(span[22..], Channels);
        BinaryPrimitives.WriteInt32LittleEndian(span[24..], SampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(span[28..], SampleRate * BlockAlign);
        BinaryPrimitives.WriteInt16LittleEndian(span[32..], BlockAlign);
        BinaryPrimitives.WriteInt16LittleEndian(span[34..], BitsPerSample);

        "data"u8.CopyTo(span[36..]);
        BinaryPrimitives.WriteInt32LittleEndian(span[40..], dataBytes);
    }

    /// <summary>
    /// Seeded white noise through an RBJ band-pass biquad (constant 0 dB peak gain), its centre
    /// moving with the voice's envelope. Coefficients are recomputed every sample: a noise voice
    /// lasts a hundred milliseconds, so the cost is nothing.
    /// </summary>
    private struct NoiseBand(uint seed)
    {
        private uint _state = seed == 0 ? 1u : seed;
        private double _x1;
        private double _x2;
        private double _y1;
        private double _y2;

        public double Next(double centreHz)
        {
            // xorshift32: deterministic, so a test can pin the rendered bytes.
            _state ^= _state << 13;
            _state ^= _state >> 17;
            _state ^= _state << 5;
            var x = (_state / (double)uint.MaxValue * 2) - 1;

            var w0 = 2 * Math.PI * Math.Min(centreHz, (SampleRate / 2.0) - 1) / SampleRate;
            var alpha = Math.Sin(w0) / (2 * NoiseQ);
            var a0 = 1 + alpha;
            var b0 = alpha / a0;
            var b2 = -alpha / a0;
            var a1 = -2 * Math.Cos(w0) / a0;
            var a2 = (1 - alpha) / a0;

            var y = (b0 * x) + (b2 * _x2) - (a1 * _y1) - (a2 * _y2);

            _x2 = _x1;
            _x1 = x;
            _y2 = _y1;
            _y1 = y;

            return y;
        }
    }
}
