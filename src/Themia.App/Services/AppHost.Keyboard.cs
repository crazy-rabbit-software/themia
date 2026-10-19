using Themia.App.Interop;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Widgets;

namespace Themia.App.Services;

/// <summary>
/// [S-800]..[S-805] The keyboard lease, app-wide: which widget the keyboard belongs to, and the
/// surface it is on.
/// <para>
/// One lease for every display, not one per surface, because a click on a widget on one display has
/// to end the lease a widget on another display holds — and a click on a <c>WS_EX_NOACTIVATE</c>
/// surface does not activate it, so the holder's surface never hears about that click by itself.
/// The rules are <see cref="KeyboardLease{T}"/>'s (Core, tested); this file carries out what it
/// decides: which surface is activatable, who is foreground, and which view hears Escape.
/// </para>
/// </summary>
internal sealed partial class AppHost
{
    private readonly KeyboardLease<WidgetCard> _keyboard = new();

    /// <summary>The surface the holder is drawn on; null while nothing holds the keyboard.</summary>
    private DesktopWindow? _keyboardSurface;

    /// <summary>
    /// The window that was foreground when the lease began, so Escape can hand the keyboard back to
    /// it. Never one of our own surfaces.
    /// </summary>
    private nint _keyboardReturn;

    /// <summary>The surface whose widget holds the keyboard, or null.</summary>
    public DesktopWindow? KeyboardSurface => _keyboardSurface;

    /// <summary>The widget holding the keyboard, or null.</summary>
    public WidgetCard? KeyboardHolder => _keyboard.Holder;

    /// <summary>[S-800] A press in a card outside edit mode.</summary>
    public void KeyboardPress(DesktopWindow surface, WidgetCard card)
    {
        if (_editMode)
        {
            return;
        }

        ApplyLease(surface, _keyboard.Press(card, card.TakesKeys));
    }

    /// <summary>[S-801] A text box in a card gained or lost focus.</summary>
    public void KeyboardTyping(DesktopWindow surface, WidgetCard card, bool typing)
        => ApplyLease(surface, _keyboard.Type(card, typing, card.TakesKeys));

    /// <summary>[S-802] Escape reached the holder's surface.</summary>
    public void KeyboardEscape()
    {
        ApplyLease(null, _keyboard.Escape(), escaped: true);
        FinishSession();
    }

    /// <summary>[S-802] A card left its surface (deleted, its view switched away, the surface rebuilt).</summary>
    public void KeyboardRemoved(WidgetCard card)
    {
        ApplyLease(null, _keyboard.Remove(card));
        RemoveFromSession(card);
    }

    /// <summary>
    /// [S-802] The lease ends if <paramref name="surface"/> holds it: the surface lost activation (a
    /// click on the desktop or in another app — the cheap signal that needs no global mouse hook),
    /// entered edit mode, or is closing.
    /// </summary>
    public void KeyboardEndOn(DesktopWindow surface)
    {
        if (ReferenceEquals(_keyboardSurface, surface))
        {
            ApplyLease(null, _keyboard.Clear());
        }

        LeaveSessionOn(surface);
    }

    private void ApplyLease(DesktopWindow? gainedOn, LeaseChange<WidgetCard> change, bool escaped = false)
    {
        if (change.IsNone)
        {
            return;
        }

        var lostOn = _keyboardSurface;

        if (change.Lost is { } lost)
        {
            _keyboardSurface = null;
            lostOn?.KeyboardLost(lost);

            // [S-813] The session drops the selection of the widget the user is working in, on every
            // way the lease ends for it too; a holder the session does not know (a text box focused
            // without a press) is told here instead, so no view keeps a cursor without the keyboard.
            if (!ReferenceEquals(lost, _session.Active))
            {
                LeaveWidget(lost, escaped);
            }
        }

        if (change.Gained is { } gained && gainedOn is not null)
        {
            // Remember where the user was only when the lease begins: a lease moving from one widget
            // to another must not remember our own surface as the place to go back to.
            if (lostOn is null)
            {
                var foreground = Win32.GetForegroundWindow();
                _keyboardReturn = _windows.Exists(w => w.Hwnd == foreground) ? 0 : foreground;
            }

            _keyboardSurface = gainedOn;

            if (!gainedOn.KeyboardGained(gained))
            {
                // Windows refused the foreground (its foreground lock): no key would reach the widget,
                // and a surface left activatable would take focus on the next click on any widget.
                Log.Warn(Scope, "the surface could not take the foreground; keyboard lease dropped");
                ApplyLease(null, _keyboard.Clear());
                return;
            }
        }

        if (change.GiveBack && lostOn is not null)
        {
            GiveKeyboardBack(lostOn);
        }

        if (_keyboard.Holder is null)
        {
            _keyboardReturn = 0;
        }
    }

    /// <summary>
    /// [S-802] Escape hands the keyboard back to the window the user was in before the click, when our
    /// surface is still the foreground one. A click elsewhere never does: that click already put the
    /// keyboard where the user wants it, and racing it (a launched app coming up) would steal it back.
    /// </summary>
    private void GiveKeyboardBack(DesktopWindow surface)
    {
        var target = _keyboardReturn;
        if (target == 0 || !Win32.IsWindow(target) || Win32.GetForegroundWindow() != surface.Hwnd)
        {
            return;
        }

        if (!Win32.SetForegroundWindow(target))
        {
            Log.Info(Scope, "could not hand the keyboard back to the previous window");
        }
    }
}
