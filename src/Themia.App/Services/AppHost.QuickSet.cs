using Themia.App.Widgets;
using Themia.Core.Model;

namespace Themia.App.Services;

/// <summary>
/// [S-3703]/[S-3704] The properties panel's writes ([S-3707]): header, background, item background
/// ([S-6521]), scale ([S-6531]) and title of the selected widgets, each through <see cref="Mutate"/> as one undo step.
/// </summary>
internal sealed partial class AppHost
{
    /// <summary>[S-3701] What the strip offers on the display showing <paramref name="shownViewId"/>.</summary>
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

    /// <summary>[S-6521] Every selected widget's own item colour, or empty for the app-wide item layer, in one step.</summary>
    public void SetSelectedItemColor(string hex)
    {
        if (SelectionView is { } view)
        {
            var ids = Selection.Ids.ToList();
            Mutate(s => QuickSet.SetItemColor(view, ids, hex, s.Design.ItemBackground));
        }
    }

    /// <summary>[S-6521] Every selected widget's own item opacity, in one step; one following the app-wide layer keeps following it.</summary>
    public void SetSelectedItemOpacity(double opacity)
    {
        if (SelectionView is { } view)
        {
            var ids = Selection.Ids.ToList();
            Mutate(_ => QuickSet.SetItemOpacity(view, ids, opacity));
        }
    }

    /// <summary>[S-6544] Every selected widget's own item text colour, or empty for the app-wide one, in one step.</summary>
    public void SetSelectedItemTextColor(string hex)
    {
        if (SelectionView is { } view)
        {
            var ids = Selection.Ids.ToList();
            Mutate(_ => QuickSet.SetItemTextColor(view, ids, hex));
        }
    }

    /// <summary>[S-6531] Every selected widget's own content scale, in one step.</summary>
    public void SetSelectedScale(double scale)
    {
        if (SelectionView is { } view)
        {
            var ids = Selection.Ids.ToList();
            Mutate(_ => QuickSet.SetScale(view, ids, scale));
        }
    }

    /// <summary>[S-6008] Every selected widget's style, in one step. Only widgets of a kind with that style change.</summary>
    public void SetSelectedStyle(int style)
    {
        if (SelectionView is { } view)
        {
            var ids = Selection.Ids.ToList();
            Mutate(_ => QuickSet.SetStyle(view, ids, style));
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
