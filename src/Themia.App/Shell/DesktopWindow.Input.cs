using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.App.Interop;
using Themia.Core.Diagnostics;
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
        if (ReferenceEquals(_host.DesktopKeysSurface, this))
        {
            RouteDesktopKey(args, phase);
            return;
        }

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
        // [S-5542] Typing on empty desktop that holds the keys finds an item, as on Explorer's desktop.
        if (!EditMode && !args.Handled && ReferenceEquals(_host.DesktopKeysSurface, this) && !TextHasFocus()
            && IsDown(Windows.System.VirtualKey.Control) == IsDown(Windows.System.VirtualKey.Menu))
        {
            args.Handled = _host.Type(args.Character);
            return;
        }

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

    /// <inheritdoc cref="IWidgetCardHost.DeskPick"/>
    public bool DeskPick(WidgetCard card, bool ctrl, bool shift) => !_closed && !EditMode && _host.DeskPick(card.Model, ctrl, shift);

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

        // A click on a row's button leaves focus on the button, which still bubbles keys up to the
        // root; a click on plain text leaves focus wherever it was, so the root takes it.
        void FocusRoot()
        {
            if (!FocusIsIn(card))
            {
                _root.Focus(FocusState.Programmatic);
            }
        }

        if (_host.EditMode)
        {
            FocusRoot();
            return true;
        }

        // Asking is not enough: the surface has to actually be the foreground window before Windows
        // routes a keystroke to it. The click that granted the lease is what lets it ask.
        return TakeForeground(FocusRoot);
    }

    /// <summary>
    /// [S-802] The lease left <paramref name="card"/>: a caret left in it stops blinking, and the surface
    /// goes back to never taking focus. Its view drops its cursor through the session ([S-813]).
    /// </summary>
    internal void KeyboardLost(WidgetCard card)
    {
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
        DesktopLayer.Apply(_hwnd, interactive: _host.EditMode || ReferenceEquals(_host.KeyboardSurface, this) || ReferenceEquals(_host.DesktopKeysSurface, this));
        ApplyChromeless();
    }

    /// <summary>
    /// [S-5550] The empty desktop's hold on the keys came here or went: activatable and the foreground
    /// window while it holds them, focus on the root so its keys reach <see cref="RouteDesktopKey"/>.
    /// Returns false when Windows refused the foreground.
    /// </summary>
    internal bool DesktopKeysChanged()
    {
        if (_closed)
        {
            return false;
        }

        ApplyInteractivity();
        if (!ReferenceEquals(_host.DesktopKeysSurface, this))
        {
            return true;
        }

        return TakeForeground(() => _root.Focus(FocusState.Programmatic));
    }

    /// <summary>
    /// [S-34f] The one way a surface takes the foreground: never while a full-screen app covers its
    /// display from above (<see cref="DesktopZOrder.MayTakeForeground"/>), where it would still be
    /// under that app and only tell the shell and a dock that the app is no longer in front. True when
    /// the surface is the foreground window (or will be when the press that asked lets go).
    /// <para>
    /// [S-13017] Never in the middle of a press: activating the surface while a button is held (the first
    /// press after the user was in another app) takes the pointer from the card or the band that captured
    /// it, so the drag that press began died and a second press was needed. Explorer's desktop starts the
    /// drag on the first press; so does this: the foreground (and <paramref name="then"/>, the focus that
    /// needs it) waits till the press lets go (<see cref="RunForegroundAfterPress"/>), when it is still wanted.
    /// </para>
    /// </summary>
    internal bool TakeForeground(Action? then = null) => TakeForeground(shellDrag: false, then);

    /// <summary>
    /// [S-13025] A shell drag of our own (<see cref="Themia.Sdk.WinUI.ItemDrag"/>) is about to start from this surface: it
    /// becomes the foreground window now, press held or not (<see cref="PressForeground"/>), because OLE's drag loop
    /// follows the mouse through a capture that a background window's thread is not given, so the first drag after
    /// another app saw no mouse and died. Nothing has the pointer captured at that point (the card let it go, a fence item
    /// never took it), so the activation takes nothing from anyone. False (logged) when Windows refused.
    /// </summary>
    internal bool TakeForegroundForDrag()
    {
        var before = Win32.GetForegroundWindow();
        var taken = !_closed && TakeForeground(shellDrag: true, then: null);
        Log.Debug(Scope, $"a drag starts on {_monitor.GdiName}: the surface {(before == _hwnd ? "was" : taken ? "became" : "could not become")} the foreground window");
        return taken;
    }

    private bool TakeForeground(bool shellDrag, Action? then)
    {
        var isForeground = Win32.GetForegroundWindow() == _hwnd;
        var step = PressForeground.When(
            isForeground,
            mayTake: isForeground || DesktopZOrder.MayTakeForeground(_hwnd, _monitor.Bounds),
            pressHeld: !isForeground && PressHeld(),
            shellDrag);
        switch (step)
        {
            case ForegroundStep.Already:
                then?.Invoke();
                return true;
            case ForegroundStep.Never:
                return false;
            case ForegroundStep.AfterPress:
                _afterPress = then ?? (static () => { });
                return true;
        }

        if (!TakeForegroundNow())
        {
            return false;
        }

        then?.Invoke();
        return true;
    }

    /// <summary>[S-13017] What waits for the press to let go before the surface takes the foreground; null when nothing does.</summary>
    private Action? _afterPress;

    /// <summary>[S-13017] Whether a mouse button is down now (the physical state, so the order of the press's handlers does not matter).</summary>
    private bool PressHeld() => CheckButton is { } held ? held() : Win32.GetAsyncKeyState(0x01) < 0 || Win32.GetAsyncKeyState(0x02) < 0;

    /// <summary>
    /// A debug check that presses with input of its own (<c>--first-drag-check</c>): whether its button is down, in place
    /// of the physical mouse's; while set the surface never takes the foreground from the user's window, it counts the
    /// times it would have (<see cref="CheckForegroundTaken"/>, <see cref="CheckForegroundMidPress"/>). Null in the app.
    /// </summary>
    internal Func<bool>? CheckButton { get; set; }

    /// <summary>The times a check's surface would have taken the foreground.</summary>
    internal int CheckForegroundTaken { get; private set; }

    /// <summary>[S-13017] The times a check's surface would have taken the foreground while its button was down (the drag would die).</summary>
    internal int CheckForegroundMidPress { get; private set; }

    /// <summary>A debug check's release: what waits for the press to let go runs now (the root's release handler's step).</summary>
    internal void CheckLetGo() => RunForegroundAfterPress();

    /// <summary>The surface becomes the foreground window; a check's surface only counts it.</summary>
    private bool TakeForegroundNow()
    {
        if (CheckButton is { } held)
        {
            CheckForegroundTaken++;
            CheckForegroundMidPress += held() ? 1 : 0;
            return true;
        }

        return Win32.SetForegroundWindow(_hwnd);
    }

    /// <summary>
    /// [S-13017] The press let go (a release, or a move with no button down after a drag the shell ran): the surface
    /// takes the foreground it was asked for during the press, if the keys are still meant for it.
    /// </summary>
    private void RunForegroundAfterPress()
    {
        if (_afterPress is not { } then || _closed || PressHeld())
        {
            return;
        }

        _afterPress = null;
        if (!_host.EditMode && !ReferenceEquals(_host.KeyboardSurface, this) && !ReferenceEquals(_host.DesktopKeysSurface, this))
        {
            return;
        }

        if (Win32.GetForegroundWindow() == _hwnd || TakeForegroundNow())
        {
            then();
        }
        else
        {
            Log.Info(Scope, "the surface could not take the foreground after the press; the keys stay with the window that has it");
        }
    }

    /// <summary>[S-5550] A key while the empty desktop holds them: the desktop's items take it; Escape ends the hold.</summary>
    private void RouteDesktopKey(KeyRoutedEventArgs args, KeyPhase phase)
    {
        var shift = IsDown(Windows.System.VirtualKey.Shift);
        var key = WidgetKeys.FromVirtualKey((int)args.Key, shift, IsDown(Windows.System.VirtualKey.Control), IsDown(Windows.System.VirtualKey.Menu));
        switch (LeaseKeyRouting.Decide(phase, key, TextHasFocus()))
        {
            case LeaseKeyRoute.EndLease:
                // [S-11877] Escape clears the desktop's one selection, icons and cards, and ends the hold.
                _host.ClearDeskSelection();
                _host.DesktopKeysEnd(giveBack: true);
                args.Handled = true;
                break;

            case LeaseKeyRoute.ToHolder when key is { } widgetKey:
                args.Handled = _host.Key(widgetKey, shift, widgetId: null);
                break;
        }
    }
}
