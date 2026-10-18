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
using Themia.Widget.Hibernate;

[assembly: ThemiaWidget("hibernate", typeof(HibernateModule))]

namespace Themia.Widget.Hibernate;

/// <summary>[S-7700] The Hibernate power button: one action, the whole tile its button. SPEC.md beside this file.</summary>
public sealed class HibernateModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("hibernate"),
        ConfigType = typeof(HibernateConfig),
        CreateDefault = () => new HibernateConfig(),
        Json = HibernateJsonContext.Default,
        Keywords = ["power", "suspend to disk", "deep sleep"],
        Label = HibernateStr.Power_Action_Hibernate.Local(),
        Description = HibernateStr.WidgetsC_Power_Desc_Hibernate.Local(),
        Glyph = Glyphs.Hibernate,
        DefaultWidth = 120,
        DefaultHeight = 120,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader | WidgetTraits.StartsHeaderless,
        Add = new(AddFolderId.PowerButtons, 0, 3),
    };

    public FrameworkElement CreateView(IWidgetHost host) => new PowerButtonView(Descriptor.Id, host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(HibernateConfig))]
internal sealed partial class HibernateJsonContext : JsonSerializerContext;
