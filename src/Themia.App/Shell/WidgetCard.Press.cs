using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Themia.App.Widgets;
using Themia.Core.Shell;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Windows.Foundation;

namespace Themia.App.Shell;

/// <summary>
/// [S-2008] [S-800] [S-13060] A press on the card outside edit mode: the keyboard lease it asks for, the desktop's selection
/// a Ctrl or Shift press shapes, and the card's move it may become, armed on the first press whatever part is under the
/// pointer. The pointer handlers hand over plain values, so a debug check can drive the same steps without a pointer of its
/// own (<see cref="CheckPress"/>).
/// </summary>
internal sealed partial class WidgetCard
{
    /// <summary>
    /// [S-13060] The card hears the moves a part inside it already handled (a button's held press, a text box selecting,
    /// a track waiting): the move past the threshold is the card's whoever took the press.
    /// </summary>
    private const bool HearsHandledMoves = true;

    /// <summary>[S-13060] The element a pointer press focused inside this card, until the press reaches the card's handler.</summary>
    private DependencyObject? _focusedByPress;

    /// <summary>[S-13060] The part the current press focused (a button, a text box at rest): a move gives the focus back.</summary>
    private DependencyObject? _pressFocus;

    /// <summary>
    /// [S-13060] A part takes the focus in its own press handler (a button, a text box), before the press reaches the card:
    /// the card notes it, so it knows a text box was at rest (the press focused it) from one being typed in.
    /// </summary>
    private void OnGettingFocus(UIElement sender, GettingFocusEventArgs args)
    {
        if (!_host.EditMode && args.FocusState == FocusState.Pointer)
        {
            _focusedByPress = args.NewFocusedElement;
        }
    }

    /// <summary>
    /// [S-800] [S-11879] [S-2008] [S-13060] Every press in the card outside edit mode, handled ones too: it asks for the
    /// keyboard lease; Ctrl or Shift with a left press puts the card in (or takes it out of) the desktop's one selection, as
    /// a desktop icon's press does, and the view never hears that press as its own; and the press arms the card's move,
    /// whatever part of the view took it first (<see cref="WidgetPress.Part"/>, <see cref="CardGestures.MovesLocked(bool, bool, bool, bool, bool, PressPart, bool)"/>):
    /// a row, a button, a slider's track, a chart, a text box at rest; never a thumb, a text being typed in or a part's own
    /// drag. True when the press is the selection's (the event is handled).
    /// </summary>
    private bool LeasePress(object? source, Point atSurface, bool left, bool ctrl, bool shift, bool handled)
    {
        var focused = _focusedByPress;
        _focusedByPress = null;
        if (_host.EditMode)
        {
            return false;
        }

        _host.KeyboardPress(this);

        if (_view is not IDesktopIconView && !_host.IsPreview
            && Themia.Core.Desktop.DesktopPicks.PicksOnPress(editMode: false, left, ctrl, shift, _host.WidgetsLocked)
            && _host.DeskPick(this, ctrl, shift))
        {
            _openArmed = false;
            _moveArmed = false;
            return true;
        }

        // [S-2000] A press a part took is that part's click, never the card's open.
        if (handled)
        {
            _openArmed = false;
        }

        var part = WidgetPress.Part(source as DependencyObject, this, focused);
        _moveArmed = CardGestures.MovesLocked(
            editMode: false,
            preview: _host.IsPreview,
            leftButton: left,
            ownsDrags: _view is IWidgetOwnsDrags,
            pinned: _host.Pins(this),
            part,
            handled);
        _moveStart = atSurface;
        _pressFocus = _moveArmed && focused is not null && Contains(focused) ? focused : null;
        return false;
    }

    /// <summary>
    /// [S-2000] A press no child took, outside edit mode: its click may open the widget's subject. The move it may become
    /// is armed where every press is heard (<see cref="LeasePress"/>).
    /// </summary>
    private void LockedPress(Point atSurface, bool left, bool modifier)
        => _openArmed = CardGestures.Opens(editMode: false, preview: _host.IsPreview, leftButton: left, modifier: modifier, showsSetup: ShowsSetup);

    /// <summary>
    /// [S-2008] A widget pressed and dragged on the locked desktop: past the dead zone the press becomes the card's move,
    /// from where it was pressed, so the widget (and the selection it is in, [S-11879]) follows the pointer at once, on the
    /// grid. Edit mode stays off: no bar, no page, no handles, no full grid; the local grid shows round where it lands
    /// ([S-11874]). <paramref name="take"/> takes the pointer for the card (from the part that held it: it forgets its
    /// press and never clicks). [S-13060] A part the press focused gives the focus back to the card: a drag never focused
    /// anything. False while the press is still a click, or is not one that may move.
    /// </summary>
    private bool LockedMove(Point atSurface, bool left, Func<bool> take)
    {
        if (!_moveArmed || _host.EditMode)
        {
            return false;
        }

        if (!left)
        {
            _moveArmed = false;
            return false;
        }

        if (!CardGestures.Travelled(atSurface.X - _moveStart.X, atSurface.Y - _moveStart.Y))
        {
            return false;
        }

        _moveArmed = false;
        _openArmed = false;

        _press = CardPress.Move;
        _activeHandle = null;
        _narrowOnRelease = false;
        _dragStartRect = LiveRect;
        _dragStartPoint = _moveStart;
        _moved = false;

        _dragging = take();
        if (_pressFocus is not null)
        {
            _pressFocus = null;

            // ignore: whether the card took it; the part let it go either way
            _ = Focus(FocusState.Programmatic);
        }

        if (_dragging)
        {
            _host.BeginMove(this);
        }
        else
        {
            _host.PressEnded(this);
        }

        return _dragging;
    }

    /// <summary>Whether <paramref name="node"/> is this card or inside it.</summary>
    private bool Contains(DependencyObject node)
    {
        for (var at = node; at is not null; at = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(at))
        {
            if (ReferenceEquals(at, this))
            {
                return true;
            }
        }

        return false;
    }

    // ---- debug checks ---------------------------------------------------------------------------

    /// <summary>
    /// A debug check's press (<c>--first-drag-check</c>), routed as XAML routes a left press to this card: from
    /// <paramref name="source"/> at <paramref name="atSurface"/> (the surface's DIPs), heard by the card's own handler only
    /// when no part inside took it (<paramref name="handled"/>, the check's reading of what is there), then by the lease's.
    /// </summary>
    internal void CheckPress(object source, Point atSurface, bool handled)
    {
        if (!handled && !_host.EditMode)
        {
            LockedPress(atSurface, left: true, modifier: false);
        }

        _ = LeasePress(source, atSurface, left: true, ctrl: false, shift: false, handled);
    }

    /// <summary>
    /// A debug check's move with the button held: heard only as the card hears moves (<paramref name="handled"/>: a part
    /// inside handles them). The check holds the pointer, so taking it always succeeds. True while the card follows it.
    /// </summary>
    internal bool CheckMove(Point atSurface, bool handled)
    {
        if (handled && !HearsHandledMoves)
        {
            return _dragging;
        }

        if (!_dragging && !LockedMove(atSurface, left: true, static () => true))
        {
            return false;
        }

        // ignore: a check never starts the shell's drag; its pointer is no real one
        _ = Follow(atSurface);
        return true;
    }

    /// <summary>A debug check's release: the held move lands, as the card's release handler lands it.</summary>
    internal void CheckRelease()
    {
        _moveArmed = false;
        if (_dragging)
        {
            EndDrag();
        }
    }
}
