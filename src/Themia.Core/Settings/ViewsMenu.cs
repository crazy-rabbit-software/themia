using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Shell;
using Themia.Core.Widgets;

namespace Themia.Core.Settings;

/// <summary>
/// [S-3541] What the edit bar's View menu reads and writes on one display: the state (every write one
/// change, one undo step) and the host's view commands, which keep the licence, the display
/// assignments and the undo notice as they are everywhere else.
/// </summary>
public interface IViewsMenuHost
{
    /// <summary>The live state, read as it is now.</summary>
    AppState State { get; }

    /// <summary>[S-842] The display the menu is open on: "here".</summary>
    string DisplayId { get; }

    /// <summary>One change of the state: saved, one undo step.</summary>
    void Mutate(Action<AppState> change, bool relayout = true);

    /// <summary>[S-19] Show a view on this display.</summary>
    void Show(string viewId);

    /// <summary>[S-1065] A new, empty view at the end, shown here; refused past the free limit, with a notice saying why.</summary>
    void Add();

    /// <summary>[S-900] A copy of a view right after it, shown here; within the licence.</summary>
    void Duplicate(string viewId);

    /// <summary>[S-902] Delete a view (never the last), repairing what named it; the undo notice offers it back ([S-881]).</summary>
    void Delete(string viewId);

    /// <summary>[S-901] Move a view into another's place in the order, one undo step.</summary>
    void Reorder(string viewId, string targetId);

    /// <summary>[S-3727] Ask beside the menu; only the answer runs it.</summary>
    void Confirm(SettingsConfirm confirm);
}

/// <summary>
/// [S-3541] [S-3542] The edit bar's View menu (View ▾) as data: this display's views in the user's
/// order, each a line that shows it here and opens what the view offers (its name, icon and colour,
/// a copy, deleting it), dragged to reorder; then New view and Duplicate this view. The lines are
/// the settings menu's vocabulary ([S-3524]), drawn by the same engine; keys hold the views' ids, so
/// a rename, a language switch or a move keeps what is open open.
/// </summary>
public static class ViewsMenu
{
    /// <summary>[S-3541] The first level: one line per view, a divider, New view and Duplicate this view.</summary>
    public static IReadOnlyList<SettingsLine> Root(IViewsMenuHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        var state = host.State;
        var shown = ShownOn(state, host.DisplayId);
        var views = state.Views;
        var lines = new List<SettingsLine>(views.Count + 3);

        // [S-3732] One move for the whole list: the engine drags a line among the lines of the same move.
        Action<int, int> move = (from, to) => Move(host, from, to);
        for (var i = 0; i < views.Count; i++)
        {
            lines.Add(ViewLine(host, views[i], i, views.Count, shown, move));
        }

        lines.Add(new SettingsSeparator("-" + Keys.New));

        // [S-1065] Past the free limit the host refuses it and says why in a notice ([S-881]).
        lines.Add(new SettingsCommand(Keys.New, Str.Shell_View_New.Text(), Glyphs.Add, host.Add));

        // [S-900] A copy of the view this display shows, shown here in its place.
        lines.Add(new SettingsCommand(Keys.DuplicateThis, Str.Shell_View_DuplicateThis.Text(), Glyphs.Duplicate, () => host.Duplicate(shown)));
        return lines;
    }

    /// <summary>
    /// [S-3541] One view's line: its icon in its colour ([S-3810]; the shared view glyph where it has
    /// none), its name, the dot while this display shows it; a click shows it here and opens its
    /// submenu ([S-3542]); dragged (or Alt+Shift+Up / Down) it moves in the order ([S-901]).
    /// </summary>
    private static SettingsSubmenu ViewLine(IViewsMenuHost host, ViewState view, int index, int count, string shown, Action<int, int> move)
    {
        var id = view.Id;
        return new SettingsSubmenu(Keys.View(id), view.Name, string.IsNullOrEmpty(view.Icon) ? Glyphs.View : view.Icon, ViewLevel(host, view, count))
        {
            GlyphColour = view.Color,
            Holds = id == shown,

            // Showing the view that already shows changes nothing: the click only opens its submenu.
            Run = id == shown ? null : () => host.Show(id),
            Order = new SettingsOrder(index, count, move),
        };
    }

