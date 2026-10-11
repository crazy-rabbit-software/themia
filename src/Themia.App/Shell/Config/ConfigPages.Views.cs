using CommunityToolkit.WinUI.Controls;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Settings;
using Themia.Core.Shell;
using Themia.Core.Widgets;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-3800] The Views page: Add view, then one row per view in the user's order — its icon in its
/// colour, its name, how many widgets it holds. A row opens to the view's own settings (name, icon,
/// colour); everything done to the view (show it, edit its layout, copy, delete) is in its "…" menu.
/// Its widgets are not listed here: they are arranged on the desktop. The order changes only by
/// dragging a row onto another, or with Alt+Shift+Up / Down on a focused row ([S-3802]).
/// </summary>
internal static partial class ConfigPages
{
    private static FrameworkElement BuildViews(PageContext context)
    {
        var host = context.Host;
        var views = host.State.Views;
        context.Expansion.Retain(SettingsList.Views, views.Select(v => v.Id));

        var add = new Button { Content = Str.Shell_Views_Add.Text() };
        add.Click += (_, _) =>
        {
            // [S-1065] Refused past the free limit, with a notice saying why; nothing to open then.
            if (host.AddView(monitorId: null) is { } created)
            {
                context.Navigate(ConfigTarget.ForView(created));
            }
        };

        var rows = new List<FrameworkElement>
        {
            FluentLayout.Row(Glyphs.Add, Str.Shell_View_New.Text(), Str.Shell_Views_AddDesc.Text(), add, "create", "new"),
        };

        // [S-1703] [S-3802] Dragged onto another view's row, or Alt+Shift+Up / Down: the view bar's
        // own paths, so the bar, the switchers and Ctrl+n follow.
        var drag = new RowReorder<string>(
            (dragged, target) => host.ReorderView(dragged, target),
            (viewId, delta) =>
            {
                var at = host.State.Views.FindIndex(v => v.Id == viewId);
                if (at < 0 || at + delta < 0 || at + delta >= host.State.Views.Count)
                {
                    return false;
                }

                host.MoveView(viewId, delta);
                return true;
            });
        for (var i = 0; i < views.Count; i++)
        {
            rows.Add(ViewGroup(context, views[i], drag));
        }

        // [S-3802] Said once, under the list: the drag has no handle to see, the shortcut no button.
        if (views.Count > 1)
        {
            rows.Add(Note(Str.Shell_Views_ReorderHint.Text()));
        }

        return Page(Str.App_Views.Text(), rows);
    }

    /// <summary>
    /// [S-3800] One view's row: its icon in its colour, name and widget count, the "…" menu on the
    /// right; opened, its Name, Icon and Colour.
    /// </summary>
    private static Border ViewGroup(PageContext context, ViewState view, RowReorder<string> drag)
    {
        var host = context.Host;
        var viewId = view.Id;
        SettingsExpander? expander = null;

        void RefreshHeader()
        {
            if (host.State.FindView(viewId) is { } changed && expander is not null)
            {
                expander.Header = changed.Name;
                expander.HeaderIcon = ViewMark.Icon(changed);
            }
        }

        var name = new TextBox { Text = view.Name, MinWidth = FluentLayout.ChoiceMinWidth };
        name.LostFocus += (_, _) =>
        {
            RenameView(host, viewId, name.Text);

            // [S-1834] A rename keeps the page; the row's header follows here instead.
            if (host.State.FindView(viewId) is { } renamed)
            {
                name.Text = renamed.Name;
            }

            RefreshHeader();
        };

        var rows = new List<Border>
        {
            FluentLayout.Row(Glyphs.Rename, Str.View_Name_Label.Text(), null, name),
            FluentLayout.Row(Glyphs.View, Str.View_Icon_Label.Text(), Str.Shell_View_IconDesc.Text(), ViewIconPicker(host, view, RefreshHeader), "glyph", "symbol"),

            // [S-3810] The icon's colour; the default is the theme's text colour.
            FluentLayout.ColorPickerRow(Glyphs.Palette, Str.Shell_View_Colour.Text(), Str.Shell_View_ColourDesc.Text(), view.Color ?? string.Empty,
                value =>
                {
                    host.Mutate(s => ViewOps.SetColor(s, viewId, value), relayout: false);
                    RefreshHeader();
                },
                new ColorChoiceOptions { DefaultLabel = Str.Shell_View_ColourDefault.Text() }),
        };

        var more = FluentLayout.IconCommand(Glyphs.More, Str.Shell_Views_More.Text());

        var count = host.State.Views.Count;
        var frame = FluentLayout.Expander(
            Glyphs.View,
            view.Name,
            Str.Shell_Views_WidgetCount.Plural(view.Widgets.Count),
            more,
            rows,
            expanded: context.Expansion.IsOpen(SettingsList.Views, viewId, count),
            item: viewId,
            expandedChanged: open => context.Expansion.Set(SettingsList.Views, viewId, open));

        expander = (SettingsExpander)frame.Child;
        expander.HeaderIcon = ViewMark.Icon(view);

        more.Click += (_, _) => ItemMenu.Open(more, ViewMenu(host, viewId));
        expander.RightTapped += (_, args) =>
        {
            if (args.Handled)
            {
                return;
            }

            try
            {
                ItemMenu.Show(expander, args, ViewMenu(host, viewId));
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "could not open a view's menu", ex);
            }
        };

