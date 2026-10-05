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

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-1832] The Views page: Add view, then one group per view in the user's order — its name, icon,
/// widgets (each drilling into its page), Edit layout, Duplicate and Delete. A group is dragged onto
/// another to take its place, and its "…" menu moves it up or down, shows it here, copies or deletes it.
/// </summary>
internal static partial class ConfigPages
{
    /// <summary>The smallest pointer travel, in DIPs, that turns a press on a group into a drag ([S-913]'s threshold).</summary>
    private const double DragThreshold = 4;

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

        var drag = new ViewDrag();
        for (var i = 0; i < views.Count; i++)
        {
            rows.Add(ViewGroup(context, views[i], drag));
        }

        return Page(Str.App_Views.Text(), rows);
    }

    /// <summary>One view's group. Its header shows the view's icon, name and widget count, and the "…" menu.</summary>
    private static Border ViewGroup(PageContext context, ViewState view, ViewDrag drag)
    {
        var host = context.Host;
        var viewId = view.Id;
        SettingsExpander? expander = null;

        var name = new TextBox { Text = view.Name, MinWidth = 220 };
        name.LostFocus += (_, _) =>
        {
            RenameView(host, viewId, name.Text);

            // [S-1834] A rename keeps the page; the group's header follows here instead.
            if (host.State.FindView(viewId) is { } renamed && expander is not null)
            {
                expander.Header = renamed.Name;
                name.Text = renamed.Name;
            }
        };

        var icon = ViewIconPicker(host, view, () =>
        {
            if (host.State.FindView(viewId) is { } changed && expander is not null)
            {
                expander.HeaderIcon = new FontIcon { Glyph = HeaderGlyph(changed) };
            }
        });

        var rows = new List<Border>
        {
            FluentLayout.Row(Glyphs.Rename, Str.View_Name_Label.Text(), null, name),
            FluentLayout.Row(Glyphs.View, Str.View_Icon_Label.Text(), Str.Shell_View_IconDesc.Text(), icon, "glyph", "symbol"),
        };

        foreach (var widget in view.Widgets)
        {
            var definition = WidgetCatalog.Definition(widget.Kind);
            var id = widget.Id;

            // [S-1540] A card that drills into the widget's own page, with Settings' chevron.
            rows.Add(FluentLayout.NavRow(
                definition.Glyph,
                string.IsNullOrWhiteSpace(widget.Label) ? definition.Label : widget.Label!,

                // The kind under a renamed widget; the kind's own line under one that is not.
                string.IsNullOrWhiteSpace(widget.Label) ? definition.Description : definition.Label,
                () => context.Navigate(new ConfigTarget.Widget(id))));
        }

        // [S-420] Widgets are added on the desktop, from the edit toolbar, where the click that places
        // one can land exactly where it is wanted. [S-1701] Edit layout puts this view on the
        // settings display first: edit mode alone would arrange whatever that screen happened to show.
        var edit = new Button { Content = Str.Shell_Card_EditLayout.Text() };
        edit.Click += (_, _) => host.EditView(viewId);
        rows.Add(FluentLayout.Row(Glyphs.Add, Str.Shell_View_AddWidget.Text(), Str.Shell_View_AddWidgetDesc.Format(("keys", Str.Shell_Key_CtrlPlus.Format(("key", "Alt+W")))), edit));

        // [S-900] The copy shows on the display Settings acts on ([S-1702]).
        var duplicate = new Button { Content = Str.Common_Duplicate.Text() };
        duplicate.Click += (_, _) => host.DuplicateView(viewId, host.SettingsSurface()?.Monitor.Id);
        rows.Add(FluentLayout.Row(Glyphs.Copy, Str.Shell_View_DuplicateThis.Text(), Str.Shell_View_DuplicateDesc.Text(), duplicate, "copy", "clone"));

        // [S-881] No confirmation: the undo notice is the way back.
        var last = host.State.Views.Count <= 1;
        var delete = FluentLayout.DangerButton(Str.App_DeleteView.Text());
        delete.IsEnabled = !last;
        delete.Click += (_, _) => host.DeleteView(viewId);
        rows.Add(FluentLayout.DangerRow(Glyphs.Delete, Str.Shell_View_DeleteThis.Text(),
            last ? Str.Display_CantDeleteLast.Text() : Str.Shell_View_DeleteDesc.Text(),
            delete));

        var more = FluentLayout.IconCommand(Glyphs.More, Str.Shell_Views_More.Text());
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(more, Str.Shell_Views_More.Text());

        var count = host.State.Views.Count;
        var frame = FluentLayout.Expander(
            HeaderGlyph(view),
            view.Name,
            Str.Shell_Views_WidgetCount.Plural(view.Widgets.Count),
            more,
            rows,
            expanded: context.Expansion.IsOpen(SettingsList.Views, viewId, count),
            item: viewId,
            expandedChanged: open => context.Expansion.Set(SettingsList.Views, viewId, open));

        expander = (SettingsExpander)frame.Child;

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

        drag.Attach(host, frame, viewId);
        return frame;
    }

    /// <summary>A view's own glyph, or the shared view glyph for one without.</summary>
    private static string HeaderGlyph(ViewState view) => string.IsNullOrEmpty(view.Icon) ? Glyphs.View : view.Icon;

    /// <summary>
    /// [S-1832] A view group's "…" menu (and its right-click): Show on this display (off when it
    /// already shows there); Move up, Move down (off at the ends); Duplicate, Delete (danger, off for
    /// the last view). Read fresh each time it opens. "This display" is the settings window's ([S-1702]).
    /// </summary>
    private static List<ItemMenuEntry> ViewMenu(AppHost host, string viewId)
    {
        var views = host.State.Views;
        var index = views.FindIndex(v => v.Id == viewId);
        if (index < 0)
        {
            return [];
        }

        var surface = host.SettingsSurface();

        return
        [
            new(Glyphs.View, Str.WidgetsC_ViewSwitcher_Menu_Show.Text(), () => host.ShowView(surface!.Monitor.Id, viewId),
                Disabled: surface is null || surface.CurrentViewId == viewId),
            ItemMenuEntry.Separator,
            new(Glyphs.MoveUp, Str.ChildList_MoveUp.Text(), () => host.MoveView(viewId, -1), Disabled: index == 0),
            new(Glyphs.MoveDown, Str.ChildList_MoveDown.Text(), () => host.MoveView(viewId, +1), Disabled: index == views.Count - 1),
            ItemMenuEntry.Separator,
            new(Glyphs.Copy, Str.App_DuplicateView.Text(), () => host.DuplicateView(viewId, host.SettingsSurface()?.Monitor.Id), Quick: QuickCommand.Duplicate),
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
            defaultText: ViewSwitcherRules.Initials(view.Name),
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

    /// <summary>
    /// [S-1832] Dragging a view group onto another: the dragged view takes the target's place through
    /// <see cref="AppHost.ReorderView"/>, the view bar's own path, so the bar, the switchers and
    /// Ctrl+n follow. Only a drag that began on this page is accepted.
    /// <para>
    /// Started by hand from the pointer rather than through <c>CanDrag</c>: the group's header is a
    /// button, which takes the press for itself, and a drag that waits for an unhandled press never
    /// begins. A mouse press that travels past the threshold starts it; touch keeps panning the page.
    /// </para>
    /// </summary>
    private sealed class ViewDrag
    {
        /// <summary>The view being dragged, or null. Shared by the groups of one page.</summary>
        private string? _dragged;

        public void Attach(AppHost host, Border frame, string viewId)
        {
            Windows.Foundation.Point? pressed = null;
            frame.AllowDrop = true;

            frame.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler((_, args) =>
            {
                var point = args.GetCurrentPoint(frame);
                pressed = args.Pointer.PointerDeviceType == PointerDeviceType.Mouse && point.Properties.IsLeftButtonPressed
                    ? point.Position
                    : null;
            }), handledEventsToo: true);

            frame.AddHandler(UIElement.PointerReleasedEvent, new PointerEventHandler((_, _) => pressed = null), handledEventsToo: true);
            frame.AddHandler(UIElement.PointerCaptureLostEvent, new PointerEventHandler((_, _) => pressed = null), handledEventsToo: true);

            frame.AddHandler(UIElement.PointerMovedEvent, new PointerEventHandler((_, args) =>
            {
                if (pressed is not { } start || _dragged is not null)
                {
                    return;
                }

                var point = args.GetCurrentPoint(frame);
                if (!point.Properties.IsLeftButtonPressed)
                {
                    pressed = null;
                    return;
                }

                if (Math.Abs(point.Position.X - start.X) < DragThreshold && Math.Abs(point.Position.Y - start.Y) < DragThreshold)
                {
                    return;
                }

                // Not inside the group's own controls (the name box selects text by dragging).
                if (args.OriginalSource is DependencyObject source && InsideControl(source, frame))
                {
                    pressed = null;
                    return;
                }

                pressed = null;
                _dragged = viewId;
                _ = DragAsync(frame, point);
            }), handledEventsToo: true);

            frame.DragStarting += (_, args) => args.Data.RequestedOperation = DataPackageOperation.Move;
            frame.DropCompleted += (_, _) => _dragged = null;

            frame.DragOver += (_, args) =>
            {
                if (_dragged is { } dragged && dragged != viewId)
                {
                    args.AcceptedOperation = DataPackageOperation.Move;
                    args.DragUIOverride.IsGlyphVisible = false;
                    args.DragUIOverride.IsCaptionVisible = false;
                    args.Handled = true;
                }
            };

            frame.Drop += (_, args) =>
            {
                if (_dragged is not { } dragged || dragged == viewId)
                {
                    return;
                }

                args.Handled = true;
                _dragged = null;

                // After the drop has finished: the move rebuilds the page, and with it the group
                // this event is still being raised on.
                if (frame.DispatcherQueue is not { } queue || !queue.TryEnqueue(() =>
                {
                    try
                    {
                        host.ReorderView(dragged, viewId);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(Scope, "could not reorder views from the Views page", ex);
                    }
                }))
                {
                    Log.Error(Scope, "could not queue a view reorder from the Views page");
                }
            };
        }

        private async Task DragAsync(UIElement element, PointerPoint point)
        {
            try
            {
                await element.StartDragAsync(point);
            }
            catch (Exception ex)
            {
                Log.Warn(Scope, "a view group's drag could not start", ex);
            }
            finally
            {
                _dragged = null;
            }
        }

        /// <summary>
        /// Whether the press began somewhere a drag must not start: in one of the group's rows (a
        /// widget's card, the name box) or on a button or other control in its header (the "…").
        /// </summary>
        private static bool InsideControl(DependencyObject source, Border frame)
        {
            var rows = frame.Child is SettingsExpander expander ? expander.Items : null;

            for (var node = source; node is not null && node != frame; node = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(node))
            {
                if (node is TextBox or ToggleSwitch or ComboBox or Slider or Button)
                {
                    return true;
                }

                if (node is SettingsCard card && rows is not null && rows.Contains(card))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
