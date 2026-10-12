using System.Text.Json.Serialization;
using Themia.Core.Widgets;

namespace Themia.Core.Model;

/// <summary>
/// [S-3200] [S-3210] An audio visualizer: the spectrum of whatever the PC plays, as a widget of its own,
/// in one of five designs, each its own kind.
/// <para>
/// Nothing to configure, and that is deliberate: the bars inside Music were an on/off switch and
/// nothing more, and the switch is now the act of placing the widget; the design is the choice of
/// widget. Its colour is the accent, its grain follows the tile ([S-3205]); the card's own
/// appearance rows cover the rest.
/// </para>
/// </summary>
public abstract class VisualizerConfig : WidgetConfig
{
    /// <summary>The design this kind draws: fixed by the kind, never stored.</summary>
    [JsonIgnore]
    public VisualizerStyle Style => VisualizerLayout.StyleOf(Kind)
        ?? throw new InvalidOperationException($"{Kind} is not a visualizer");
}

/// <summary>[S-3210] Bars standing on the bottom edge: the original design, so it keeps the original kind.</summary>
public sealed class AudioVisualizerConfig : VisualizerConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.AudioVisualizer;

    public override WidgetConfig Clone() => new AudioVisualizerConfig();
}

/// <summary>[S-3210] Bars symmetric around the centre line.</summary>
public sealed class VisualizerMirrorConfig : VisualizerConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.VisualizerMirror;

    public override WidgetConfig Clone() => new VisualizerMirrorConfig();
}

/// <summary>[S-3210] A smooth curve through the bands, the area under it filled.</summary>
public sealed class VisualizerWaveConfig : VisualizerConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.VisualizerWave;

    public override WidgetConfig Clone() => new VisualizerWaveConfig();
}

/// <summary>[S-3210] Columns of LED cells.</summary>
public sealed class VisualizerDotsConfig : VisualizerConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.VisualizerDots;

    public override WidgetConfig Clone() => new VisualizerDotsConfig();
}

/// <summary>[S-3210] Spokes around a ring.</summary>
public sealed class VisualizerRadialConfig : VisualizerConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.VisualizerRadial;

    public override WidgetConfig Clone() => new VisualizerRadialConfig();
}
