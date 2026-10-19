using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>
/// [S-3701] What the selected widget's properties strip ([S-3707]) offers for the selection on one
/// display: how many widgets it acts on, their header and background (null where they differ),
/// whether a header can be set at all ([S-3711]), and for exactly one widget its title.
/// <see cref="None"/> hides the strip.
/// </summary>
/// <param name="Count">The selected widgets in the view the display shows.</param>
/// <param name="Header">Their header placement, or null when they differ.</param>
/// <param name="Background">[S-6566] Their own background, or null when they differ.</param>
/// <param name="TitleWidgetId">The one widget whose title the group edits; null with several.</param>
/// <param name="TitleKind">That widget's kind, whose name is the title's placeholder.</param>
/// <param name="Label">That widget's own title, null for the kind's name.</param>
/// <param name="HasHeader">[S-3711] Whether the Header choice shows: every selected widget can have a header.</param>
/// <param name="StyleKind">[S-6007] The one kind whose style the Style choice sets: every selected widget is of it and it has styles. Null hides the choice.</param>
/// <param name="Style">[S-6007] Their style, or null when they differ.</param>
/// <param name="Items">[S-6521] The own item background the widgets with items share, or null when they differ.</param>
/// <param name="HasItems">[S-6571] [S-6573] Whether any selected widget draws items now ([S-6570]); the item lines are not shown without.</param>
/// <param name="Scale">[S-6531] Their own content scale, or null when they differ.</param>
/// <param name="ItemText">[S-6544] Their own item text colour (empty: they follow the app-wide one), or null when they differ.</param>
/// <param name="CanAlign">[S-6584] Whether any selected widget can align its content in its style ([S-6581]); the Alignment line is not shown without.</param>
/// <param name="Align">[S-6584] Where the widgets that can align draw their content, or null when they differ.</param>
/// <param name="HasItemText">[S-6574] Whether any selected widget draws items with text on them (not tracks only); the Item text line is not shown without.</param>
public sealed record QuickSetOffer(
    int Count,
    HeaderPlacement? Header,
    FillChoice? Background,
    string? TitleWidgetId,
    WidgetId? TitleKind,
    string? Label,
    bool HasHeader = true,
    WidgetId? StyleKind = null,
    int? Style = null,
    FillChoice? Items = null,
    bool HasItems = true,
    double? Scale = 1.0,
    string? ItemText = "",
    bool CanAlign = false,
    ContentAlign? Align = null,
    bool HasItemText = true)
{
    public static readonly QuickSetOffer None = new(0, null, null, null, null, null, false);

    /// <summary>[S-6007] The Style choice shows.</summary>
    public bool HasStyle => StyleKind is not null;

    /// <summary>Whether the group shows at all.</summary>
    public bool Shows => Count > 0;

    /// <summary>[S-3702] The title box: one widget only.</summary>
    public bool HasTitle => TitleWidgetId is not null;
}

/// <summary>
/// [S-3701]..[S-3711] The rules behind the properties strip: which controls show for a
/// selection, what a mixed selection reads as, what a typed title stores, and applying a choice to
/// every selected widget. Pure; the host runs the writes through its one mutation path.
/// </summary>
public static class QuickSet
{
    /// <summary>
    /// [S-3701]/[S-3702] The offer for the display showing <paramref name="shownViewId"/>: nothing
    /// unless the selection is in that view; with one widget, header, background and title; with
    /// several, header and background only (a title is the widget's own name, so one typed for
    /// many would make them indistinguishable). [S-3711] No header choice when any of them can
    /// never have a header: the strip shows what all of them share.
    /// </summary>
    public static QuickSetOffer For(AppState state, WidgetSelection selection, string? shownViewId)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(selection);

        if (selection.IsEmpty || shownViewId is null
            || !string.Equals(selection.ViewId, shownViewId, StringComparison.Ordinal)
            || state.FindView(shownViewId) is not { } view)
        {
            return QuickSetOffer.None;
        }

        var widgets = Selected(view, selection.Ids);
        if (widgets.Count == 0)
        {
            return QuickSetOffer.None;
        }

        var hasHeader = widgets.TrueForAll(w => WidgetInstance.CanHaveHeader(w.Kind));
        var header = hasHeader ? Common(widgets.Select(w => w.Header)) : null;
        var background = CommonFill(widgets.Select(w => w.Background));

