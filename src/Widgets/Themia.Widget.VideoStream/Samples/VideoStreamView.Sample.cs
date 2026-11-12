using System.Runtime.InteropServices.WindowsRuntime;
using Themia.App.Services.Web;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Windows.Graphics.Imaging;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11850] [S-1314] The stream's sample: no camera is ever opened, so it is a still: a made-up frame (a
/// sky-to-ground gradient drawn here, no file, no address reached) with the hover strip as the pointer
/// shows it and, at XL, the status chip. The address is a placeholder nothing connects to.
/// </summary>
internal sealed partial class VideoStreamView : IWidgetSample
{
    private const string PreviewAddress = "http://camera.local:8080/video";

    public async Task ShowSampleAsync()
    {
        ShowStill(PreviewAddress, paused: false, StreamPhase.Live, reconnects: 0);

        // 1280×720 from an MJPEG camera at 12 frames a second, the newest "now": stamped ahead of the
        // view's clock, so the still says "now" however long the picture takes to be taken.
        var now = _clock.Elapsed + TimeSpan.FromMinutes(10);
        for (var i = 11; i >= 0; i--)
        {
            _rate.Add(now - TimeSpan.FromSeconds(i / 12.0));
        }

        _facts = new FrameFacts(1280, 720, StreamTransport.Mjpeg);
        _transport = StreamTransport.Mjpeg;
        _hovered = true;

        if (_pictureSource is { } source)
        {
            await source.SetBitmapAsync(Frame(640, 360));
        }

        Render();
    }

    /// <summary>Stop any session and stand still at <paramref name="phase"/> for <paramref name="url"/>; no frame is held.</summary>
    private void ShowStill(string url, bool paused, StreamPhase phase, int reconnects)
    {
        _still = true;
        StopSession();
        _config = new VideoStreamConfig
        {
            Url = url,
            Paused = paused,
            ShowStatus = true,
        };
        PublishTitle();

        _phase = phase;
        _reconnects = reconnects;
        _detail = null;
        ForgetFrames();
    }

    /// <summary>A still that reads as a camera's picture: a sky over a horizon, no file and no network.</summary>
    private static SoftwareBitmap Frame(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            var t = y / (double)(height - 1);
            var (r, g, b) = t < 0.62
                ? (Mix(0x6F, 0xB8, t / 0.62), Mix(0x9F, 0xD4, t / 0.62), Mix(0xD8, 0xEE, t / 0.62))
                : (Mix(0x5A, 0x2E, (t - 0.62) / 0.38), Mix(0x7A, 0x48, (t - 0.62) / 0.38), Mix(0x4A, 0x2A, (t - 0.62) / 0.38));
            for (var x = 0; x < width; x++)
            {
                var at = ((y * width) + x) * 4;
                pixels[at] = b;
                pixels[at + 1] = g;
                pixels[at + 2] = r;
                pixels[at + 3] = 0xFF;
            }
        }

        var bitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, width, height, BitmapAlphaMode.Premultiplied);
        bitmap.CopyFromBuffer(pixels.AsBuffer());
        return bitmap;
    }

    private static byte Mix(int from, int to, double t) => (byte)Math.Round(from + ((to - from) * Math.Clamp(t, 0, 1)));
}
