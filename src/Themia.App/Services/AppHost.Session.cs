using Themia.App.Interop;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Widgets;

namespace Themia.App.Services;

/// <summary>
/// [S-813]..[S-816] The interaction session, app-wide: the widget the user is working in, and the
/// watch for a press anywhere else that closes every popup and clears that widget's selection. The
/// rules are <see cref="WidgetSession{T}"/>'s (Core, tested); this file carries them out.
/// <para>
/// One session for every display, like the lease: a press on a widget on one display has to clear a
/// selection and close a menu on another, and neither surface hears that press by itself.
/// </para>
/// </summary>
internal sealed partial class AppHost
{
    private readonly WidgetSession<WidgetCard> _session = new();

    /// <summary>The surface the active widget is on; null while no widget is active.</summary>
    private DesktopWindow? _sessionSurface;

    /// <summary>Created on the first press (renders never press), disposed with the host.</summary>
    private OutsideClickWatcher? _outsideWatch;

    /// <summary>[S-814] A press on <paramref name="surface"/>: in <paramref name="card"/>, or on the chrome (null).</summary>
    public void SessionPress(DesktopWindow surface, WidgetCard? card, PressTarget target, bool extending)
    {
        var typing = card is not null && _keyboard.Typing && ReferenceEquals(_keyboard.Holder, card);
        ApplySession(surface, card is null ? null : surface, _session.Press(card, target, extending, typing));
    }

    /// <summary>
    /// [S-815] A press outside every surface, or another app took the foreground: the lease ends too
    /// (without handing the keyboard back: the click put it where the user wants, [S-802]).
    /// </summary>
    private void OnOutsidePress()
    {
        ApplyLease(null, _keyboard.Clear());
        ApplySession(null, null, _session.Outside());
    }

    /// <summary>[S-815] Escape ended the session.</summary>
    private void FinishSession() => ApplySession(null, null, _session.Finish());

    /// <summary>[S-815] <paramref name="surface"/> lost the keyboard, entered edit mode or is closing.</summary>
    private void LeaveSessionOn(DesktopWindow surface)
    {
        if (ReferenceEquals(_sessionSurface, surface))
        {
            ApplySession(null, null, _session.Leave());
        }
    }

    /// <summary>[S-815] A card left its surface: nothing to clear later.</summary>
    private void RemoveFromSession(WidgetCard card)
    {
        _session.Remove(card);
        if (_session.Active is null)
        {
            _sessionSurface = null;
        }
    }

    private void ApplySession(DesktopWindow? pressedOn, DesktopWindow? activeOn, SessionChange<WidgetCard> change)
    {
        _sessionSurface = _session.Active is null ? null : activeOn ?? _sessionSurface;

        if (change.IsNone)
        {
            return;
        }

        // The one place a view drops its selection or cursor ([S-802]): Escape, guarded by the card.
        change.Left?.SendKey(WidgetKey.Escape);
        change.Reset?.SendKey(WidgetKey.Escape);

        switch (change.Popups)
        {
            case PopupDismissal.OtherSurfaces:
                foreach (var window in _windows)
                {
                    if (!ReferenceEquals(window, pressedOn))
                    {
                        window.CloseLightDismissPopups();
                    }
                }

                break;

            case PopupDismissal.Everywhere:
                foreach (var window in _windows)
                {
                    window.CloseLightDismissPopups();
                }

                break;
        }

        if (change.Arm)
        {
            OutsideWatch()?.Arm();
        }

        if (change.Disarm)
        {
            _outsideWatch?.Disarm();
        }
    }

    private OutsideClickWatcher? OutsideWatch()
    {
        if (_outsideWatch is null && !_disposed)
        {
            try
            {
                _outsideWatch = new OutsideClickWatcher(_dispatcher, OnOutsidePress);
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "could not start watching for clicks outside; menus close only by their own light dismiss", ex);
            }
        }

        return _outsideWatch;
    }

    private void StopOutsideWatch()
    {
        _outsideWatch?.Dispose();
        _outsideWatch = null;
    }
}
