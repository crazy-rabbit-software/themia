using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk;

namespace Themia.Core.Shell;

/// <summary>[S-2342] A section of the selection's page, in its order under the header card.</summary>
public enum SelectionSection
{
    /// <summary>What it shows: the style first with what that look draws under it, then the widget's own lines.</summary>
    Shows,

    /// <summary>Header: the title (one widget) and where the header stands.</summary>
    Header,

    /// <summary>Appearance: alignment, background, item background, item text, background image, scale.</summary>
    Appearance,
}

/// <summary>
/// [S-2340]..[S-2343] The selection's entry on the edit bar and its page, after Karpo's widget page
/// (karpo-app, docs/widgets.md §529, §575, §596): the entry is the kind's glyph and name, "2 × Clock"
/// for several of one kind, "3 widgets" for several kinds; the page a header card, then the sections
/// the selection shares. Pure: the page's rows are the properties' lines ([S-3719]), the host's to build.
/// </summary>
public static class SelectionPage
{
    /// <summary>[S-2341] The entry's words for <paramref name="offer"/>, the kinds named by <paramref name="name"/>; null while nothing is selected.</summary>
    public static string? Words(QuickSetOffer offer, Func<WidgetId, string> name)
    {
        ArgumentNullException.ThrowIfNull(offer);
        ArgumentNullException.ThrowIfNull(name);

        return offer switch
        {
            { Shows: false } => null,
            { Count: 1, TitleKind: { } kind } => name(kind),
            { CommonKind: { } kind } => Str.EditBar_Selection_Same.Format(("count", LocaleFormat.Integer(offer.Count)), ("kind", name(kind))),
            _ => Str.Shell_Views_WidgetCount.Plural(offer.Count),
        };
    }

    /// <summary>
    /// [S-2342] The sections under the header card: What it shows where the look has a style or the one
    /// widget has lines of its own (<paramref name="ownLines"/>), or what its look draws with
    /// (<paramref name="styleLines"/>); Header where every widget selected can have one ([S-3711]);
    /// Appearance always. Several widgets have no lines of their own: only what they share ([S-3702]).
    /// </summary>
    public static IReadOnlyList<SelectionSection> Sections(QuickSetOffer offer, bool ownLines, bool styleLines)
    {
        ArgumentNullException.ThrowIfNull(offer);
        if (!offer.Shows)
        {
            return [];
        }

        var sections = new List<SelectionSection>(3);
        if (offer.HasStyle || (offer.HasTitle && (ownLines || styleLines)))
        {
            sections.Add(SelectionSection.Shows);
        }

        if (offer.HasHeader)
        {
            sections.Add(SelectionSection.Header);
        }

        sections.Add(SelectionSection.Appearance);
        return sections;
    }

    /// <summary>[S-2342] A section's heading.</summary>
    public static string Heading(SelectionSection section) => section switch
    {
        SelectionSection.Shows => Str.EditBar_Page_Shows.Text(),
        SelectionSection.Header => Str.App_ConfigHeaderLabel.Text(),
        SelectionSection.Appearance => Str.Settings_Section_Appearance.Text(),
        _ => throw new ArgumentOutOfRangeException(nameof(section), section, "not a section"),
    };
}
