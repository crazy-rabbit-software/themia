using Themia.App.Widgets;
using Themia.Sdk.WinUI;
using Themia.Core.Model;
using Themia.Core.Shell;
using Themia.Sdk;

namespace Themia.App.Services;

/// <summary>
/// [S-3703]/[S-3704] The selection page's writes: header, background, item background ([S-6521]), scale
/// ([S-6531]), alignment ([S-6584]) and title, of the widgets it names (the selection, or one member of a
/// group on its own page, [S-11755]); a shared setting set on every widget that holds it ([S-11751]); a
/// group renamed ([S-11754]). Each through <see cref="Mutate"/>, one change each.
/// </summary>
internal sealed partial class AppHost
{
    /// <summary>[S-3701] What the strip offers on the display showing <paramref name="shownViewId"/>.</summary>
    public QuickSetOffer QuickSetFor(string shownViewId) => QuickSet.For(State, Selection, shownViewId);

    /// <summary>[S-11754] The selection page's model on the display showing <paramref name="shownViewId"/>; null while nothing there is selected.</summary>
    public SelectionModel? SelectionModelFor(string shownViewId) => SelectionPage.Of(State, Selection, shownViewId);

    /// <summary>[S-11755] One widget of the selection's view as a lone selection of it would show it: its page's model.</summary>
    public SelectionModel? WidgetModel(string widgetId)
    {
        if (SelectionView is not { } view)
        {
            return null;
        }

        var alone = new WidgetSelection();
        alone.Replace(view.Id, [widgetId]);
        return SelectionPage.Of(State, alone, view.Id);
    }

    /// <summary>[S-3703] The widgets' header, in one step.</summary>
    public void SetHeader(IReadOnlyList<string> ids, HeaderPlacement header) => Change(ids, (view, of) => QuickSet.SetHeader(view, of, header));

    /// <summary>[S-3703] [S-6566] The widgets' background, in one step.</summary>
    public void SetBackground(IReadOnlyList<string> ids, FillChoice fill) => Change(ids, (view, of) => QuickSet.SetBackground(view, of, fill));

    /// <summary>[S-6593] The widgets' background image (a picture file's full path, or empty for none), in one step.</summary>
    public void SetBackgroundImage(IReadOnlyList<string> ids, string path) => Change(ids, (view, of) => QuickSet.SetBackgroundImage(view, of, path));

    /// <summary>[S-6521] The widgets' own item background (the ones that have items, [S-6571]), in one step.</summary>
    public void SetItemBackground(IReadOnlyList<string> ids, FillChoice fill) => Change(ids, (view, of) => QuickSet.SetItemBackground(view, of, fill));

    /// <summary>[S-6544] The widgets' own item text colour, or empty for the app-wide one, in one step.</summary>
    public void SetItemTextColor(IReadOnlyList<string> ids, string hex) => Change(ids, (view, of) => QuickSet.SetItemTextColor(view, of, hex));

    /// <summary>[S-6531] The widgets' own content scale, in one step.</summary>
    public void SetScale(IReadOnlyList<string> ids, double scale) => Change(ids, (view, of) => QuickSet.SetScale(view, of, scale));

    /// <summary>[S-6584] Where the widgets that can align ([S-6581]) draw their content, in one step.</summary>
    public void SetAlign(IReadOnlyList<string> ids, ContentAlign align) => Change(ids, (view, of) => QuickSet.SetAlign(view, of, align));

    /// <summary>[S-6008] The widgets' style, in one step. Only widgets of a kind with that style change.</summary>
    public void SetStyle(IReadOnlyList<string> ids, int style) => Change(ids, (view, of) => QuickSet.SetStyle(view, of, style));

    /// <summary>
    /// [S-11751] <paramref name="setting"/> set to <paramref name="from"/>'s value (the copy its line
    /// wrote) on every one of <paramref name="ids"/> that holds it: one mutation, nothing where all agree already.
    /// </summary>
    public void SetShared(SharedSetting setting, IReadOnlyList<string> ids, WidgetConfig from)
    {
        ArgumentNullException.ThrowIfNull(setting);
        ArgumentNullException.ThrowIfNull(from);
        if (SelectionView is { } view && view.Widgets.Any(w => ids.Contains(w.Id, StringComparer.Ordinal) && setting.Holds(w.Config) && !setting.Same(from, w.Config)))
        {
            Mutate(_ => QuickSet.SetShared(view, ids, setting, from));
        }
    }

    /// <summary>[S-11754] A group's name, as typed: one step; a blank name (or "Group") is none.</summary>
    public void RenameGroup(string groupId, string? typed)
    {
        if (SelectionView is { } view && view.FindGroup(groupId) is { } group
            && !string.Equals(group.Name, SelectionPage.GroupName(typed), StringComparison.Ordinal))
        {
            Mutate(_ => WidgetGroups.Rename(view, groupId, SelectionPage.GroupName(typed)));
        }
    }

    /// <summary>
    /// [S-11755] "Remove from group" on a member's row of its group's page: it alone leaves (a group left
    /// with one dissolves); what stays of the group stays selected.
    /// </summary>
    public void RemoveFromGroup(string widgetId)
    {
        if (SelectionView is not { } view || WidgetGroups.GroupOf(view, widgetId) is not { } group)
        {
            return;
        }

        var rest = WidgetGroups.Members(view, group.Id).Select(w => w.Id).Where(id => id != widgetId).ToList();
        Mutate(_ => WidgetGroups.RemoveFromGroup(view, widgetId), undo: UndoAction.Ungroup);
        SelectMany(view.Id, rest);
    }

    /// <summary>[S-11757] An entered member's link to its group: the whole group selected, its page shown.</summary>
    public void SelectGroupOfEntered()
    {
        if (Selection.Entered is not null)
        {
            ChangeSelection(Selection.StepOut(SelectionView));
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

        var defaultTitle = ModuleCatalog.Of(widget.Kind).Name;
        if (!string.Equals(widget.Label, QuickSet.Title(typed, defaultTitle), StringComparison.Ordinal))
        {
            Mutate(_ => QuickSet.SetTitle(view, widgetId, typed, defaultTitle));
        }
    }

    /// <summary>
    /// [S-3719] One widget's own lines on its properties menu, over its live config; a change they
    /// make in place is stored through the host's one state write. Null for a widget that is gone.
    /// [S-3749] <paramref name="openSetting"/> opens a link row's level on the display the menu is on.
    /// </summary>
    public WidgetMenuLines? WidgetMenuLines(string widgetId, Action<WidgetTextAsk> ask, Action<WidgetConfirm> confirm, Action<SettingLink>? openSetting)
        => Find(widgetId) is var (_, widget) && widget is not null ? ModuleHost.MenuLines(widget.Kind, widget.Config, () => Mutate(_ => { }), ask, confirm, openSetting) : null;

    /// <summary>The selection's view, the widgets of <paramref name="ids"/> in it changed by <paramref name="change"/> in one step.</summary>
    private void Change(IReadOnlyList<string> ids, Func<ViewState, IReadOnlyList<string>, int> change)
    {
        if (SelectionView is { } view)
        {
            var of = ids.ToList();
            Mutate(_ => change(view, of));
        }
    }
}