        // [S-6007] A style choice only where it means one thing: every widget the same kind, and
        // that kind with two styles or more; [S-6075] and the look able to change one of them now.
        var kind = widgets[0].Kind;
        var styleKind = widgets.TrueForAll(w => w.Kind == kind) && WidgetStyles.Offers(kind) && widgets.Exists(w => WidgetStyles.Applies(w.Config))
            ? kind
            : (WidgetId?)null;
        var style = styleKind is null ? null : Common(widgets.Select(w => WidgetStyles.Current(w.Config) ?? 0));

        // [S-6571] [S-6573] The item background of the widgets that draw items now: the others have none to show.
        var withItems = widgets.Where(ItemKinds.Draws).ToList();
        var hasItems = withItems.Count > 0;
        var items = hasItems ? CommonFill(withItems.Select(w => w.ItemBackground)) : new FillChoice();

        // [S-6531] Read as stored, so two widgets at the same percentage always share it.
        var scale = Common(widgets.Select(w => WidgetScale.Normalise(w.Scale)));

        // [S-6574] The item text of the widgets whose items carry text: a track has none.
        var withText = withItems.Where(ItemKinds.DrawsText).ToList();
        var hasItemText = withText.Count > 0;
        var itemText = CommonItemText(hasItemText ? withText : widgets);

        // [S-6584] The alignment the widgets that can align draw with: the others have none to show.
        var aligned = widgets.Where(ContentAlignment.Applies).ToList();
        var canAlign = aligned.Count > 0;
        var align = canAlign ? Common(aligned.Select(ContentAlignment.Shown)) : null;

        if (widgets.Count == 1)
        {
            var only = widgets[0];
            return new QuickSetOffer(1, header, background, only.Id, only.Kind, only.Label, hasHeader, styleKind, style, items, hasItems, scale, itemText, canAlign, align, hasItemText);
        }

