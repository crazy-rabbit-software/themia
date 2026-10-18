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
using Themia.Widget.EnergySaverToggle;

[assembly: ThemiaWidget("energySaverToggle", typeof(EnergySaverToggleModule))]

namespace Themia.Widget.EnergySaverToggle;

/// <summary>[S-8500] The Energy saver quick-setting toggle: the whole tile its switch. SPEC.md beside this file.</summary>
public sealed class EnergySaverToggleModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("energySaverToggle"),
        ConfigType = typeof(EnergySaverToggleConfig),
        CreateDefault = () => new EnergySaverToggleConfig(),
        Json = EnergySaverToggleJsonContext.Default,
        Keywords = ["quick settings", "toggle", "battery saver", "power saving", "battery", "eco"],
        Label = EnergySaverToggleStr.Toggles_EnergySaver_Label.Local(),
        Description = EnergySaverToggleStr.Toggles_EnergySaver_Desc.Local(),
        Glyph = Glyphs.EnergySaver,
        DefaultWidth = 120,
        DefaultHeight = 120,
        Traits = WidgetTraits.HasItems,
        Add = new(AddFolderId.QuickSettings, 3, 0),
    };

    public FrameworkElement CreateView(IWidgetHost host) => new QuickToggleView(Descriptor.Id, host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(EnergySaverToggleConfig))]
internal sealed partial class EnergySaverToggleJsonContext : JsonSerializerContext;
