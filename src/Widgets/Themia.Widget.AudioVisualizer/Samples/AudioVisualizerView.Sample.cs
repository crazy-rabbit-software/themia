using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11850] [S-3207] The visualizer's sample: a picture cannot capture the PC's sound, so it draws a
/// still, synthetic spectrum (bass high, falling towards the treble with some unevenness).
/// </summary>
internal sealed partial class AudioVisualizerView : IWidgetSample
{
    public Task ShowSampleAsync()
    {
        var bands = new float[Spectrum.Bands];
        for (var i = 0; i < bands.Length; i++)
        {
            var t = i / (double)(bands.Length - 1);
            var shape = 0.88 - (0.55 * t) + (0.14 * Math.Sin(i * 1.7)) + (0.06 * Math.Cos(i * 4.3));
            bands[i] = (float)Math.Clamp(shape, 0.05, 1);
        }

        // [S-3212] The waveform's still: a voice-like sum of three partials, louder at the start.
        var trace = new float[Oscilloscope.Points];
        for (var i = 0; i < trace.Length; i++)
        {
            var t = i / (double)trace.Length;
            var wave = (0.55 * Math.Sin(2 * Math.PI * 3 * t)) + (0.25 * Math.Sin(2 * Math.PI * 7 * t + 0.6)) + (0.12 * Math.Sin(2 * Math.PI * 17 * t));
            trace[i] = (float)Math.Clamp(wave * (1.1 - (0.4 * t)), -1, 1);
        }

        ShowAvailability(available: true);
        _last = new AudioFrame(bands, trace);
        _bars.SetFrame(_last);
        return Task.CompletedTask;
    }
}
