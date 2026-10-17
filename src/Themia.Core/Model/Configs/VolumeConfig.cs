using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Storage;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>
/// The master level of one output device, and whether this tile is allowed to move it.
/// [S-2730] The tile is the slider and nothing else, so there is nothing to show or hide.
/// <para>
/// Reading is the widget's job; setting is opt-out rather than opt-in, because a volume tile you
/// cannot drag is a strange thing to put on a desktop — but a surface that sits under every other
/// window can be scrolled or clicked across by accident, so the switch exists.
/// </para>
/// <para>
/// [S-1220] No refresh interval: the tile follows Core Audio's volume notifications, so there is no
/// poll for a setting to tune.
/// </para>
/// </summary>
public sealed class VolumeConfig : WidgetConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.Volume;

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("volume"),
        Kind = WidgetKind.Volume,
        ConfigType = typeof(VolumeConfig),
        CreateDefault = () => new VolumeConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["sound", "audio", "speaker", "mute", "loudness"],
        LabelId = Str.Widget_Volume_Label,
        DescriptionId = Str.WidgetsA_Catalog_Volume_Description,
        Glyph = Glyphs.Volume,
        DefaultWidth = 200,
        DefaultHeight = 150,
    };

    /// <summary>[S-1221] The output device's endpoint id, or null to follow Windows' default output.</summary>
    public string? DeviceId { get; set; }

    /// <summary>[S-1221] The chosen device's name when it was chosen, so an unplugged one is still named.</summary>
    public string? DeviceName { get; set; }

    /// <summary>Let the slider and the mute button write back to the system mixer.</summary>
    public bool AllowControl { get; set; } = true;

    public override WidgetConfig Clone() => new VolumeConfig
    {
        DeviceId = DeviceId,
        DeviceName = DeviceName,
        AllowControl = AllowControl,
    };

    /// <summary>A blank id is "follow the default", stored as null so there is one way to say it.</summary>
    public override IReadOnlyList<string> Repair()
    {
        if (DeviceId is not null && string.IsNullOrWhiteSpace(DeviceId))
        {
            DeviceId = null;
            DeviceName = null;
            return ["volume device id was blank; following the default output"];
        }

        return [];
    }
}
