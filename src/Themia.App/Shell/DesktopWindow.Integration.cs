using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Themia.App.Interop;

namespace Themia.App.Shell;

/// <summary>
/// Desktop integration on the surface's side: being suspended while a full-screen app has the screen
/// ([S-928]), and cleaning up after a click the way a click on an ordinary window would ([S-938]).
/// </summary>
internal sealed partial class DesktopWindow
{
    /// <summary>Set while game mode holds this surface hidden with no widgets on it.</summary>
    private bool _suspended;

    /// <summary>Whether game mode currently holds this surface hidden.</summary>
    public bool IsSuspended => _suspended;

    /// <summary>
    /// [S-928] Take every widget down and hide the window.
    /// <para>
    /// Detaching, not just hiding, is what makes the suspension complete: every widget unsubscribes
    /// from its ticker, its sampler, its network feed and its watcher on detach, so nothing a widget
    /// started keeps running behind a game — by construction, with no per-widget pause to forget.
    /// A hidden window is also not composited. The window itself stays, so resuming costs no window
    /// creation and keeps the surface's place in the display plan.
    /// </para>
    /// </summary>
    public void Suspend()
    {
        if (_closed || _suspended)
        {
            return;
        }

        _suspended = true;

        // [S-802] A lease on a widget that is about to go away ends with it.
        _host.KeyboardEndOn(this);

        foreach (var card in _cards)
        {
            card.Detach();
            _widgetLayer.Children.Remove(card);
        }

        _cards.Clear();

        // Out of the z-order controller's hands while hidden: its cloak repair shows any cloaked
        // surface it manages, and a full-screen game is exactly what cloaks windows.
        DesktopZOrder.Forget(_hwnd);
        _appWindow?.Hide();
    }

    /// <summary>
    /// [S-929] Show the window again and rebuild its widgets from the state. Each widget subscribes
    /// afresh and fires at once, so nothing shows what it knew before the game.
    /// </summary>
    public void Resume()
    {
        if (_closed || !_suspended)
        {
            return;
        }

        _suspended = false;
        DesktopZOrder.Track(_hwnd);

        // Shows without activation, reconciles the z-order and lays the cards out from the state.
        ShowSurface();
    }

    /// <summary>
    /// [S-938] Hear every press on the surface, including ones a widget has already handled — a
    /// button in a widget is still a click that should close the Start menu.
    /// </summary>
    private void AttachShellCleanup()
        => _root.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(OnShellCleanupPressed), handledEventsToo: true);

    private void OnShellCleanupPressed(object sender, PointerRoutedEventArgs e)
        => ShellClicks.OnSurfacePressed(activatable: EditMode);
}
