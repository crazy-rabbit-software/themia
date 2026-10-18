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
using Themia.Widget.Sleep;

[assembly: ThemiaWidget("sleep", typeof(SleepModule))]

namespace Themia.Widget.Sleep;

/// <summary>[S-7600] The Sleep power button: one action, the whole tile its button. SPEC.md beside this file.</summary>
public sealed class SleepModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("sleep"),
        ConfigType = typeof(SleepConfig),
        CreateDefault = () => new SleepConfig(),
        Json = SleepJsonContext.Default,
        Keywords = ["power", "standby", "suspend"],
        Label = SleepStr.Power_Action_Sleep.Local(),
        Description = SleepStr.WidgetsC_Power_Desc_Sleep.Local(),
        Glyph = Glyphs.Moon,
        DefaultWidth = 120,
        DefaultHeight = 120,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader | WidgetTraits.StartsHeaderless,
        Add = new(AddFolderId.PowerButtons, 0, 2),
    };

    public FrameworkElement CreateView(IWidgetHost host) => new PowerButtonView(Descriptor.Id, host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(SleepConfig))]
internal sealed partial class SleepJsonContext : JsonSerializerContext;
