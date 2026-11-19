using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk;

namespace Themia.Core.Shell;

/// <summary>[S-2342] [S-11754] A section of the selection's page, in its order under the header card.</summary>
public enum SelectionSection
{
    /// <summary>What it shows: the style first with what that look draws under it, then the widget's own lines.</summary>
    Shows,

    /// <summary>[S-11751] For all of them: every setting two or more of the widgets share, once.</summary>
    Shared,

    /// <summary>Appearance: alignment, background, item background, item text, background image, scale.</summary>
    Appearance,
}

/// <summary>
/// [S-2345] The selection page's tabs under its header card, Karpo's tab strip (§592: WinUI's SelectorBar): what
/// the widgets show and hold, and how they look.
/// </summary>
public enum SelectionTab
{
    /// <summary>Settings: What it shows, For all of them.</summary>
    Settings,

    /// <summary>Appearance: the Appearance section alone.</summary>
    Appearance,
}

/// <summary>[S-11754] What is selected, as the page is built for it.</summary>
public enum SelectionKind
{
    /// <summary>One lone widget.</summary>
    One,

    /// <summary>Several widgets.</summary>
    Several,
}

/// <summary>[S-11751] A shared setting as the page shows it: the widgets that hold it (two or more) and whether they disagree.</summary>
public sealed record SharedRow(SharedSetting Setting, IReadOnlyList<string> Holders, bool Mixed);

/// <summary>
/// [S-11754] The selection's page as a model: what is selected, what it offers (<see cref="QuickSetOffer"/>:
/// the header, the look) and the settings its widgets share. Several widgets' page shows no widget one by
/// one ([S-11755]): what they share, and how they look.
/// </summary>
public sealed record SelectionModel(SelectionKind Kind, QuickSetOffer Offer, IReadOnlyList<SharedRow> Shared);

/// <summary>
/// [S-2340]..[S-2343] [S-11754] [S-11755] The selection's entry on the edit bar and its page, after
/// Karpo's widget page (karpo-app, docs/widgets.md §529, §575, §596): the entry is the kind's glyph and
/// name, "2 × Clock" for several of one kind, "3 widgets" for several kinds;
/// the page a header card, then the sections the selection shares. Pure: the page's rows are the
/// properties' lines ([S-3719]), the host's to build.
/// </summary>
public static class SelectionPage
{
    /// <summary>
    /// [S-2343] What a selection that has its entry on the bar does to its flyout: it opens it once, when
    /// the selection is new (<paramref name="selection"/>, its key, is not <paramref name="shown"/>, the
    /// one the flyout was last opened for), and never while a ghost is being placed. While a widget is
    /// held by the pointer (<paramref name="held"/>: a press, a drag, a resize, the drag that unlocks the
    /// desktop, [S-2007]) nothing opens over the widget that is still moving, and the selection stays
    /// new: the gesture's end opens its page (<see cref="OpensWhenReleased"/>).
    /// Returns the key now shown and whether the flyout opens.
    /// </summary>
    public static (string? Shown, bool Opens) Flyout(string? shown, string selection, bool placing, bool held)
    {
        ArgumentNullException.ThrowIfNull(selection);
        return held
            ? (shown, false)
            : (selection, !string.Equals(shown, selection, StringComparison.Ordinal) && !placing);
    }

    /// <summary>
    /// [S-2343] A pointer gesture on a widget ended (a click on it, a drag or a resize of it, the drag
    /// that unlocked the desktop): the selection's page is open afterwards. Whether it has to be opened
    /// now: the selection has a page (<paramref name="shows"/>), it is not open already, and no ghost is
    /// being placed. This is the one opening that does not ask whether the selection is new: a click on
    /// a widget whose page the user closed opens it again.
    /// </summary>
    public static bool OpensWhenReleased(bool shows, bool open, bool placing) => shows && !open && !placing;

    /// <summary>
    /// [S-11754] The page's model for the selection on the display showing <paramref name="shownViewId"/>:
    /// null while nothing of it is in that view.
    /// </summary>
    public static SelectionModel? Of(AppState state, WidgetSelection selection, string? shownViewId)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(selection);

        var offer = QuickSet.For(state, selection, shownViewId);
        if (!offer.Shows || state.FindView(shownViewId) is not { } view)
        {
            return null;
        }

