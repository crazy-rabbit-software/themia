using Themia.App.Widgets;
using Themia.Core.Model;

namespace Themia.App.Services;

/// <summary>
/// [S-3703]/[S-3704] The edit bar's selection group writes: header, background and title of the
/// selected widgets, each through <see cref="Mutate"/> as one undo step.
/// </summary>
internal sealed partial class AppHost
{
    /// <summary>[S-3701] What the group offers on the display showing <paramref name="shownViewId"/>.</summary>
    public QuickSetOffer QuickSetFor(string shownViewId) => QuickSet.For(State, Selection, shownViewId);

    /// <summary>[S-3703] Every selected widget's header, in one step.</summary>
    public void SetSelectedHeader(HeaderPlacement header)
    {
        if (SelectionView is { } view)
        {
            var ids = Selection.Ids.ToList();
            Mutate(_ => QuickSet.SetHeader(view, ids, header));
        }
    }

    /// <summary>[S-3703] Every selected widget's background, in one step.</summary>
    public void SetSelectedSurface(WidgetSurface surface)
    {
        if (SelectionView is { } view)
        {
            var ids = Selection.Ids.ToList();
            Mutate(_ => QuickSet.SetSurface(view, ids, surface));
        }
    }

    /// <summary>
    /// [S-3704] One widget's title, as typed. By id rather than the selection: the box commits as it
    /// loses focus, which can be after a click has already selected another widget.
    /// </summary>
    public void SetTitle(string widgetId, string? typed)
    {
        if (Find(widgetId) is not var (view, widget) || widget is null)
        {
            return;
        }

        var defaultTitle = WidgetCatalog.Definition(widget.Kind).Label;
        if (!string.Equals(widget.Label, QuickSet.Title(typed, defaultTitle), StringComparison.Ordinal))
        {
            Mutate(_ => QuickSet.SetTitle(view, widgetId, typed, defaultTitle));
        }
    }
}
