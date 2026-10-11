using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.App.Interop;
using Themia.Core.Model;
using Themia.Core.Shell;
using Themia.Core.Widgets;

namespace Themia.App.Shell;

/// <summary>
/// Keyboard: the edit-mode shortcuts, and the keyboard lease ([S-800]..[S-805]) — when the surface
/// may take focus for a widget, and which widget the keys go to.
/// <para>
/// The surface carries <c>WS_EX_NOACTIVATE</c> so that clicking a widget never pulls focus off the
/// user's work — right for every widget except the ones you type into or navigate with the keys.
/// For those the host's lease names one widget; the surface it is on drops <c>NOACTIVATE</c> and
/// becomes the foreground window for exactly as long as the lease lasts, and the z-order controller
/// keeps it on the desktop regardless, so it still never rises above another window. Edit mode is
/// the other reason for the same flag, so both go through <see cref="ApplyInteractivity"/>.
/// </para>
/// </summary>
internal sealed partial class DesktopWindow
{
    /// <summary>The surface's window, for the host's lease to compare against the foreground window.</summary>
    internal nint Hwnd => _hwnd;

    private void OnKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (!EditMode)
        {
            RouteLeaseKey(args, KeyPhase.Bubble);
            return;
        }

        // [S-70], [S-1970]..[S-1976] One key map (Core EditKeys); the surface only carries it out.
        var action = EditKeys.Decide(
            (int)args.Key,
            IsDown(Windows.System.VirtualKey.Shift),
            IsDown(Windows.System.VirtualKey.Control),
            IsDown(Windows.System.VirtualKey.Menu));

