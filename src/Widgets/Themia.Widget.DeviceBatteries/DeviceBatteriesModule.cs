using Themia.Sdk.Kit;
using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Shell;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.DeviceBatteries;

[assembly: ThemiaWidget("deviceBatteries", typeof(DeviceBatteriesModule))]

namespace Themia.Widget.DeviceBatteries;

/// <summary>[S-10000] Device batteries: every connected peripheral Windows reports a battery for, lowest first. SPEC.md beside this file.</summary>
public sealed class DeviceBatteriesModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("deviceBatteries"),
        ConfigType = typeof(DeviceBatteriesConfig),
        CreateDefault = () => new DeviceBatteriesConfig(),
        Json = DeviceBatteriesJsonContext.Default,
        Keywords = ["battery", "batteries", "mouse", "keyboard", "headphones", "controller", "bluetooth", "peripherals", "charge"],
        Label = DeviceBatteriesStr.DeviceBatteries_Label.Local(),
        Description = DeviceBatteriesStr.DeviceBatteries_Desc.Local(),
        Glyph = Glyphs.Devices,
        Sizing = c => KitSizing.Sizing(KitSizing.Choice((DeviceBatteriesConfig)c), addFooter: false),
        SizeSets = (c, s) => KitSizing.SizeSets(c, s, KitSizing.Choice((DeviceBatteriesConfig)c), addFooter: false),
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.Devices, 1, 2),
    };

    /// <summary>[S-10006] Its two options are on the properties menu: the widget has no settings page.</summary>
    public void MenuLines(WidgetMenuLines menu, Themia.Core.Model.WidgetConfig config)
        => DeviceBatteriesMenu.Lines(menu, (DeviceBatteriesConfig)config);

    public FrameworkElement CreateView(IWidgetHost host) => new DeviceBatteriesView(host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(DeviceBatteriesConfig))]
internal sealed partial class DeviceBatteriesJsonContext : JsonSerializerContext;
