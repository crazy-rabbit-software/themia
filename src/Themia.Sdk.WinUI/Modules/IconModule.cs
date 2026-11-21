using System.Text.Json.Serialization.Metadata;
using Microsoft.UI.Xaml;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-15003] One kind of an icon family as a module: its descriptor made from its <see cref="IconKind"/> (the one sizing,
/// <see cref="IconRules.Sizing"/>; a one-readout face with its one item, at home in its family's add-page folder, in the
/// family's order), its menu (the family's lines: a place, when it hides, which icon) and its view (the family's, over the
/// one face, <c>IconView</c>). A family's kinds are sealed modules of this, one per id.
/// </summary>
public abstract class IconModule : IWidgetModule
{
    /// <param name="kind">The kind.</param>
    /// <param name="json">The family's source-generated JSON metadata.</param>
    /// <param name="folder">The family's add-page folder.</param>
    /// <param name="rank">Its place there: the family's order.</param>
    /// <param name="popular">Its place among Start's Popular, if it is one.</param>
    /// <param name="merges">[S-15022] The removed kinds a placed one of which it stands in for, in place; none for most.</param>
    protected IconModule(IconKind kind, IJsonTypeInfoResolver json, AddFolderId folder, int rank, int? popular = null, IReadOnlyList<MergeFrom>? merges = null)
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
            Sizing = _ => IconRules.Sizing(),
            Traits = WidgetTraits.HasItems | WidgetTraits.OneReadout,
            Add = new(folder, 0, rank, popular),
            Merges = merges ?? [],
        };
    }

    /// <summary>The kind this module is: its data (its glyph, what a click opens, [S-15004]).</summary>
    public IconKind Kind { get; }

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; }

    /// <summary>[S-15003] Its options, all on its properties menu ([S-3719]): the family's lines; what it shows is its kind.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);
        SourceLines(menu, config);
    }

    /// <summary>The lines of what the icon is (which icon), what it is about (a place) or when it shows; none by default.</summary>
    protected virtual void SourceLines(WidgetMenuLines menu, WidgetConfig config)
    {
    }

    public abstract FrameworkElement CreateView(IWidgetHost host);
}
