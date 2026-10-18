using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.AudioVisualizer;

[assembly: ThemiaWidget("audioVisualizer", typeof(AudioVisualizerModule))]

namespace Themia.Widget.AudioVisualizer;

/// <summary>[S-3200] What the speakers play, as a spectrum in one of six styles: bars, mirrored, a wave, LEDs, a ring or a waveform. SPEC.md beside this file.</summary>
public sealed class AudioVisualizerModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("audioVisualizer"),
        ConfigType = typeof(AudioVisualizerConfig),
        CreateDefault = () => new AudioVisualizerConfig(),
        Json = AudioVisualizerJsonContext.Default,
        // [S-3216] The six former visualizer widgets' names and words find the one visualizer.
        Keywords =
        [
            "spectrum", "equalizer", "equaliser", "visualiser", "bars", "audio", "sound", "music", "mirrored",
            "symmetric", "wave", "waveform", "curve", "led", "dots", "vu meter", "circle", "radial", "ring",
            "oscilloscope", "line", "ecg",
        ],
        Label = AudioVisualizerStr.Styles_AudioVisualizer_Label.Local(),
        Description = AudioVisualizerStr.Styles_AudioVisualizer_Desc.Local(),
        Glyph = Glyphs.Visualizer,
        DefaultWidth = 320,
        DefaultHeight = 160,
        Traits = WidgetTraits.None,
        Add = new(AddFolderId.Media, 0, 1),
    };

    public FrameworkElement CreateView(IWidgetHost host) => new AudioVisualizerView(host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(AudioVisualizerConfig))]
internal sealed partial class AudioVisualizerJsonContext : JsonSerializerContext;
