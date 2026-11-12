using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk;

namespace Themia.Core.Shell;

/// <summary>[S-2342] [S-11754] A section of the selection's page, in its order under the header card.</summary>
public enum SelectionSection
{
    /// <summary>[S-11757] An entered member: the link back to its group's page.</summary>
    InGroup,

    /// <summary>What it shows: the style first with what that look draws under it, then the widget's own lines.</summary>
    Shows,

    /// <summary>[S-11751] For all of them: every setting two or more of the widgets share, once.</summary>
    Shared,

    /// <summary>Header: the title (one widget) and where the header stands.</summary>
    Header,

    /// <summary>[S-11755] A group's widgets: a row each, opening its own page.</summary>
    Members,

    /// <summary>Appearance: alignment, background, item background, item text, background image, scale.</summary>
    Appearance,
}

/// <summary>[S-11754] What is selected, as the page is built for it.</summary>
public enum SelectionKind
{
    /// <summary>One lone widget.</summary>
    One,

    /// <summary>Several widgets that are not one whole group.</summary>
    Several,

    /// <summary>[S-2451] One group, whole.</summary>
    Group,

    /// <summary>[S-2451] One member of a group, entered.</summary>
    Member,
}

/// <summary>[S-11755] A group's member as its row names it: its kind, and its name as its header shows it.</summary>
public sealed record SelectionMember(string Id, WidgetId Kind, string Name);

/// <summary>[S-11751] A shared setting as the page shows it: the widgets that hold it (two or more) and whether they disagree.</summary>
public sealed record SharedRow(SharedSetting Setting, IReadOnlyList<string> Holders, bool Mixed);

/// <summary>
/// [S-11754] The selection's page as a model: what is selected, what it offers (<see cref="QuickSetOffer"/>:
/// the header, the look), the group it is or is in, its members and the settings they share.
/// </summary>
public sealed record SelectionModel(SelectionKind Kind, QuickSetOffer Offer, IReadOnlyList<SelectionMember> Members, IReadOnlyList<SharedRow> Shared)
{
    /// <summary>The group selected whole, or the one the entered member is in.</summary>
    public string? GroupId { get; init; }

    /// <summary>That group's stored name, null for none.</summary>
    public string? GroupName { get; init; }

    /// <summary>[S-11754] The group's name as the page and the bar say it: its own, or "Group".</summary>
    public string? GroupTitle => GroupId is null ? null : SelectionPage.GroupTitle(GroupName);
}

/// <summary>
/// [S-2340]..[S-2343] [S-11754]..[S-11757] The selection's entry on the edit bar and its page, after
/// Karpo's widget page (karpo-app, docs/widgets.md §529, §575, §596): the entry is the kind's glyph and
/// name, "2 × Clock" for several of one kind, "3 widgets" for several kinds, a group's name for a group;
/// the page a header card, then the sections the selection shares. Pure: the page's rows are the
/// properties' lines ([S-3719]), the host's to build.
/// </summary>
public static class SelectionPage
{
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
        var members = widgets.Select(w => new SelectionMember(w.Id, w.Kind, w.Label ?? ModuleCatalog.Of(w.Kind).Name)).ToList();

        if (selection.Entered is { } entered && view.FindGroup(entered) is { } inside)
        {
            return new SelectionModel(SelectionKind.Member, offer, members, []) { GroupId = inside.Id, GroupName = inside.Name };
        }

        var shared = Shared(widgets);
        if (WholeGroup(view, widgets) is { } group)
        {
            return new SelectionModel(SelectionKind.Group, offer, members, shared) { GroupId = group.Id, GroupName = group.Name };
        }

        return widgets.Count == 1
            ? new SelectionModel(SelectionKind.One, offer, members, [])
            : new SelectionModel(SelectionKind.Several, offer, members, shared);
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

    /// <summary>[S-2451] The group <paramref name="widgets"/> are, whole and nothing else; null otherwise.</summary>
    private static WidgetGroup? WholeGroup(ViewState view, List<WidgetInstance> widgets)
    {
        if (widgets.Count < 2 || widgets[0].Group is not { } id || !widgets.All(w => string.Equals(w.Group, id, StringComparison.Ordinal)))
        {
            return null;
        }

        return WidgetGroups.Members(view, id).Count == widgets.Count ? view.FindGroup(id) : null;
    }

