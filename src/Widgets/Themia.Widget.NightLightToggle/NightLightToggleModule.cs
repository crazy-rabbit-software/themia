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
using Themia.Widget.NightLightToggle;

[assembly: ThemiaWidget("nightLightToggle", typeof(NightLightToggleModule))]

namespace Themia.Widget.NightLightToggle;

/// <summary>[S-8600] The Night light quick-setting toggle: the whole tile its switch. SPEC.md beside this file.</summary>
public sealed class NightLightToggleModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("nightLightToggle"),
        ConfigType = typeof(NightLightToggleConfig),
        CreateDefault = () => new NightLightToggleConfig(),
        Json = NightLightToggleJsonContext.Default,
        Keywords = ["quick settings", "toggle", "blue light", "warm screen", "night mode", "eye strain"],
        Label = NightLightToggleStr.Toggles_NightLight_Label.Local(),
        Description = NightLightToggleStr.Toggles_NightLight_Desc.Local(),
        Glyph = Glyphs.NightLight,
        DefaultWidth = 120,
        DefaultHeight = 120,
        Traits = WidgetTraits.HasItems,
        Add = new(AddFolderId.QuickSettings, 2, 0),
    };

    public FrameworkElement CreateView(IWidgetHost host) => new QuickToggleView(Descriptor.Id, host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(NightLightToggleConfig))]
internal sealed partial class NightLightToggleJsonContext : JsonSerializerContext;
