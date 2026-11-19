using Themia.App.Widgets;
using Themia.Core.Shell;
using Themia.Sdk.WinUI;
using Windows.Foundation;

namespace Themia.App.Shell;

/// <summary>
/// [S-2008] [S-800] A press on the card outside edit mode: the keyboard lease it asks for, the desktop's selection a
/// Ctrl or Shift press shapes, and the card's move it may become. The pointer handlers hand over plain values, so a
/// debug check can drive the same steps without a pointer of its own (<see cref="CheckPress"/>).
/// </summary>
internal sealed partial class WidgetCard
{
    /// <summary>Whether the card hears the moves a part inside it already handled (a slider's, a text box's).</summary>
    private const bool HearsHandledMoves = false;

    /// <summary>
    /// [S-800] [S-11879] [S-2008] Every press in the card outside edit mode, handled ones too: it asks for the keyboard
    /// lease; Ctrl or Shift with a left press puts the card in (or takes it out of) the desktop's one selection, as a
    /// desktop icon's press does, and the view never hears that press as its own; a widget that is one button arms the
    /// move here, since its button takes the press first. True when the press is the selection's (the event is handled).
    /// </summary>
    private bool LeasePress(object? source, Point atSurface, bool left, bool ctrl, bool shift, bool handled)
    {
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

        // [S-2008] A button takes its press itself, so the card's own press handler never arms the move:
        // it is armed here, where handled presses are heard too. [S-12953] By the one rule: a locked button stays put.
        if (handled && _view is IWidgetButton
            && CardGestures.MovesLocked(editMode: false, _host.IsPreview, left, ownsDrags: false, pinned: _host.Pins(this)))
        {
            _moveArmed = true;
            _moveStart = atSurface;
        }

        return false;
    }

    /// <summary>
    /// [S-2000] [S-2008] A press no child took, outside edit mode: its click may open the widget's subject, and the same
    /// press, moved past the dead zone, moves the card (and what is selected with it) unless the view drags its own content.
    /// </summary>
    private void LockedPress(Point atSurface, bool left, bool modifier)
    {
        _openArmed = CardGestures.Opens(editMode: false, preview: _host.IsPreview, leftButton: left, modifier: modifier, showsSetup: ShowsSetup);
        _moveArmed = CardGestures.MovesLocked(
            editMode: false,
            preview: _host.IsPreview,
            leftButton: left,
            ownsDrags: _view is IWidgetOwnsDrags,
            pinned: _host.Pins(this));
        _moveStart = atSurface;
    }

    /// <summary>
    /// [S-2008] A widget pressed and dragged on the locked desktop: past the dead zone the press becomes the card's move,
    /// from where it was pressed, so the widget (and the selection it is in, [S-11879]) follows the pointer at once, on the
    /// grid. Edit mode stays off: no bar, no page, no handles, no full grid; the local grid shows round where it lands
    /// ([S-11874]). <paramref name="take"/> takes the pointer for the card. False while the press is still a click, or is
    /// not one that may move.
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

    // ---- debug checks ---------------------------------------------------------------------------

    /// <summary>
    /// A debug check's press (<c>--first-drag-check</c>), routed as XAML routes a left press to this card: from
    /// <paramref name="source"/> at <paramref name="atSurface"/> (the surface's DIPs), heard by the lease's handler, and by
    /// the card's own only when no part inside took it (<paramref name="handled"/>, the check's reading of what is there).
    /// </summary>
    internal void CheckPress(object source, Point atSurface, bool handled)
    {
        if (!LeasePress(source, atSurface, left: true, ctrl: false, shift: false, handled) && !handled && !_host.EditMode)
        {
            LockedPress(atSurface, left: true, modifier: false);
        }
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
