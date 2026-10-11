#if DEBUG
using Themia.App.Shell.Gallery;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-3207]): a render cannot capture the PC's sound, so "playing" draws a still,
/// synthetic spectrum (bass high, falling towards the treble with some unevenness) and
/// "unavailable" the sentence a device the capture cannot reach shows.
/// </summary>
internal sealed partial class AudioVisualizerView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["playing", "unavailable"];

    public Task ShowPreviewStateAsync(string state)
    {
        switch (state)
        {
            case "playing":
                var bands = new float[Spectrum.Bands];
                for (var i = 0; i < bands.Length; i++)
                {
                    var t = i / (double)(bands.Length - 1);
                    var shape = 0.88 - (0.55 * t) + (0.14 * Math.Sin(i * 1.7)) + (0.06 * Math.Cos(i * 4.3));
                    bands[i] = (float)Math.Clamp(shape, 0.05, 1);
                }

                ShowAvailability(available: true);
                _bars.SetLevels(bands);
                break;

            case "unavailable":
                ShowAvailability(available: false);
                break;
        }

        return Task.CompletedTask;
    }
}
#endif
