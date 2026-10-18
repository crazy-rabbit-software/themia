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
using Themia.Widget.RotationLockToggle;

[assembly: ThemiaWidget("rotationLockToggle", typeof(RotationLockToggleModule))]

namespace Themia.Widget.RotationLockToggle;

/// <summary>[S-8700] The Rotation lock quick-setting toggle: the whole tile its switch. SPEC.md beside this file.</summary>
public sealed class RotationLockToggleModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("rotationLockToggle"),
        ConfigType = typeof(RotationLockToggleConfig),
        CreateDefault = () => new RotationLockToggleConfig(),
        Json = RotationLockToggleJsonContext.Default,
        Keywords = ["quick settings", "toggle", "auto rotate", "screen rotation", "tablet", "orientation"],
        Label = RotationLockToggleStr.Toggles_RotationLock_Label.Local(),
        Description = RotationLockToggleStr.Toggles_RotationLock_Desc.Local(),
        Glyph = Glyphs.RotationLock,
        DefaultWidth = 120,
        DefaultHeight = 120,
        Traits = WidgetTraits.HasItems,
        Add = new(AddFolderId.QuickSettings, 2, 2),
    };

    public FrameworkElement CreateView(IWidgetHost host) => new QuickToggleView(Descriptor.Id, host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(RotationLockToggleConfig))]
internal sealed partial class RotationLockToggleJsonContext : JsonSerializerContext;
