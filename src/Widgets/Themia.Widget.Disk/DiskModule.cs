using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.App.Widgets.Meters;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.Disk;

[assembly: ThemiaWidget("disk", typeof(DiskModule))]

namespace Themia.Widget.Disk;

/// <summary>[S-1820] Disk activity: the drive's reads and writes together per second, over their history, or as two bars; the header names the drive ([S-1161]). SPEC.md beside this file.</summary>
public sealed class DiskModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("disk"),
        ConfigType = typeof(DiskConfig),
        CreateDefault = () => new DiskConfig(),
        Json = DiskJsonContext.Default,
        Keywords = ["storage", "ssd", "hdd", "drive", "space", "free space", "io"],
        Label = DiskStr.WidgetsA_Catalog_Disk_Label.Local(),
        Description = DiskStr.WidgetsA_Catalog_Disk_Description.Local(),
        Glyph = Glyphs.Disk,
        DefaultWidth = 200,
        DefaultHeight = 150,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.System, 0, 3),
    };

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => DiskSettings.Rows(rows.Rows, (DiskConfig)config, rows.Save);

    public FrameworkElement CreateView(IWidgetHost host) => new MeterView(host, new DiskMeter(host));
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(DiskConfig))]
internal sealed partial class DiskJsonContext : JsonSerializerContext;
