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
using Themia.Widget.AirplaneModeToggle;

[assembly: ThemiaWidget("airplaneModeToggle", typeof(AirplaneModeToggleModule))]

namespace Themia.Widget.AirplaneModeToggle;

/// <summary>[S-8400] The Airplane mode quick-setting toggle: the whole tile its switch. SPEC.md beside this file.</summary>
public sealed class AirplaneModeToggleModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("airplaneModeToggle"),
        ConfigType = typeof(AirplaneModeToggleConfig),
        CreateDefault = () => new AirplaneModeToggleConfig(),
        Json = AirplaneModeToggleJsonContext.Default,
        Keywords = ["quick settings", "toggle", "flight mode", "flight", "radios off", "offline"],
        Label = AirplaneModeToggleStr.Toggles_AirplaneMode_Label.Local(),
        Description = AirplaneModeToggleStr.Toggles_AirplaneMode_Desc.Local(),
        Glyph = Glyphs.Airplane,
        DefaultWidth = 120,
        DefaultHeight = 120,
        Traits = WidgetTraits.HasItems,
        Add = new(AddFolderId.QuickSettings, 0, 2),
    };

    public FrameworkElement CreateView(IWidgetHost host) => new QuickToggleView(Descriptor.Id, host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(AirplaneModeToggleConfig))]
internal sealed partial class AirplaneModeToggleJsonContext : JsonSerializerContext;
