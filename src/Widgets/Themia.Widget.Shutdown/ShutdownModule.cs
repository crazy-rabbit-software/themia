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
using Themia.Widget.Shutdown;

[assembly: ThemiaWidget("shutdown", typeof(ShutdownModule))]

namespace Themia.Widget.Shutdown;

/// <summary>[S-7400] The Shutdown power button: one action, the whole tile its button. SPEC.md beside this file.</summary>
public sealed class ShutdownModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("shutdown"),
        ConfigType = typeof(ShutdownConfig),
        CreateDefault = () => new ShutdownConfig(),
        Json = ShutdownJsonContext.Default,
        Keywords = ["power", "shutdown", "power off", "turn off", "switch off"],
        Label = ShutdownStr.Power_Action_Shutdown.Local(),
        Description = ShutdownStr.WidgetsC_Power_Desc_Shutdown.Local(),
        Glyph = Glyphs.Power,
        DefaultWidth = 120,
        DefaultHeight = 120,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader | WidgetTraits.StartsHeaderless,
        Add = new(AddFolderId.PowerButtons, 0, 0),
    };

    public FrameworkElement CreateView(IWidgetHost host) => new PowerButtonView(Descriptor.Id, host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ShutdownConfig))]
internal sealed partial class ShutdownJsonContext : JsonSerializerContext;