        var picked = new HashSet<string>(selection.Ids, StringComparer.Ordinal);
        var widgets = view.Widgets.Where(w => picked.Contains(w.Id)).ToList();
        return widgets.Count == 1
            ? new SelectionModel(SelectionKind.One, offer, [])
            : new SelectionModel(SelectionKind.Several, offer, Shared(widgets));
    }

    /// <summary>[S-11751] The settings two or more of <paramref name="widgets"/> share, each with its holders and whether they disagree.</summary>
    public static IReadOnlyList<SharedRow> Shared(IReadOnlyList<WidgetInstance> widgets)
    {
        ArgumentNullException.ThrowIfNull(widgets);

        var configs = widgets.Select(w => w.Config).ToList();
        return
        [
            .. SharedSettings.Among(configs).Select(setting => new SharedRow(
                setting,
                [.. widgets.Where(w => setting.Holds(w.Config)).Select(w => w.Id)],
                SharedSettings.Read(setting, configs).Mixed)),
        ];
    }

    /// <summary>[S-2341] The entry's words for <paramref name="model"/>, as <see cref="Words(QuickSetOffer, Func{WidgetId, string})"/>.</summary>
    public static string? Words(SelectionModel? model, Func<WidgetId, string> name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return model is null ? null : Words(model.Offer, name);
    }

    /// <summary>[S-2341] The entry's glyph: the one kind's, or the apps glyph for several kinds.</summary>
    public static string Glyph(SelectionModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        return (model.Offer.TitleKind ?? model.Offer.CommonKind) is { } kind ? ModuleCatalog.Of(kind).Glyph : Glyphs.Apps;
    }

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
    /// [S-2342] [S-11754] The sections under the header card. One widget: What it shows where the look has a style or the widget has lines of its own
    /// (<paramref name="ownLines"/>) or what its look draws with (<paramref name="styleLines"/>); Appearance.
    /// Several: the style for one kind, For all of them where they share a setting, Appearance ([S-3702]).
    /// [S-12480] No Header section: no card has a header; a title is a Header widget of its own.
    /// </summary>
    public static IReadOnlyList<SelectionSection> Sections(SelectionModel model, bool ownLines, bool styleLines)
    {
        ArgumentNullException.ThrowIfNull(model);

        var offer = model.Offer;
        var sections = new List<SelectionSection>(4);
        if (offer.HasStyle || (offer.HasTitle && (ownLines || styleLines)))
        {
            sections.Add(SelectionSection.Shows);
        }

        if (model.Shared.Count > 0)
        {
            sections.Add(SelectionSection.Shared);
        }

        sections.Add(SelectionSection.Appearance);
        return sections;
    }

    /// <summary>[S-2345] The tab a section stands on: Appearance its own, every other Settings.</summary>
    public static SelectionTab TabOf(SelectionSection section)
        => section == SelectionSection.Appearance ? SelectionTab.Appearance : SelectionTab.Settings;

    /// <summary>
    /// [S-2345] The tabs of <paramref name="sections"/>, in order, each with its sections: Settings while one
    /// stands on it, then Appearance. One tab alone is no strip: its sections stand under the header card.
    /// </summary>
    public static IReadOnlyList<(SelectionTab Tab, IReadOnlyList<SelectionSection> Sections)> Tabs(IReadOnlyList<SelectionSection> sections)
    {
        ArgumentNullException.ThrowIfNull(sections);
        return
        [
            .. Enum.GetValues<SelectionTab>()
                .Select(tab => (tab, (IReadOnlyList<SelectionSection>)[.. sections.Where(s => TabOf(s) == tab)]))
                .Where(t => t.Item2.Count > 0),
        ];
    }

    /// <summary>[S-2345] A tab's words.</summary>
    public static string TabTitle(SelectionTab tab) => tab switch
    {
        SelectionTab.Settings => Str.EditBar_Page_SettingsTab.Text(),
        SelectionTab.Appearance => Str.Settings_Section_Appearance.Text(),
        _ => throw new ArgumentOutOfRangeException(nameof(tab), tab, "not a tab"),
    };

    /// <summary>[S-2342] [S-11754] A section's heading.</summary>
    public static string Heading(SelectionSection section) => section switch
    {
        SelectionSection.Shows => Str.EditBar_Page_Shows.Text(),
        SelectionSection.Shared => Str.Groups_Page_Shared.Text(),
        SelectionSection.Appearance => Str.Settings_Section_Appearance.Text(),
        _ => throw new ArgumentOutOfRangeException(nameof(section), section, "not a section"),
    };
}
