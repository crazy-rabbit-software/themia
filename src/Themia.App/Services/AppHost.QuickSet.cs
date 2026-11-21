using Themia.App.Widgets;
using Themia.Sdk.WinUI;
using Themia.Core.Model;
using Themia.Core.Shell;
using Themia.Sdk;

namespace Themia.App.Services;

/// <summary>
/// [S-3703]/[S-3704] The selection page's writes: header, background, item background ([S-6521]), scale
/// ([S-6531]), alignment ([S-6584]) and title, of the widgets it names (the selection); a shared setting set on every widget that holds it ([S-11751]).
/// Each through <see cref="Mutate"/>, one change each.
/// </summary>
internal sealed partial class AppHost
{
    /// <summary>[S-3701] What the strip offers on the display showing <paramref name="shownViewId"/>.</summary>
    public QuickSetOffer QuickSetFor(string shownViewId) => QuickSet.For(State, Selection, shownViewId);

    /// <summary>[S-11754] The selection page's model on the display showing <paramref name="shownViewId"/>; null while nothing there is selected.</summary>
    public SelectionModel? SelectionModelFor(string shownViewId) => SelectionPage.Of(State, Selection, shownViewId);

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

    /// <summary>
    /// [S-3719] One widget's own lines on its page, over its live config; a change they
    /// make in place is landed through the host's one landing path ([S-15662]: that widget's change, [S-15660]). Null for a widget that is gone.
    /// [S-3749] <paramref name="openSetting"/> opens a link row's level on the display the menu is on.
    /// </summary>
    public WidgetMenuLines? WidgetMenuLines(string widgetId, Action<SettingLink>? openSetting)
        => Find(widgetId) is var (_, widget) && widget is not null ? ModuleHost.MenuLines(widget.Kind, widget.Config, () => Land(null, [new Landing(widget.Id, Config: widget.Config)], UndoAction.Change), openSetting) : null;

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
