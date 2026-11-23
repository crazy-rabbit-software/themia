using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Links;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Devices;
using Themia.Sdk.Items;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Items;
using Themia.Widget.Battery;

[assembly: ThemiaWidget("battery", typeof(BatteryModule))]

namespace Themia.Widget.Battery;

/// <summary>[S-1180] [S-17182] This PC's battery as a device item: its charge, its state and time, its bar. SPEC.md beside this file.</summary>
public sealed class BatteryModule : IWidgetModule
{
    /// <summary>[S-2022] A click on the card opens Windows' Power & battery page.</summary>
    internal static CardOpen? Opens => CardOpen.Settings("batterysaver");

    /// <summary>[S-17182] The widest lines its item says, for its least width.</summary>
    private static readonly string[] WidestLines = ["Charging · Full in 10h 59m", "On battery · 10h 59m left"];

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("battery"),
        ConfigType = typeof(BatteryConfig),
        CreateDefault = () => new BatteryConfig(),
        Json = BatteryJsonContext.Default,
        Keywords = ["power", "charge", "laptop", "percent", "energy"],
        Label = BatteryStr.Widget_Battery_Label.Local(),
        Description = BatteryStr.Shell_Meters_BatteryDescription.Local(),
        Glyph = Glyphs.Battery,
        Sizing = c => ItemLayout.Face(DeviceItems.Shape(((BatteryConfig)c).HiddenParts), ItemLayout.WidestLine(WidestLines), ItemLayout.ReadingWidth),
        Traits = WidgetTraits.HasItems,
        Links = LinkUse.None,
        Add = new(AddFolderId.Devices, 0, 0),
    };

    /// <summary>[S-16672] What its item shows, the device family's one section.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
        => menu.Lines.Add(ItemMenuLines.Parts(menu, (BatteryConfig)config, DeviceItems.Parts, name: ItemMenuLines.DevicePartName));

    public FrameworkElement CreateView(IWidgetHost host) => new BatteryView(host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(BatteryConfig))]
internal sealed partial class BatteryJsonContext : JsonSerializerContext;
