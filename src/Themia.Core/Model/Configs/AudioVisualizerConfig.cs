using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>
/// [S-3216] The audio visualizer's styles: the same capture, six drawings. The first is the
/// default; an unknown stored name is repaired to it ([S-6004]).
/// </summary>
[JsonConverter(typeof(StyleJsonConverter<AudioVisualizerStyle>))]
public enum AudioVisualizerStyle
{
    /// <summary>Bars standing on the bottom edge.</summary>
    Bars = 0,

    /// <summary>Bars symmetric around the centre line.</summary>
    Mirrored = 1,

    /// <summary>A smooth curve through the bands, the area under it filled.</summary>
    Wave = 2,

    /// <summary>Columns of LED cells, lit from the bottom.</summary>
    Led = 3,

    /// <summary>Spokes around a ring, the spectrum mirrored left and right.</summary>
    Radial = 4,

    /// <summary>[S-3212] The waveform itself, a line moving about the centre: an oscilloscope, not a spectrum.</summary>
    Waveform = 5,
}

/// <summary>
/// [S-3200] [S-3216] The audio visualizer: the spectrum (or the waveform) of whatever the PC plays,
/// in one of six styles.
/// <para>
/// Nothing else to configure, and that is deliberate: the bars inside Music were an on/off switch
/// and nothing more, and the switch is now the act of placing the widget. Its colour is the
/// accent, its grain follows the tile ([S-3205]); the card's own appearance rows cover the rest.
/// </para>
/// </summary>
public sealed class AudioVisualizerConfig : WidgetConfig, IStyled
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.AudioVisualizer;

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("audioVisualizer"),
        Kind = WidgetKind.AudioVisualizer,
        ConfigType = typeof(AudioVisualizerConfig),
        CreateDefault = () => new AudioVisualizerConfig(),
        Json = WidgetConfigJsonContext.Default,
        LabelId = Str.Styles_AudioVisualizer_Label,
        DescriptionId = Str.Styles_AudioVisualizer_Desc,
        Glyph = Glyphs.Visualizer,
        DefaultWidth = 320,
        DefaultHeight = 160,
    };

    /// <summary>[S-3216] The drawing; what were six widget kinds.</summary>
    public AudioVisualizerStyle Style { get; set; } = AudioVisualizerStyle.Bars;

    private static readonly StyleOption[] VisualizerStyles =
    [
        StyleOption.Of(AudioVisualizerStyle.Bars, Str.Styles_AudioVisualizer_Bars, 0xE7F3),
        StyleOption.Of(AudioVisualizerStyle.Mirrored, Str.Styles_AudioVisualizer_Mirrored, 0xF4C3),
        StyleOption.Of(AudioVisualizerStyle.Wave, Str.Styles_AudioVisualizer_Wave, 0xE9D9),
        StyleOption.Of(AudioVisualizerStyle.Led, Str.Styles_AudioVisualizer_Led, 0xF232),
        StyleOption.Of(AudioVisualizerStyle.Radial, Str.Styles_AudioVisualizer_Radial, 0xE706),
        StyleOption.Of(AudioVisualizerStyle.Waveform, Str.Styles_AudioVisualizer_Waveform, 0xE95E),
    ];

    IReadOnlyList<StyleOption> IStyled.Styles => VisualizerStyles;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (AudioVisualizerStyle)value;
    }

    public override WidgetConfig Clone() => new AudioVisualizerConfig { Style = Style };
}
