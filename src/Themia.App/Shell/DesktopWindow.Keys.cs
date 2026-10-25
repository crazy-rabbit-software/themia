using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Geometry;
using Themia.Core.Model;
using Themia.Core.Shell;

namespace Themia.App.Shell;

/// <summary>
/// [S-70], [S-1970]..[S-1976] The edit-mode keys the surface carries out. Which key means what is
/// Core <see cref="EditKeys"/>'s; the nudge maths is <see cref="KeyboardNudge"/>'s and the Tab order
/// <see cref="SelectionRules.Cycle"/>'s.
/// </summary>
internal sealed partial class DesktopWindow
{
    /// <summary>
    /// [S-1972] A run of arrow presses not yet committed: one press-and-hold is one undo step, so
    /// the commit waits for the arrow to come up (or for any other command, or for the surface to
    /// lose the keyboard).
    /// </summary>
    private UndoAction? _nudging;

    /// <summary>Carry out one edit-mode key. False when it is not one, so it carries on.</summary>
    private bool RunEditKey(EditKeyAction action)
    {
        // [S-804] A text box (the picker's search) owns every key it is typed with.
        if (TextHasFocus())
        {
            return false;
        }

        // A key on the toolbar or a notice is that control's: arrows, Tab and Enter work its buttons.
        if (FocusOnChrome() && action.Command is EditCommand.Move or EditCommand.Resize or EditCommand.Cycle
            or EditCommand.FocusProperties or EditCommand.OpenMenu)
        {
            return false;
        }

        // [S-3523] The keyboard in an edit bar's menu is the menu's: the selection behind it is away,
        // so no key reaches it there but the undo and redo of what the menu changed, and F6.
        if (OpenBarMenuView is { } barMenu && FocusIsIn(barMenu) && action.Command is not (EditCommand.Undo or EditCommand.Redo or EditCommand.SwitchPane))
        {
            return false;
        }

        if (action.Command is not (EditCommand.Move or EditCommand.Resize))
        {
            FlushNudge();
        }

        switch (action.Command)
        {
            case EditCommand.Copy:
                _host.CopySelected();
                return true;

            case EditCommand.Cut:
                _host.CutSelected();
                return true;

            case EditCommand.Paste:
                // [S-74] The ghost appears on the display under the pointer, not the one holding the
                // keyboard. Those differ exactly when it matters: copy on one screen, point at the
                // other, paste.
                _host.Paste();
                return true;

            case EditCommand.Undo:
                _host.Undo();
                return true;

            case EditCommand.Redo:
                _host.Redo();
                return true;

            case EditCommand.SelectAll:
                // [S-1582] Everything on the display under the pointer.
                _host.SelectAllUnderCursor();
                return true;

            case EditCommand.Delete:
                _host.DeleteSelected();
                return true;

            case EditCommand.ShowView:
                ShowViewByNumber(action.ViewNumber);
                return true;

            case EditCommand.Back:
                BackOneStep();
                return true;

            case EditCommand.Move:
            case EditCommand.Resize:
                return Nudge(action);

            case EditCommand.Cycle:
                return CycleSelection(action.Backwards);

            case EditCommand.FocusProperties:
                return FocusProperties();

            case EditCommand.OpenMenu:
                return OpenMenuFromKeyboard();

            case EditCommand.SwitchPane:
                SwitchPane(action.Backwards);
                return true;

            case EditCommand.AddWidget:
                // [S-1998] The hotkey's own path: the display under the pointer, placement dropped.
                _host.OpenAddMenu();
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// [S-1583] Escape backs out one step: out of placing, then the selection, then editing. While
    /// placing, the placement's keyboard watch ([S-1594]) takes Escape before any window sees it; the
    /// first step here is for a surface that has the keyboard when that watch could not start.
    /// </summary>
    private void BackOneStep()
    {
        if (_host.Placing is not null)
        {
            _host.CancelPlacement();
        }
        else if (!_host.Selection.IsEmpty)
        {
            _host.Select(null);
            _root.Focus(FocusState.Keyboard);
        }
        else
        {
            _host.SetEditMode(false);
        }
    }

    /// <summary>[S-1970]/[S-1971] One arrow press on every selected widget of this display.</summary>
    private bool Nudge(EditKeyAction action)
    {
        var metrics = Metrics;
        var view = ViewForThisMonitor(_host.State);
        if (!metrics.IsUsable || _host.Placing is not null
            || !string.Equals(_host.Selection.ViewId, view.Id, StringComparison.Ordinal))
        {
            return false;
        }

        var cards = new List<WidgetCard>();
        foreach (var id in SelectionRules.InView(view, _host.Selection.Ids))
        {
            if (CardFor(id) is { Visibility: Visibility.Visible } card)
            {
                cards.Add(card);
            }
        }

        if (cards.Count == 0)
        {
            return false;
        }

        var snap = Design.GridSnap;
        var rects = action.Command == EditCommand.Move
            ? KeyboardNudge.Move([.. cards.Select(c => c.Model.Rect)], action.Direction, action.Step, metrics, snap)
            : [.. cards.Select(c => KeyboardNudge.Resize(c.Model.Rect, action.Direction, action.Step, metrics, snap))];

        for (var i = 0; i < cards.Count; i++)
        {
            cards[i].Model.Rect = rects[i];
            cards[i].SetLiveRect(rects[i]);
            PositionCard(cards[i], rects[i]);
        }

        _nudging = action.Command == EditCommand.Move ? UndoAction.Move : UndoAction.Resize;
        return true;
    }

    /// <summary>[S-1972] The run of nudges ends: persist it as one undo step.</summary>
    private void FlushNudge()
    {
        if (_nudging is not { } action)
        {
            return;
        }

        _nudging = null;
        _host.CommitGeometry(action);
    }

    private void OnKeyUp(object sender, KeyRoutedEventArgs args)
    {
        if (EditMode && EditKeys.IsArrow((int)args.Key))
        {
            FlushNudge();
        }
    }

    /// <summary>[S-1973] Tab: select the next widget of this display in reading order, and focus it.</summary>
    private bool CycleSelection(bool backwards)
    {
        if (_host.Placing is not null)
        {
            return true; // ignore: a ghost follows the pointer; Tab must not walk focus off the surface under it
        }

        var view = ViewForThisMonitor(_host.State);
        var current = string.Equals(_host.Selection.ViewId, view.Id, StringComparison.Ordinal) ? _host.Selection.Primary : null;
        if (SelectionRules.Cycle(view, current, backwards, HiddenIds()) is not { } next)
        {
            return true; // ignore: no widget on this display; focus stays on the surface
        }

        _host.Select(next);
        CardFor(next)?.Focus(FocusState.Keyboard);
        return true;
    }

    /// <summary>The selected widget's card on this display (the primary of a multi-selection).</summary>
    private WidgetCard? SelectedCardHere()
    {
        var view = ViewForThisMonitor(_host.State);
        return string.Equals(_host.Selection.ViewId, view.Id, StringComparison.Ordinal) && _host.Selection.Primary is { } id
            ? CardFor(id)
            : null;
    }

    /// <summary>[S-1974] The Menu key or Shift+F10: the selection's menu on its card, else the surface's.</summary>
    private bool OpenMenuFromKeyboard()
    {
        try
        {
            if (SelectedCardHere() is { Visibility: Visibility.Visible } card)
            {
                return ItemMenu.ShowAt(card, null, card.CardMenu());
            }

            var area = GridGeometry.AllowedPixels(Metrics);
            return OpenSurfaceMenu(new Windows.Foundation.Point(area.X + (area.Width / 2), area.Y + (area.Height / 2)));
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "could not open the menu from the keyboard", ex);
            return true;
        }
    }

    /// <summary>
    /// [S-1975] F6 walks the surface's panes: the widgets, the toolbar, the properties strip ([S-3707], while it shows), the edit bar's open menu ([S-3520]), the view bar (when it shows),
    /// the notices (while there are any), and round again; Shift+F6 the other way. A pane lands on its first control; the widgets
    /// on the selected one, else the surface itself.
    /// </summary>
    private void SwitchPane(bool backwards)
    {
        var panes = new List<FrameworkElement?> { null, _toolbar };
        if (_properties.Visibility == Visibility.Visible)
        {
            panes.Add(_properties);
        }

        if (OpenBarMenuView is { } barMenu)
        {
            panes.Add(barMenu);
        }

        if (_viewBar.Visibility == Visibility.Visible)
        {
            panes.Add(_viewBar);
        }

        if (_noticeLayer is { } notices && FocusManager.FindFirstFocusableElement(notices) is not null)
        {
            panes.Add(notices);
        }

        var at = panes.FindIndex(p => p is not null && FocusIsIn(p));
        var next = panes[((at < 0 ? 0 : at) + (backwards ? panes.Count - 1 : 1)) % panes.Count];

        if (ReferenceEquals(next, _viewBar))
        {
            _viewBar.FocusFirstChip();
        }
        else if (next is not null)
        {
            if (FocusManager.FindFirstFocusableElement(next) is Control first)
            {
                first.Focus(FocusState.Keyboard);
            }
        }
        else if (SelectedCardHere() is { } card)
        {
            card.Focus(FocusState.Keyboard);
        }
        else
        {
            _root.Focus(FocusState.Keyboard);
        }
    }

    /// <summary>
    /// [S-1974] Enter: the keyboard goes into the selection's properties menu, on its first control,
    /// as F6 reaches it ([S-1975]); Escape there hands it back. Nothing while the menu is away (no
    /// selection on this display, a drag), so Enter goes on to whatever else takes it.
    /// </summary>
    private bool FocusProperties()
    {
        if (_properties.Visibility != Visibility.Visible || FocusManager.FindFirstFocusableElement(_properties) is not Control first)
        {
            return false;
        }

        first.Focus(FocusState.Keyboard);
        return true;
    }

    /// <summary>Whether the keyboard is on the surface's chrome (the toolbar, the properties strip, the view bar, a notice) rather than the widgets.</summary>
    private bool FocusOnChrome() => FocusIsIn(_toolbar) || FocusIsIn(_properties) || (OpenBarMenuView is { } menu && FocusIsIn(menu)) || FocusIsIn(_viewBar) || (_noticeLayer is { } notices && FocusIsIn(notices));

    /// <summary>Whether the focused element is <paramref name="container"/> or inside it.</summary>
    private bool FocusIsIn(DependencyObject container)
    {
        if (_root.XamlRoot is not { } xamlRoot)
        {
            return false;
        }

        for (var node = FocusManager.GetFocusedElement(xamlRoot) as DependencyObject; node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (ReferenceEquals(node, container))
            {
                return true;
            }
        }

        return false;
    }
}
