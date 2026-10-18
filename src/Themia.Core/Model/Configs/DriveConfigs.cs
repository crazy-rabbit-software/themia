using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>[S-4660] The removable devices: no options, so no settings page ([S-1750]).</summary>
public sealed class RemovableDrivesConfig : WidgetConfig
{

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("removableDrives"),
        ConfigType = typeof(RemovableDrivesConfig),
        CreateDefault = () => new RemovableDrivesConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["usb", "usb stick", "flash drive", "sd card", "memory card", "external disk", "eject", "safely remove"],
        Label = Str.Drives_Removable_Label.Local(),
        Description = Str.Drives_Removable_Desc.Local(),
        Glyph = Glyphs.Usb,
        DefaultWidth = 300,
        DefaultHeight = 200,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.Devices, 2, 1),
    };

    public override WidgetConfig Clone() => new RemovableDrivesConfig();
}
