using System.Text.Json.Serialization.Metadata;
using Microsoft.UI.Xaml;
using Themia.Core.Links;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-14604] One kind of a single-metric family as a module: its descriptor made from its <see cref="MetricKind"/> (the
/// one sizing, <see cref="MetricRules.Sizing"/>; at home in its family's add-page folder, in the family's order), its
/// menu (the family's source line, then the trend's, [S-14603]) and its view (the family's, over the base face). A
/// family's kinds are sealed modules of this, one per id.
/// </summary>
public abstract class MetricModule : IWidgetModule
{
    /// <param name="kind">The kind.</param>
    /// <param name="json">The family's source-generated JSON metadata.</param>
    /// <param name="folder">The family's add-page folder.</param>
    /// <param name="rank">Its place there: the family's order.</param>
    /// <param name="links">What it takes part in on its view's links (a drive's card).</param>
    protected MetricModule(MetricKind kind, IJsonTypeInfoResolver json, AddFolderId folder, int rank, LinkUse? links = null)
    {
        Kind = kind ?? throw new ArgumentNullException(nameof(kind));
        Descriptor = new()
        {
            Id = new(kind.Id),
            ConfigType = kind.ConfigType,
            CreateDefault = kind.Create,
            Json = json,
            Keywords = kind.Keywords,
            Label = kind.Label,
            Description = kind.Description,
            Glyph = kind.Glyph,
            Sizing = MetricRules.SizingOf,
            Traits = WidgetTraits.HasItems,
            Links = links ?? LinkUse.None,
            Add = new(folder, 0, rank),
            Split = kind.Split,
        };
    }

    /// <summary>The kind this module is.</summary>
    protected MetricKind Kind { get; }

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; }

    /// <summary>[S-14603] Its options, all on its properties menu ([S-3719]): the family's source line, then the trend's.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);
        SourceLines(menu, config);
        if (config is IMetricTrend trend)
        {
            MetricMenuLines.Add(menu, trend);
        }
    }

    /// <summary>The line of what the metric is about (a place, a drive, an adapter, a host); none by default.</summary>
    protected virtual void SourceLines(WidgetMenuLines menu, WidgetConfig config)
    {
    }

    public abstract FrameworkElement CreateView(IWidgetHost host);
}
