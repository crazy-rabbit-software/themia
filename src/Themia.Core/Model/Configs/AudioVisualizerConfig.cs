using System.Text.Json.Serialization;

namespace Themia.Core.Model;

/// <summary>
/// [S-3200] The audio visualizer: the spectrum of whatever the PC plays, as a widget of its own.
/// <para>
/// Nothing to configure, and that is deliberate: the bars inside Music were an on/off switch and
/// nothing more, and the switch is now the act of placing the widget. Its colour is the accent, its
/// bar count follows the tile ([S-3205]); the card's own appearance rows cover the rest.
/// </para>
/// </summary>
public sealed class AudioVisualizerConfig : WidgetConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.AudioVisualizer;

    public override WidgetConfig Clone() => new AudioVisualizerConfig();
}
