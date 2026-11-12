using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Links;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Widget.ExchangeHistory;

[assembly: ThemiaWidget("exchangeHistory", typeof(ExchangeHistoryModule))]

namespace Themia.Widget.ExchangeHistory;

/// <summary>[S-11155] One exchange rate's last 90 days as a chart, headerless by default. SPEC.md beside this file.</summary>
public sealed class ExchangeHistoryModule : IWidgetModule
{
    /// <summary>Its glyph: a chart (U+E9D2), the rate's old chart style's.</summary>
    internal static readonly string Glyph = char.ConvertFromUtf32(0xE9D2);

    /// <summary>[S-11156] The chart's band: its least, a new card's and its most.</summary>
    public const double BandMin = FamilyShape.IconHuge;

    public const double BandStart = 72;

    public const double BandMax = 200;

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("exchangeHistory"),
        ConfigType = typeof(ExchangeHistoryConfig),
        CreateDefault = () => new ExchangeHistoryConfig(),
        Json = ExchangeHistoryJsonContext.Default,
        Keywords = ["currency", "forex", "fx", "exchange rate", "chart", "history", "ecb"],
        Label = ExchangeHistoryStr.ExchangeHistory_Label.Local(),
        Description = ExchangeHistoryStr.ExchangeHistory_Desc.Local(),
        Glyph = Glyph,
        Sizing = _ => Sizing(),
        Traits = WidgetTraits.CanHaveHeader | WidgetTraits.StartsHeaderless,
        Links = LinkUse.Of([LinkTopicId.HoveredInstant], [LinkTopicId.HoveredInstant]),
        Add = new(AddFolderId.Markets, 0, 3),
    };

    /// <summary>[S-11156] The band under the card's top inset (headerless), to the sides and the foot: 200..480 wide, 48..200 tall.</summary>
    internal static WidgetSizing Sizing()
        => WidgetSizing.Range(new(200, FamilySpace.Inset + BandMin), new(280, FamilySpace.Inset + BandStart), new(480, FamilySpace.Inset + BandMax));

    /// <summary>[S-11157] Every option is on the properties menu: the pair, as the rate's.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        menu.Lines.AddRange(ExchangePairLines.Lines(menu, (ExchangeHistoryConfig)config, Glyph));
    }

    public FrameworkElement CreateView(IWidgetHost host) => new ExchangeHistoryView(host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ExchangeHistoryConfig))]
internal sealed partial class ExchangeHistoryJsonContext : JsonSerializerContext;
