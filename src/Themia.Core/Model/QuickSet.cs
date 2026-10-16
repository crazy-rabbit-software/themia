using Themia.Core.Widgets;

namespace Themia.Core.Model;

/// <summary>
/// [S-3701] What the selected widget's properties strip ([S-3707]) offers for the selection on one
/// display: how many widgets it acts on, their header and background (null where they differ),
/// whether a header can be set at all ([S-3711]), and for exactly one widget its title.
/// <see cref="None"/> hides the strip.
/// </summary>
/// <param name="Count">The selected widgets in the view the display shows.</param>
/// <param name="Header">Their header placement, or null when they differ.</param>
/// <param name="Surface">Their background, or null when they differ.</param>
/// <param name="TitleWidgetId">The one widget whose title the group edits; null with several.</param>
/// <param name="TitleKind">That widget's kind, whose name is the title's placeholder.</param>
/// <param name="Label">That widget's own title, null for the kind's name.</param>
/// <param name="HasHeader">[S-3711] Whether the Header choice shows: every selected widget can have a header.</param>
/// <param name="StyleKind">[S-6007] The one kind whose style the Style choice sets: every selected widget is of it and it has styles. Null hides the choice.</param>
/// <param name="Style">[S-6007] Their style, or null when they differ.</param>
public sealed record QuickSetOffer(
    int Count,
    HeaderPlacement? Header,
    WidgetSurface? Surface,
    string? TitleWidgetId,
    WidgetKind? TitleKind,
    string? Label,
    bool HasHeader = true,
    WidgetKind? StyleKind = null,
    int? Style = null)
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
        var surface = Common(widgets.Select(w => w.Surface));

        // [S-6007] A style choice only where it means one thing: every widget the same kind, and
        // that kind with two styles or more.
        var kind = widgets[0].Kind;
        var styleKind = widgets.TrueForAll(w => w.Kind == kind) && WidgetStyles.Offers(kind) ? kind : (WidgetKind?)null;
        var style = styleKind is null ? null : Common(widgets.Select(w => WidgetStyles.Current(w.Config) ?? 0));

        if (widgets.Count == 1)
        {
            var only = widgets[0];
            return new QuickSetOffer(1, header, surface, only.Id, only.Kind, only.Label, hasHeader, styleKind, style);
        }

        return new QuickSetOffer(widgets.Count, header, surface, null, null, null, hasHeader, styleKind, style);
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

    /// <summary>[S-3703] Set the background of every selected widget of <paramref name="view"/>. Returns how many changed.</summary>
    public static int SetSurface(ViewState view, IEnumerable<string> ids, WidgetSurface surface)
        => Apply(view, ids, w => w.Surface == surface ? false : Set(() => w.Surface = surface));

    /// <summary>
    /// [S-6008] Set the style of every selected widget of <paramref name="view"/>. Returns how many
    /// changed. A widget of a kind without that style is left as it is.
    /// </summary>
    public static int SetStyle(ViewState view, IEnumerable<string> ids, int style)
        => Apply(view, ids, w => WidgetStyles.Set(w.Config, style));

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
