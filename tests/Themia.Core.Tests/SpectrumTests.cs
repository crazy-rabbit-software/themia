using System.Buffers.Binary;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-3201]..[S-3204] the loopback spectrum: analysis, decoding, settling, cadence, the shared capture's count and its availability.</summary>
public class SpectrumTests
{
    private static float[] Sine(double hz, int count, double amplitude = 0.8)
    {
        var samples = new float[count];
        for (var i = 0; i < count; i++)
        {
            samples[i] = (float)(amplitude * Math.Sin(2 * Math.PI * hz * i / 48000));
        }

        return samples;
    }

    [Fact]
    public void S3201_silence_is_all_bars_down()
    {
        var bands = new float[Spectrum.Bands];
        new Spectrum().Analyze(new float[Spectrum.FftSize], bands);
        Assert.All(bands, b => Assert.Equal(0, b));
    }

    [Fact]
    public void S3201_a_low_tone_lifts_the_low_bands_and_a_high_tone_the_high_ones()
    {
        var spectrum = new Spectrum();
        var low = new float[Spectrum.Bands];
        var high = new float[Spectrum.Bands];

        spectrum.Analyze(Sine(120, Spectrum.FftSize), low);
        spectrum.Analyze(Sine(9000, Spectrum.FftSize), high);

        var lowPeak = Array.IndexOf(low, low.Max());
        var highPeak = Array.IndexOf(high, high.Max());

        Assert.True(lowPeak < Spectrum.Bands / 2, $"120 Hz peaked in band {lowPeak}");
        Assert.True(highPeak > Spectrum.Bands / 2, $"9 kHz peaked in band {highPeak}");
        Assert.True(low.Max() > 0.2f);
    }

    [Fact]
    public void S3201_levels_stay_within_zero_to_one_even_for_a_full_scale_square()
    {
        var square = Enumerable.Range(0, Spectrum.FftSize).Select(i => i % 64 < 32 ? 1f : -1f).ToArray();
        var bands = new float[Spectrum.Bands];
        new Spectrum().Analyze(square, bands);
        Assert.All(bands, b => Assert.InRange(b, 0f, 1f));
    }

    [Fact]
    public void S3201_bands_cover_bin_one_to_nyquist_without_gaps_or_empty_bands()
    {
        var spectrum = new Spectrum();
        Assert.Equal(1, spectrum.BinsOf(0).Low);
        Assert.Equal(Spectrum.FftSize / 2, spectrum.BinsOf(Spectrum.Bands - 1).High);

        for (var b = 0; b < Spectrum.Bands; b++)
        {
            var (low, high) = spectrum.BinsOf(b);
            Assert.True(high > low, $"band {b} is empty");
        }
    }

    [Fact]
    public void S3201_a_short_capture_is_padded_not_rejected()
    {
        var bands = new float[Spectrum.Bands];
        new Spectrum().Analyze(Sine(440, 100), bands);
        Assert.All(bands, b => Assert.InRange(b, 0f, 1f));
    }

    [Fact]
    public void S3202_bars_rise_at_once_and_fall_by_the_step()
    {
        float[] shown = [0.2f, 0.9f, 0.05f];
        Spectrum.Settle(shown, [0.6f, 0.1f, 0f], fall: 0.1f);
        Assert.Equal(0.6f, shown[0], 5);
        Assert.Equal(0.8f, shown[1], 5);
        Assert.Equal(0f, shown[2], 5);
    }