    /// <summary>
    /// [S-3542] What one view offers: how many widgets it holds; Rename… ([S-1707]), Icon › ([S-903]),
    /// Colour › ([S-3810]); then Duplicate view ([S-900]) and Delete view (asked first, never the last
    /// view, [S-902]).
    /// </summary>
    private static List<SettingsLine> ViewLevel(IViewsMenuHost host, ViewState view, int count)
    {
        var id = view.Id;
        var key = Keys.View(id);
        var widgets = Str.Shell_Views_WidgetCount.Plural(view.Widgets.Count);

        return
        [
            new SettingsNote(key + "/count", widgets),
            new SettingsAsk(key + "/rename", Str.Menu_Rename.Text(), Glyphs.Rename, Str.BarMenus_View_RenameTitle.Text(), Str.View_Name_Label.Text(),
                Str.Common_Rename.Text(), view.Name, _ => null, name => host.Mutate(s => ViewOps.Rename(s, id, name))),
            new SettingsIcon(key + "/icon", Str.View_Icon_Label.Text(), string.IsNullOrEmpty(view.Icon) ? Glyphs.View : view.Icon,
                string.IsNullOrEmpty(view.Icon) ? null : view.Icon, ViewListRules.Initials(view.Name), Str.Shell_View_Initials.Text(),
                glyph => host.Mutate(s =>
                {
                    if (s.FindView(id) is { } target)
                    {
                        target.Icon = glyph;
                    }
                }))
            {
                GlyphColour = view.Color,
            },
            new SettingsColour(key + "/colour", Str.Shell_View_Colour.Text(), view.Color ?? string.Empty,
                value => host.Mutate(s => ViewOps.SetColor(s, id, value), relayout: false), Str.Shell_View_ColourDefault.Text(), ColorPresets.Windows),
            new SettingsSeparator(key + "/-copy"),
            new SettingsCommand(key + "/duplicate", Str.App_DuplicateView.Text(), Glyphs.Duplicate, () => host.Duplicate(id)),

            // [S-12] The last view cannot go: a desktop with no view has nowhere to put a widget.
            new SettingsCommand(key + "/delete", Str.App_DeleteView.Text(), Glyphs.Delete, () => host.Confirm(new SettingsConfirm(
                Str.BarMenus_View_DeleteQuestion.Format(("name", view.Name)),
                Str.Common_Delete.Text(),
                () => host.Delete(id),
                Str.BarMenus_View_DeleteDetail.Format(("widgets", widgets)),
                Danger: true)))
            {
                Danger = true,
                Disabled = count <= 1,
            },
        ];
    }

    /// <summary>[S-3732] A drop or Alt+Shift+arrow: the view at <paramref name="from"/> takes the place of the one at <paramref name="to"/>, as the state holds them now.</summary>
    private static void Move(IViewsMenuHost host, int from, int to)
    {
        var views = host.State.Views;
        if (from == to || from < 0 || to < 0 || from >= views.Count || to >= views.Count)
        {
            return;
        }

        host.Reorder(views[from].Id, views[to].Id);
    }

    /// <summary>
    /// [S-1706] The view a display shows: its own assignment, or the active view when it has none yet
    /// ([S-18]). Read only, unlike <see cref="AppState.ViewForMonitor"/>, which repairs.
    /// </summary>
    public static string ShownOn(AppState state, string displayId)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.MonitorViews.TryGetValue(displayId, out var assigned) && state.FindView(assigned) is not null
            ? assigned
            : state.ActiveView().Id;
    }

    /// <summary>The menu's keys that something outside it opens (a view chip's View settings, a render).</summary>
    public static class Keys
    {
        public const string New = "new";
        public const string DuplicateThis = "duplicateThis";

        /// <summary>[S-3541] A view's line, by its id: the same in every language and through a rename.</summary>
        public static string View(string viewId) => "view:" + viewId;
    }
}
