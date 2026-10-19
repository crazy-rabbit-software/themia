using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.Quote;

[assembly: ThemiaWidget("quote", typeof(QuoteModule))]

namespace Themia.Widget.Quote;

/// <summary>[S-1495] The quote: a line worth rereading, from the user's own list, on a rotation. SPEC.md beside this file.</summary>
public sealed class QuoteModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("quote"),
        ConfigType = typeof(QuoteConfig),
        CreateDefault = () => new QuoteConfig(),
        Json = QuoteJsonContext.Default,
        Keywords = ["inspiration", "daily quote", "saying", "motivation"],
        Label = QuoteStr.WidgetsA_Catalog_Quote_Label.Local(),
        Description = QuoteStr.WidgetsA_Catalog_Quote_Description.Local(),
        Glyph = Glyphs.Quote,
        DefaultWidth = 320,
        DefaultHeight = 180,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.Notes, 1, 1),
    };

    /// <summary>[S-3719] Every option is on the properties menu: the widget has no settings page.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
        => QuoteMenu.Lines(menu, (QuoteConfig)config);

    public FrameworkElement CreateView(IWidgetHost host) => new QuoteView();
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(QuoteConfig))]
internal sealed partial class QuoteJsonContext : JsonSerializerContext;