    /// <summary>[S-11754] A group's name as said: its own, or "Group" while it has none.</summary>
    public static string GroupTitle(string? name) => string.IsNullOrWhiteSpace(name) ? Str.Groups_Page_Name.Text() : name;

    /// <summary>
    /// [S-11754] What a typed group name stores: trimmed, and null (no name: "Group" in whatever language
    /// is current) when it is blank or that word itself.
    /// </summary>
    public static string? GroupName(string? typed)
    {
        var text = typed?.Trim();
        return string.IsNullOrEmpty(text) || string.Equals(text, Str.Groups_Page_Name.Text(), StringComparison.Ordinal) ? null : text;
    }

    /// <summary>[S-11754] A group's glyph: its ready-made group's, found by the name it was placed under, else the group glyph.</summary>
    public static string GroupGlyph(string? name)
        => name is { Length: > 0 } && ReadyMadeGroups.All.FirstOrDefault(r => string.Equals(r.Name.Text(), name, StringComparison.Ordinal)) is { } recipe
            ? recipe.Glyph
            : Glyphs.Group;

    /// <summary>[S-2341] [S-11754] The entry's words for <paramref name="model"/>: a group's name for a group, else as <see cref="Words(QuickSetOffer, Func{WidgetId, string})"/>.</summary>
    public static string? Words(SelectionModel? model, Func<WidgetId, string> name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return model switch
        {
            null => null,
            { Kind: SelectionKind.Group } => model.GroupTitle,
            _ => Words(model.Offer, name),
        };
    }

    /// <summary>[S-2341] [S-11754] The entry's glyph: a group's, the one kind's, or the apps glyph for several kinds.</summary>
    public static string Glyph(SelectionModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (model.Kind == SelectionKind.Group)
        {
            return GroupGlyph(model.GroupName);
        }

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
    /// [S-2342] [S-11754] The sections under the header card. One widget (and an entered member, after the
    /// link to its group): What it shows where the look has a style or the widget has lines of its own
    /// (<paramref name="ownLines"/>) or what its look draws with (<paramref name="styleLines"/>); Header
    /// where it can have one ([S-3711]); Appearance. Several: the style for one kind, For all of them
    /// where they share a setting, Header, Appearance ([S-3702]). A group: For all of them, its Widgets,
    /// Appearance.
    /// </summary>
    public static IReadOnlyList<SelectionSection> Sections(SelectionModel model, bool ownLines, bool styleLines)
    {
        ArgumentNullException.ThrowIfNull(model);

        var offer = model.Offer;
        var sections = new List<SelectionSection>(5);
        if (model.Kind == SelectionKind.Member)
        {
            sections.Add(SelectionSection.InGroup);
        }

        if (model.Kind != SelectionKind.Group && (offer.HasStyle || (offer.HasTitle && (ownLines || styleLines))))
        {
            sections.Add(SelectionSection.Shows);
        }

        if (model.Shared.Count > 0)
        {
            sections.Add(SelectionSection.Shared);
        }

        if (model.Kind == SelectionKind.Group)
        {
            sections.Add(SelectionSection.Members);
        }
        else if (offer.HasHeader)
        {
            sections.Add(SelectionSection.Header);
        }

        sections.Add(SelectionSection.Appearance);
        return sections;
    }

    /// <summary>[S-2342] [S-11754] A section's heading; null for the link to the group, which stands under the header card alone.</summary>
    public static string? Heading(SelectionSection section) => section switch
    {
        SelectionSection.InGroup => null,
        SelectionSection.Shows => Str.EditBar_Page_Shows.Text(),
        SelectionSection.Shared => Str.Groups_Page_Shared.Text(),
        SelectionSection.Header => Str.App_ConfigHeaderLabel.Text(),
        SelectionSection.Members => Str.Groups_Page_Members.Text(),
        SelectionSection.Appearance => Str.Settings_Section_Appearance.Text(),
        _ => throw new ArgumentOutOfRangeException(nameof(section), section, "not a section"),
    };
}