        return new QuickSetOffer(widgets.Count, header, background, null, null, null, hasHeader, styleKind, style, items, hasItems, scale, itemText, canAlign, align, hasItemText);
    }

    /// <summary>
    /// [S-6566] The fill choice the widgets share (a copy of it), or null when any two differ
    /// (<see cref="FillChoice.SameAs"/>) or there are none. Default is a choice like the others: a
    /// widget following the app-wide fill and one of its own are mixed.
    /// </summary>
    public static FillChoice? CommonFill(IEnumerable<FillChoice> fills)
    {
        ArgumentNullException.ThrowIfNull(fills);

        FillChoice? first = null;
        foreach (var fill in fills)
        {
            if (first is null)
            {
                first = fill;
            }
            else if (!first.SameAs(fill))
            {
                return null;
            }
        }

        return first?.Clone();
    }

    /// <summary>
    /// [S-6544] The item text colour the widgets share, normalised: empty when none has one of its
    /// own, the colour when all have the same, null when they differ (one own and one following too).
    /// </summary>
    public static string? CommonItemText(IReadOnlyList<WidgetInstance> widgets)
    {
        ArgumentNullException.ThrowIfNull(widgets);

        if (widgets.Count == 0)
        {
            return string.Empty;
        }

        var first = HexColor.Normalise(widgets[0].ItemTextColor);
        return widgets.All(w => string.Equals(HexColor.Normalise(w.ItemTextColor), first, StringComparison.Ordinal)) ? first : null;
    }

    /// <summary>[S-3703] The one value all of <paramref name="values"/> share; null when they differ or there are none.</summary>
    public static T? Common<T>(IEnumerable<T> values)
        where T : struct
    {
        ArgumentNullException.ThrowIfNull(values);

        T? common = null;
        foreach (var value in values)
        {
            if (common is null)
            {
                common = value;
            }
            else if (!EqualityComparer<T>.Default.Equals(common.Value, value))
            {
                return null;
            }
        }

        return common;
    }

    /// <summary>
    /// [S-3704] What a typed title stores: trimmed, and null (the kind's name, in whatever language
    /// is current) when it is blank or the kind's name itself, so typing the default back never
    /// freezes it in one language.
    /// </summary>
    public static string? Title(string? typed, string defaultTitle)
    {
        var text = typed?.Trim();
        if (string.IsNullOrEmpty(text) || string.Equals(text, defaultTitle?.Trim(), StringComparison.Ordinal))
        {
            return null;
        }

        return text;
    }

    /// <summary>
    /// [S-3703] Set the header of every selected widget of <paramref name="view"/>. Returns how many
    /// changed. [S-3711] A widget that can never have a header is left headerless.
    /// </summary>
    public static int SetHeader(ViewState view, IEnumerable<string> ids, HeaderPlacement header)
        => Apply(view, ids, w => w.Header == header || !WidgetInstance.CanHaveHeader(w.Kind) ? false : Set(() => w.Header = header));

    /// <summary>
    /// [S-3703] [S-6566] Set the background of every selected widget of <paramref name="view"/> to a
    /// copy of <paramref name="fill"/>: Default follows the app-wide one again. Returns how many changed.
    /// </summary>
    public static int SetBackground(ViewState view, IEnumerable<string> ids, FillChoice fill)
    {
        ArgumentNullException.ThrowIfNull(fill);
        return Apply(view, ids, w => !w.Background.SameAs(fill) && Set(() => w.Background = fill.Clone()));
    }

    /// <summary>
    /// [S-6008] Set the style of every selected widget of <paramref name="view"/>. Returns how many
    /// changed. A widget of a kind without that style is left as it is.
    /// </summary>
    public static int SetStyle(ViewState view, IEnumerable<string> ids, int style)
        => Apply(view, ids, w => WidgetStyles.Set(w.Config, style));

    /// <summary>
    /// [S-6521] [S-6571] Set the item background of every selected widget of <paramref name="view"/>
    /// that draws items now ([S-6570], [S-6573]) to a copy of <paramref name="fill"/>: Default follows
    /// the app-wide one again. A widget without items is left as it is. Returns how many changed.
    /// </summary>
    public static int SetItemBackground(ViewState view, IEnumerable<string> ids, FillChoice fill)
    {
        ArgumentNullException.ThrowIfNull(fill);
        return Apply(view, ids, w => ItemKinds.Draws(w) && !w.ItemBackground.SameAs(fill)
            && Set(() => w.ItemBackground = fill.Clone()));
    }

    /// <summary>
    /// [S-6544] Set the item text colour of every selected widget of <paramref name="view"/>: a
    /// colour of their own, or empty ("Default") to follow the app-wide one again. Apart from their
    /// item background, which it never touches. Returns how many changed.
    /// </summary>
    public static int SetItemTextColor(ViewState view, IEnumerable<string> ids, string? hex)
    {
        // [S-6571] [S-6574] Only on widgets whose items carry text now: a track has none to colour.
        var colour = HexColor.Normalise(hex);
        return Apply(view, ids, w => ItemKinds.DrawsText(w)
            && !string.Equals(HexColor.Normalise(w.ItemTextColor), colour, StringComparison.Ordinal)
            && Set(() => w.ItemTextColor = colour));
    }

    /// <summary>
    /// [S-6531] Set the content scale of every selected widget of <paramref name="view"/>, as stored
    /// (<see cref="WidgetScale.Normalise"/>). Returns how many changed.
    /// </summary>
    public static int SetScale(ViewState view, IEnumerable<string> ids, double scale)
    {
        var value = WidgetScale.Normalise(scale);
        return Apply(view, ids, w => !w.Scale.Equals(value) && Set(() => w.Scale = value));
    }

    /// <summary>
    /// [S-6584] Set where every selected widget of <paramref name="view"/> that can align in its
    /// style ([S-6581]) draws its content. The others are left as they are. Returns how many changed.
    /// </summary>
    public static int SetAlign(ViewState view, IEnumerable<string> ids, ContentAlign align)
        => Apply(view, ids, w => ContentAlignment.Applies(w) && w.Align != align && Set(() => w.Align = align));

    /// <summary>[S-3704] Set one widget's title from what was typed. False when nothing changed or the widget is gone.</summary>
    public static bool SetTitle(ViewState view, string widgetId, string? typed, string defaultTitle)
    {
        ArgumentNullException.ThrowIfNull(view);

        if (view.Find(widgetId) is not { } widget)
        {
            return false;
        }

        var label = Title(typed, defaultTitle);
        if (string.Equals(widget.Label, label, StringComparison.Ordinal))
        {
            return false;
        }

        widget.Label = label;
        return true;
    }

    private static List<WidgetInstance> Selected(ViewState view, IEnumerable<string> ids)
    {
        var picked = new HashSet<string>(ids, StringComparer.Ordinal);
        return [.. view.Widgets.Where(w => picked.Contains(w.Id))];
    }

    private static int Apply(ViewState view, IEnumerable<string> ids, Func<WidgetInstance, bool> change)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(ids);

        var changed = 0;
        foreach (var widget in Selected(view, ids))
        {
            if (change(widget))
            {
                changed++;
            }
        }

        return changed;
    }

    private static bool Set(Action write)
    {
        write();
        return true;
    }
}
