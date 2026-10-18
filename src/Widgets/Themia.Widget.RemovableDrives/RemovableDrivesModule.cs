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
using Themia.Widget.RemovableDrives;

[assembly: ThemiaWidget("removableDrives", typeof(RemovableDrivesModule))]

namespace Themia.Widget.RemovableDrives;

/// <summary>[S-4660] A row per USB stick, card, disc or external disk plugged in, each with its Eject. SPEC.md beside this file.</summary>
public sealed class RemovableDrivesModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("removableDrives"),
        ConfigType = typeof(RemovableDrivesConfig),
        CreateDefault = () => new RemovableDrivesConfig(),
        Json = RemovableDrivesJsonContext.Default,
        Keywords = ["usb", "usb stick", "flash drive", "sd card", "memory card", "external disk", "eject", "safely remove"],
        Label = RemovableDrivesStr.Drives_Removable_Label.Local(),
        Description = RemovableDrivesStr.Drives_Removable_Desc.Local(),
        Glyph = Glyphs.Usb,
        DefaultWidth = 300,
        DefaultHeight = 200,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.Devices, 2, 1),
    };

    public FrameworkElement CreateView(IWidgetHost host) => new RemovableDrivesView(host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(RemovableDrivesConfig))]
internal sealed partial class RemovableDrivesJsonContext : JsonSerializerContext;
