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
using Themia.Widget.ExchangeRates;

[assembly: ThemiaWidget("exchangeRates", typeof(ExchangeRatesModule))]

namespace Themia.Widget.ExchangeRates;

/// <summary>[S-5140] The ECB's reference rate of one pair: the rate as the readout, over its 90 days in the chart style. SPEC.md beside this file.</summary>
public sealed class ExchangeRatesModule : IWidgetModule
{
    /// <summary>Its glyph, the module's own (U+E9A1, two arrows round a coin; checked on a rendered sheet of SegoeIcons.ttf).</summary>
    internal static readonly string Glyph = char.ConvertFromUtf32(0xE9A1);

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("exchangeRates"),
        ConfigType = typeof(ExchangeRatesConfig),
        CreateDefault = () => new ExchangeRatesConfig(),
        Json = ExchangeRatesJsonContext.Default,
        Keywords = ["currency", "forex", "fx", "exchange", "money", "euro", "dollar", "franc", "pound", "ecb", "conversion"],
        Label = ExchangeRatesStr.Online_Rates_Label.Local(),
        Description = ExchangeRatesStr.Online_Rates_Desc.Local(),
        Glyph = Glyph,
        DefaultWidth = 300,
        DefaultHeight = 220,
        Traits = WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.Markets, 0, 1),
    };

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => ExchangeRatesSettings.Rows(rows.Rows, (ExchangeRatesConfig)config, rows.Save);

    public FrameworkElement CreateView(IWidgetHost host) => new ExchangeRatesView(host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ExchangeRatesConfig))]
internal sealed partial class ExchangeRatesJsonContext : JsonSerializerContext;
