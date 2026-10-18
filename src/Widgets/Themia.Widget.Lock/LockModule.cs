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
using Themia.Widget.Lock;

[assembly: ThemiaWidget("lock", typeof(LockModule))]

namespace Themia.Widget.Lock;

/// <summary>[S-7800] The Lock power button: one action, the whole tile its button. SPEC.md beside this file.</summary>
public sealed class LockModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("lock"),
        ConfigType = typeof(LockConfig),
        CreateDefault = () => new LockConfig(),
        Json = LockJsonContext.Default,
        Keywords = ["power", "lock screen", "away", "security"],
        Label = LockStr.Power_Action_Lock.Local(),
        Description = LockStr.WidgetsC_Power_Desc_Lock.Local(),
        Glyph = Glyphs.Lock,
        DefaultWidth = 120,
        DefaultHeight = 120,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader | WidgetTraits.StartsHeaderless,
        Add = new(AddFolderId.PowerButtons, 0, 4),
    };

    public FrameworkElement CreateView(IWidgetHost host) => new PowerButtonView(Descriptor.Id, host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(LockConfig))]
internal sealed partial class LockJsonContext : JsonSerializerContext;
