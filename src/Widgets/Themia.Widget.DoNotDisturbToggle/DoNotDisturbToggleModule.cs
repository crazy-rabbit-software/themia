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
using Themia.Widget.DoNotDisturbToggle;

[assembly: ThemiaWidget("doNotDisturbToggle", typeof(DoNotDisturbToggleModule))]

namespace Themia.Widget.DoNotDisturbToggle;

/// <summary>[S-8800] The Do not disturb quick-setting toggle: the whole tile its switch. SPEC.md beside this file.</summary>
public sealed class DoNotDisturbToggleModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("doNotDisturbToggle"),
        ConfigType = typeof(DoNotDisturbToggleConfig),
        CreateDefault = () => new DoNotDisturbToggleConfig(),
        Json = DoNotDisturbToggleJsonContext.Default,
        Keywords = ["quick settings", "toggle", "dnd", "focus assist", "quiet hours", "notifications", "silence"],
        Label = DoNotDisturbToggleStr.Toggles_DoNotDisturb_Label.Local(),
        Description = DoNotDisturbToggleStr.Toggles_DoNotDisturb_Desc.Local(),
        Glyph = Glyphs.QuietHours,
        DefaultWidth = 120,
        DefaultHeight = 120,
        Traits = WidgetTraits.HasItems,
        Add = new(AddFolderId.QuickSettings, 3, 1),
    };

    public FrameworkElement CreateView(IWidgetHost host) => new QuickToggleView(Descriptor.Id, host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(DoNotDisturbToggleConfig))]
internal sealed partial class DoNotDisturbToggleJsonContext : JsonSerializerContext;