        // A press in one of the view's own settings rows never starts a drag.
        var settings = expander.Items;
        drag.Attach(frame, viewId, node => node is SettingsCard card && settings.Contains(card));
        return frame;
    }

    /// <summary>
    /// [S-3801] A view's "…" menu (and its right-click): Show on this display (off when it already
    /// shows there), Edit layout; Duplicate, Delete (danger, off for the last view). No Move up / Move
    /// down: the order is dragged, or moved with Alt+Shift+arrows ([S-3802]). Read fresh each time it
    /// opens. "This display" is the settings window's ([S-1702]).
    /// </summary>
    private static List<ItemMenuEntry> ViewMenu(AppHost host, string viewId)
    {
        var views = host.State.Views;
        if (views.FindIndex(v => v.Id == viewId) < 0)
        {
            return [];
        }

        var surface = host.SettingsSurface();

        return
        [
            new(Glyphs.View, Str.WidgetsC_ViewSwitcher_Menu_Show.Text(), () => host.ShowView(surface!.Monitor.Id, viewId),
                Disabled: surface is null || surface.CurrentViewId == viewId),

            // [S-1701] Edit layout puts this view on the settings display first: edit mode alone would
            // arrange whatever that screen happened to show. Widgets are added there ([S-420]).
            new(Glyphs.Edit, Str.Shell_Card_EditLayout.Text(), () => host.EditView(viewId)),
            ItemMenuEntry.Separator,
            new(Glyphs.Copy, Str.App_DuplicateView.Text(), () => host.DuplicateView(viewId, host.SettingsSurface()?.Monitor.Id), Quick: QuickCommand.Duplicate),

            // [S-881] No confirmation: the undo notice is the way back.
            new(Glyphs.Delete, Str.App_DeleteView.Text(), () => host.DeleteView(viewId), Danger: true, Disabled: views.Count <= 1, Quick: QuickCommand.Delete),
        ];
    }

    /// <summary>[S-1707] Rename a view: one rule, <see cref="ViewOps.Rename"/>.</summary>
    internal static void RenameView(AppHost host, string viewId, string? name)
        => host.Mutate(s => ViewOps.Rename(s, viewId, name));

    /// <summary>
    /// [S-903] One glyph from the shared icon grid, or the name's initials — which is what a view
    /// without an icon shows in the view bar, so the "default" cell shows exactly that.
    /// </summary>
    internal static DropDownButton ViewIconPicker(AppHost host, ViewState view, Action? changed = null)
    {
        var viewId = view.Id;
        return IconChoice.Create(
            view.Icon,
            Glyphs.View,
            glyph =>
            {
                host.Mutate(s =>
                {
                    if (s.FindView(viewId) is { } target)
                    {
                        target.Icon = glyph;
                    }
                });
                changed?.Invoke();
            },
            defaultText: ViewListRules.Initials(view.Name),
            defaultName: Str.Shell_View_Initials.Text());
    }

    /// <summary>
    /// [S-1706] The view a display shows: its own assignment, or the active view when it has none
    /// yet ([S-18]). Read only, unlike <see cref="AppState.ViewForMonitor"/>, which repairs.
    /// </summary>
    internal static string ShownViewId(AppState state, string monitorId)
        => state.MonitorViews.TryGetValue(monitorId, out var assigned) && state.FindView(assigned) is not null
            ? assigned
            : state.ActiveView().Id;
}
