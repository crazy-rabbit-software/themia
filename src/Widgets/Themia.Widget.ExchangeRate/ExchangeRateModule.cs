using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Links;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.ExchangeRate;

[assembly: ThemiaWidget("exchangeRate", typeof(ExchangeRateModule))]

namespace Themia.Widget.ExchangeRate;

/// <summary>[S-11150] The ECB's reference rate of one pair as a reading: the rate, its change, its day. SPEC.md beside this file.</summary>
public sealed class ExchangeRateModule : IWidgetModule
{
    /// <summary>Its glyph, the module's own (U+E9A1, two arrows round a coin; checked on a rendered sheet of SegoeIcons.ttf).</summary>
    internal static readonly string Glyph = char.ConvertFromUtf32(0xE9A1);

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("exchangeRate"),
        ConfigType = typeof(ExchangeRateConfig),
        CreateDefault = () => new ExchangeRateConfig(),
        Json = ExchangeRateJsonContext.Default,
        Keywords = ["currency", "forex", "fx", "exchange", "money", "euro", "dollar", "franc", "pound", "ecb", "conversion"],
        Label = ExchangeRateStr.Online_Rates_Label.Local(),
        Description = ExchangeRateStr.Online_Rates_Desc.Local(),
        Glyph = Glyph,
        Sizing = _ => RatesLayout.Sizing(),
        Traits = WidgetTraits.CanHaveHeader,
        Links = LinkUse.Of([], [LinkTopicId.HoveredInstant]),
        Add = new(AddFolderId.Markets, 0, 2),
    };

    /// <summary>[S-3719] Every option is on the properties menu: the widget has no settings page.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        menu.Lines.AddRange(ExchangePairLines.Lines(menu, (ExchangeRateConfig)config, Glyph));
    }

    public FrameworkElement CreateView(IWidgetHost host) => new ExchangeRateView(host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ExchangeRateConfig))]
internal sealed partial class ExchangeRateJsonContext : JsonSerializerContext;