        if (action.Command != EditCommand.None && RunEditKey(action))
        {
            args.Handled = true;
        }
    }

    /// <summary>
    /// [S-904] Show the n-th view on the display under the pointer — the same display a paste lands
    /// on ([S-74]) — or on this one when the pointer is on none.
    /// </summary>
    private void ShowViewByNumber(int number)
    {
        if (ViewOps.ForShortcut(_host.State.Views, number) is { } viewId)
        {
            var surface = _host.SurfaceUnderCursor() ?? this;
            _host.ShowView(surface.Monitor.Id, viewId);
        }
    }

    /// <summary>
    /// [S-860] The tunnelling pass, root to focused element: the lease holder's view sees its keys
    /// before any control inside its card. A <c>ScrollViewer</c> in a widget handles the arrows, Page
    /// Up/Down and Home/End in the bubbling pass, so a lease that only listened there never got them.
    /// A key the view does not use carries on to the focused control.
    /// </summary>
    private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs args)
    {
        if (!EditMode)
        {
            RouteLeaseKey(args, KeyPhase.Preview);
        }
    }

    /// <summary>
    /// [S-803]/[S-804]/[S-860] A key outside edit mode, for the holder's widget if it is on this
    /// surface; <see cref="LeaseKeyRouting"/> decides. Escape ends the lease in the bubble, after a
    /// text box that wanted it (one that clears on Escape) had its turn.
    /// </summary>
    private void RouteLeaseKey(KeyRoutedEventArgs args, KeyPhase phase)
    {
        if (!ReferenceEquals(_host.KeyboardSurface, this) || _host.KeyboardHolder is not { } holder)
        {
            return;
        }

        var key = WidgetKeys.FromVirtualKey(
            (int)args.Key,
            IsDown(Windows.System.VirtualKey.Shift),
            IsDown(Windows.System.VirtualKey.Control),
            IsDown(Windows.System.VirtualKey.Menu));

        switch (LeaseKeyRouting.Decide(phase, key, TextHasFocus()))
        {
            case LeaseKeyRoute.EndLease:
                _host.KeyboardEscape();
                args.Handled = true;
                break;

            case LeaseKeyRoute.ToHolder when key is { } widgetKey:
                args.Handled = holder.SendKey(widgetKey);
                break;
        }
    }

    /// <summary>
    /// [S-1997] A character typed outside edit mode goes to the lease holder on this surface for its
    /// type-ahead ([S-1980]) — never while a text box has focus (the box types it, [S-804]), and never
    /// a Ctrl or Alt chord's (those are not typing; AltGr, which is both, is).
    /// </summary>
    private void OnCharacterReceived(UIElement sender, CharacterReceivedRoutedEventArgs args)
    {
        if (EditMode || args.Handled || !ReferenceEquals(_host.KeyboardSurface, this) || _host.KeyboardHolder is not { } holder
            || TextHasFocus() || IsDown(Windows.System.VirtualKey.Control) != IsDown(Windows.System.VirtualKey.Menu))
        {
            return;
        }

        args.Handled = holder.SendText(args.Character);
    }

    private static bool IsDown(Windows.System.VirtualKey key)
        => InputKeyboardSource.GetKeyStateForCurrentThread(key).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

    /// <summary>[S-804] Whether a text box on this surface has focus: then the caret owns the arrows.</summary>
    private bool TextHasFocus()
        => _root.XamlRoot is { } xamlRoot
            && FocusManager.GetFocusedElement(xamlRoot) is TextBox or PasswordBox or RichEditBox or AutoSuggestBox;

    /// <summary>[S-801] A widget's text box gained or lost focus: the lease, held for typing.</summary>
    public void RequestTextInput(WidgetCard card, bool wanted)
    {
        if (_closed)
        {
            return;
        }

        _host.KeyboardTyping(this, card, wanted);
    }

    /// <summary>[S-800] A press in a card outside edit mode.</summary>
    public void KeyboardPress(WidgetCard card)
    {
        if (_closed)
        {
            return;
        }

        _host.KeyboardPress(this, card);
    }

    /// <summary>
    /// [S-800] The host's lease gave <paramref name="card"/> the keyboard: become activatable and the
    /// foreground window, and put focus where the holder's keys reach this surface's handler.
    /// Returns false when Windows refused the foreground, so the host can drop the lease.
    /// </summary>
    internal bool KeyboardGained(WidgetCard card)
    {
        if (_closed)
        {
            return false;
        }

        ApplyInteractivity();

        // Asking is not enough: the surface has to actually be the foreground window before Windows
        // routes a keystroke to it. The click that granted the lease is what lets it ask.
        if (!_host.EditMode && Win32.GetForegroundWindow() != _hwnd && !Win32.SetForegroundWindow(_hwnd))
        {
            return false;
        }

        // A click on a row's button leaves focus on the button, which still bubbles keys up to the
        // root; a click on plain text leaves focus wherever it was, so the root takes it.
        if (!FocusIsIn(card))
        {
            _root.Focus(FocusState.Programmatic);
        }

        return true;
    }

    /// <summary>
    /// [S-802] The lease left <paramref name="card"/>: its view drops its cursor (Escape), a caret left
    /// in it stops blinking, and the surface goes back to never taking focus.
    /// </summary>
    internal void KeyboardLost(WidgetCard card)
    {
        card.SendKey(WidgetKey.Escape);

        if (_closed)
        {
            return;
        }

        if (FocusIsIn(card))
        {
            _root.Focus(FocusState.Programmatic);
        }

        ApplyInteractivity();
    }

    /// <summary>
    /// [S-802] Losing activation ends the lease: the user clicked the desktop or another app, or
    /// switched away with Alt+Tab. That is the "click outside" signal — no global mouse hook, because
    /// while the lease lasts this surface is the foreground window and anything that takes the
    /// keyboard elsewhere takes that away first.
    /// </summary>
    private void OnWindowActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState == WindowActivationState.Deactivated)
        {
            // [S-1972] An arrow held while the keyboard went elsewhere never sees its key-up.
            FlushNudge();
            _host.KeyboardEndOn(this);
        }
    }

    /// <summary>
    /// The one place that decides whether the surface takes focus. Edit mode and the keyboard lease
    /// are two reasons for the same thing, so they share the flag rather than each setting the style
    /// themselves and overwriting each other.
    /// </summary>
    private void ApplyInteractivity()
    {
        DesktopLayer.Apply(_hwnd, interactive: _host.EditMode || ReferenceEquals(_host.KeyboardSurface, this));
        ApplyChromeless();
    }
}
