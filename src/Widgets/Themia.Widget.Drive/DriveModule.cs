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
using Themia.Widget.Drive;

[assembly: ThemiaWidget("drive", typeof(DriveModule))]

namespace Themia.Widget.Drive;

/// <summary>[S-4670] One drive's card: its picture and name, the space line and the used share as a bar or a ring. SPEC.md beside this file.</summary>
public sealed class DriveModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("drive"),
        ConfigType = typeof(DriveConfig),
        CreateDefault = () => new DriveConfig(),
        Json = DriveJsonContext.Default,
        Keywords = ["disk", "storage", "free space", "capacity", "ssd", "hdd", "network drive", "mapped drive", "nas", "share"],
        Label = DriveStr.Drives_Drive_Label.Local(),
        Description = DriveStr.Drives_Drive_Desc.Local(),
        Glyph = Glyphs.Disk,
        DefaultWidth = 280,
        DefaultHeight = 160,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader | WidgetTraits.StartsHeaderless,
        Add = new(AddFolderId.Devices, 2, 0),
    };

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => DriveSettings.Rows(rows.Rows, (DriveConfig)config, rows.Save);

    public FrameworkElement CreateView(IWidgetHost host) => new DriveView(host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(DriveConfig))]
internal sealed partial class DriveJsonContext : JsonSerializerContext;
