using System.Text.Json.Serialization.Metadata;
using Microsoft.UI.Xaml;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-14934] One kind of a text family as a module: its descriptor made from its <see cref="TextKind"/> (the one sizing,
/// <see cref="TextRules.Sizing(TextKind)"/>; a one-readout face, at home in its family's add-page folder, in the family's
/// order), its menu (the family's source lines: a place, when it hides) and its view (the family's, over the one face,
/// <c>TextView</c>). A family's kinds are sealed modules of this, one per id.
/// </summary>
public abstract class TextModule : IWidgetModule
{
    /// <param name="kind">The kind.</param>
    /// <param name="json">The family's source-generated JSON metadata.</param>
    /// <param name="folder">The family's add-page folder.</param>
    /// <param name="rank">Its place there: the family's order.</param>
    /// <param name="popular">Its place among Start's Popular, if it is one.</param>
    /// <param name="merges">[S-15022] The removed kinds a placed one of which it stands in for, in place; none for most.</param>
    protected TextModule(TextKind kind, IJsonTypeInfoResolver json, AddFolderId folder, int rank, int? popular = null, IReadOnlyList<MergeFrom>? merges = null)
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
            Sizing = _ => TextRules.Sizing(kind),
            Traits = WidgetTraits.HasItems | WidgetTraits.OneReadout,
            Add = new(folder, 0, rank, popular),
            Merges = merges ?? [],
        };
    }

    /// <summary>The kind this module is: its data (which text, its lead, what a click opens, [S-14935]).</summary>
    public TextKind Kind { get; }

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; }

    /// <summary>[S-14934] Its options, all on its properties menu ([S-3719]): the family's source lines; what it shows is its kind.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);
        SourceLines(menu, config);
    }

    /// <summary>The lines of what the text is about (a place) or when it shows; none by default.</summary>
    protected virtual void SourceLines(WidgetMenuLines menu, WidgetConfig config)
    {
    }

    public abstract FrameworkElement CreateView(IWidgetHost host);
}