    [Fact]
    public void S3201_float_frames_mix_down_to_mono()
    {
        var data = new byte[2 * 2 * 4];
        BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(0), 0.5f);
        BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(4), -0.1f);
        BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(8), 1f);
        BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan(12), 1f);

        var mono = new float[2];
        Assert.Equal(2, Spectrum.MixDown(data, SampleEncoding.Float32, 2, 2, silent: false, mono));
        Assert.Equal(0.2f, mono[0], 5);
        Assert.Equal(1f, mono[1], 5);
    }

    [Fact]
    public void S3201_int16_frames_are_scaled_to_one()
    {
        var data = new byte[4];
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(0), short.MinValue);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(2), 16384);

        var mono = new float[2];
        Spectrum.MixDown(data, SampleEncoding.Int16, 1, 2, silent: false, mono);
        Assert.Equal(-1f, mono[0], 5);
        Assert.Equal(0.5f, mono[1], 5);
    }

    [Theory]
    [InlineData(true, SampleEncoding.Float32)]
    [InlineData(false, SampleEncoding.Unsupported)]
    public void S3201_silent_packets_and_unknown_formats_are_zeros_that_still_take_time(bool silent, SampleEncoding encoding)
    {
        var data = Enumerable.Repeat((byte)0x7F, 64).ToArray();
        var mono = Enumerable.Repeat(9f, 8).ToArray();
        Assert.Equal(8, Spectrum.MixDown(data, encoding, 2, 8, silent, mono));
        Assert.All(mono, s => Assert.Equal(0f, s));
    }

    [Fact]
    public void S3201_a_short_buffer_is_silence_rather_than_an_overrun()
    {
        var mono = new float[4];
        Assert.Equal(4, Spectrum.MixDown(new byte[3], SampleEncoding.Float32, 2, 4, silent: false, mono));
        Assert.All(mono, s => Assert.Equal(0f, s));
    }

    [Fact]
    public void S3203_the_first_subscriber_starts_the_worker_and_more_only_count()
    {
        var lease = new CaptureLease();
        Assert.Equal(CaptureAction.Start, lease.Add());
        Assert.Equal(CaptureAction.None, lease.Add());
        Assert.Equal(CaptureAction.None, lease.Add());
        Assert.Equal(3, lease.Subscribers);
    }

    [Fact]
    public void S3203_the_worker_stops_only_with_the_last_subscriber()
    {
        var lease = new CaptureLease();
        lease.Add();
        lease.Add();

        Assert.Equal(CaptureAction.None, lease.Remove());
        Assert.True(lease.Running);
        Assert.Equal(CaptureAction.Stop, lease.Remove());
        Assert.False(lease.Running);
    }

    [Fact]
    public void S3203_a_remount_that_overlaps_old_and_new_never_stops_the_capture()
    {
        var lease = new CaptureLease();
        lease.Add();                                         // the old instance
        Assert.Equal(CaptureAction.None, lease.Add());       // the new one arrives first
        Assert.Equal(CaptureAction.None, lease.Remove());    // then the old one leaves
        Assert.True(lease.Running);
    }

    [Fact]
    public void S3203_a_dead_worker_is_restarted_by_the_next_subscriber()
    {
        var lease = new CaptureLease();
        lease.Add();
        lease.WorkerEnded();

        Assert.Equal(CaptureAction.Start, lease.Add());
        Assert.Equal(2, lease.Subscribers);
    }

    [Theory]
    [InlineData(true, false, Spectrum.FrameMs)]
    [InlineData(false, true, Spectrum.FrameMs)]
    [InlineData(true, true, Spectrum.FrameMs)]
    [InlineData(false, false, Spectrum.IdleFrameMs)]
    public void S3202_the_capture_drains_slower_only_once_the_bars_are_down_and_nothing_arrives(bool arrived, bool lit, int wait)
        => Assert.Equal(wait, Spectrum.FrameWait(arrived, lit));

    [Theory]
    [InlineData(480, false, true)]
    [InlineData(480, true, false)]
    [InlineData(0, false, false)]
    public void S6332_a_packet_flagged_silent_is_not_sound_so_the_capture_idles(int written, bool silent, bool heard)
    {
        Assert.Equal(heard, Spectrum.Heard(written, silent));

        // A stream held open in silence: once the bars are down, the idle wait.
        Assert.Equal(heard ? Spectrum.FrameMs : Spectrum.IdleFrameMs, Spectrum.FrameWait(Spectrum.Heard(written, silent), lit: false));
    }

    [Fact]
    public void S3202_the_idle_wait_stays_inside_the_capture_buffer()
        => Assert.True(Spectrum.IdleFrameMs < 200);

    [Fact]
    public void S3204_availability_is_unknown_until_the_worker_reports_and_told_once_per_change()
    {
        var lease = new CaptureLease();
        lease.Add();
        Assert.Null(lease.Available);

        Assert.True(lease.Report(false));
        Assert.False(lease.Report(false));
        Assert.False(lease.Available);
        Assert.True(lease.Report(true));
        Assert.True(lease.Available);
    }

    [Fact]
    public void S3204_a_new_worker_forgets_what_the_last_one_found()
    {
        var lease = new CaptureLease();
        lease.Add();
        lease.Report(false);
        lease.Remove();

        Assert.Null(lease.Available);
        Assert.False(lease.Report(false));   // no worker: nothing to tell
        lease.Add();
        Assert.Null(lease.Available);

        lease.Report(false);
        lease.WorkerEnded();
        Assert.Null(lease.Available);
    }

    [Fact]
    public void S3203_an_extra_release_never_goes_negative()
    {
        var lease = new CaptureLease();
        Assert.Equal(CaptureAction.None, lease.Remove());
        Assert.Equal(0, lease.Subscribers);
        Assert.Equal(CaptureAction.Start, lease.Add());
    }
}
