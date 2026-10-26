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

    /// <summary>[S-1065] A new, empty view at the end, shown here; refused past the free limit, with a notice saying why.</summary>
    void Add();

    /// <summary>[S-900] A copy of a view right after it, shown here; within the licence.</summary>
    void Duplicate(string viewId);

    /// <summary>[S-902] Delete a view (never the last), repairing what named it; the undo notice offers it back ([S-881]).</summary>
    void Delete(string viewId);

    /// <summary>[S-901] Move a view into another's place in the order, one undo step.</summary>
    void Reorder(string viewId, string targetId);

    /// <summary>[S-3727] Ask on a level of the menu's own; only the answer runs it.</summary>
    void Confirm(SettingsConfirm confirm);
}

/// <summary>
/// [S-3541] [S-3542] [S-3549] The edit bar's View menu (View ▾) as data, after Karpo's Layout ▾
/// (karpo-app, docs/widgets.md §352): the view this display shows, its own settings on the first
/// level — how many widgets it holds, Name, Icon, Colour; Duplicate view and Delete view — then
/// Manage views ›, every view's order, name and deletion (Karpo's level of things). Which view this
/// display shows is Display ▾'s ([S-3543]), not here. Keys hold the views' ids, so a rename, a
/// language switch or a move keeps what is open open.
/// </summary>
public static class ViewsMenu
{
    /// <summary>[S-3542] The first level: the shown view's own lines, then Manage views ›.</summary>
    public static IReadOnlyList<SettingsLine> Root(IViewsMenuHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        var state = host.State;
        var view = state.FindView(ShownOn(state, host.DisplayId)) ?? state.ActiveView();
        var id = view.Id;
        var widgets = Str.Shell_Views_WidgetCount.Plural(view.Widgets.Count);

        return
        [
            new SettingsNote(Keys.Count, widgets),
            Rename(host, view, Keys.Name, Str.View_Name_Label.Text(), Glyphs.Rename),
            new SettingsIcon(Keys.Icon, Str.View_Icon_Label.Text(), Mark(view),
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
            new SettingsColour(Keys.Colour, Str.Shell_View_Colour.Text(), view.Color ?? string.Empty,
                value => host.Mutate(s => ViewOps.SetColor(s, id, value), relayout: false), Str.Shell_View_ColourDefault.Text(), ColorPresets.Windows),
            new SettingsSeparator("-" + Keys.Duplicate),

            // [S-900] A copy of this view, shown here in its place.
            new SettingsCommand(Keys.Duplicate, Str.App_DuplicateView.Text(), Glyphs.Duplicate, () => host.Duplicate(id)),
            Delete(host, view, Keys.Delete, state.Views.Count, widgets) with { Glyph = Glyphs.Delete },
            new SettingsSeparator("-" + Keys.Manage),
            Manage(host),
        ];
    }

    /// <summary>
    /// [S-3549] Manage views ›: New view on top (an empty view at the end, shown here; past the free
    /// limit refused with a notice saying why, [S-1065]), a rule, then every view in the user's order,
    /// each with its icon in its colour, Rename (F2) and Delete (Del) at its end, dragged or moved with
    /// Ctrl+Up / Down to reorder ([S-3732], [S-901]).
    /// </summary>
    private static SettingsSubmenu Manage(IViewsMenuHost host)
    {
        var state = host.State;
        var views = state.Views;
        var lines = new List<SettingsLine>(views.Count + 2)
        {
            new SettingsCommand(Keys.New, Str.Shell_View_New.Text(), Glyphs.Add, host.Add),
            new SettingsSeparator("-" + Keys.Manage + "/each"),
        };

        // [S-3732] One move for the whole list: the engine drags a line among the lines of the same move.
        Action<int, int> move = (from, to) => Move(host, from, to);
        for (var i = 0; i < views.Count; i++)
        {
            var view = views[i];
            var key = Keys.View(view.Id);
            var widgets = Str.Shell_Views_WidgetCount.Plural(view.Widgets.Count);
            var rename = Rename(host, view, key + "/rename", Str.Menu_Rename.Text(), Glyphs.Rename);
            var delete = Delete(host, view, key + "/delete", views.Count, widgets);
            lines.Add(new SettingsItem(key, view.Name, Mark(view),
            [
                new SettingsTool(key + "/rename", Glyphs.Edit, Str.Common_Rename.Text(), SettingsToolKey.Rename) { Ask = rename },
                new SettingsTool(key + "/delete", Glyphs.Delete, Str.Common_Delete.Text(), SettingsToolKey.Delete) { Run = delete.Run, Disabled = delete.Disabled },
            ])
            {
                GlyphColour = view.Color,
                Order = new SettingsOrder(i, views.Count, move),
            });
        }

        return new SettingsSubmenu(Keys.Manage, Str.EditBar_View_Manage.Text(), Glyphs.ListLayout, lines)
        {
            Value = LocaleFormat.Integer(views.Count),
        };
    }

    /// <summary>[S-1707] [S-3736] A view's name as a row whose level is the field holding it; stored trimmed.</summary>
    private static SettingsAsk Rename(IViewsMenuHost host, ViewState view, string key, string text, string glyph)
    {
        var id = view.Id;
        return new SettingsAsk(key, text, glyph, Str.BarMenus_View_RenameTitle.Text(), Str.View_Name_Label.Text(),
            Str.Common_Rename.Text(), view.Name, _ => null, name => host.Mutate(s => ViewOps.Rename(s, id, name)))
        {
            Value = key == Keys.Name ? view.Name : null,
        };
    }

    /// <summary>
    /// [S-902] [S-3737] Delete view, in the critical colour, asked first on a level of its own with
    /// what goes with it; [S-12] greyed on the last view: a desktop with no view has nowhere to put a widget.
    /// </summary>
    private static SettingsCommand Delete(IViewsMenuHost host, ViewState view, string key, int count, string widgets)
    {
        var id = view.Id;
        return new SettingsCommand(key, Str.App_DeleteView.Text(), null, () => host.Confirm(new SettingsConfirm(
            Str.BarMenus_View_DeleteQuestion.Format(("name", view.Name)),
            Str.Common_Delete.Text(),
            () => host.Delete(id),
            Str.BarMenus_View_DeleteDetail.Format(("widgets", widgets)),
            Danger: true)))
        {
            Danger = true,
            Disabled = count <= 1,
        };
    }

    /// <summary>[S-3810] A view's mark: its own icon, or the shared view glyph where it has none.</summary>
    internal static string Mark(ViewState view) => string.IsNullOrEmpty(view.Icon) ? Glyphs.View : view.Icon;

    /// <summary>[S-3732] A drop or Ctrl+arrow: the view at <paramref name="from"/> takes the place of the one at <paramref name="to"/>, as the state holds them now.</summary>
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

    /// <summary>The menu's keys that something outside it opens (a render, a test).</summary>
    public static class Keys
    {
        public const string Count = "count";
        public const string Name = "name";
        public const string Icon = "icon";
        public const string Colour = "colour";
        public const string Duplicate = "duplicate";
        public const string Delete = "delete";
        public const string Manage = "manage";
        public const string New = "manage/new";

        /// <summary>[S-3549] A view's row in Manage views, by its id: the same in every language and through a rename.</summary>
        public static string View(string viewId) => Manage + "/view:" + viewId;
    }
}
