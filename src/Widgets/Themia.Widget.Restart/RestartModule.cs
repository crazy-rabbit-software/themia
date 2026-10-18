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
using Themia.Widget.Restart;

[assembly: ThemiaWidget("restart", typeof(RestartModule))]

namespace Themia.Widget.Restart;

/// <summary>[S-7500] The Restart power button: one action, the whole tile its button. SPEC.md beside this file.</summary>
public sealed class RestartModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("restart"),
        ConfigType = typeof(RestartConfig),
        CreateDefault = () => new RestartConfig(),
        Json = RestartJsonContext.Default,
        Keywords = ["power", "reboot", "restart", "reset"],
        Label = RestartStr.Power_Action_Restart.Local(),
        Description = RestartStr.WidgetsC_Power_Desc_Restart.Local(),
        Glyph = Glyphs.Reset,
        DefaultWidth = 120,
        DefaultHeight = 120,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader | WidgetTraits.StartsHeaderless,
        Add = new(AddFolderId.PowerButtons, 0, 1),
    };

    public FrameworkElement CreateView(IWidgetHost host) => new PowerButtonView(Descriptor.Id, host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(RestartConfig))]
internal sealed partial class RestartJsonContext : JsonSerializerContext;
