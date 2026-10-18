using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>[S-1910] Turn off the display.</summary>
public sealed class ScreenOffConfig : PowerButtonConfig
{
    /// <summary>The action it stands for.</summary>
    [JsonIgnore]
    public override PowerAction Action => PowerAction.ScreenOff;

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("screenOff"),
        ConfigType = typeof(ScreenOffConfig),
        CreateDefault = () => new ScreenOffConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["power", "monitor off", "screen off", "blank screen", "display"],
        Label = Str.Power_Action_ScreenOff.Local(),
        Description = Str.WidgetsC_Power_Desc_ScreenOff.Local(),
        Glyph = Glyphs.Display,
        DefaultWidth = 120,
        DefaultHeight = 120,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader | WidgetTraits.StartsHeaderless,
        Add = new(AddFolderId.PowerButtons, 0, 6),
    };

    public override WidgetConfig Clone() => new ScreenOffConfig { Style = Style };
}
