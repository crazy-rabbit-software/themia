using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.Core.Diagnostics;
using Windows.System;

namespace Themia.App.Shell;

/// <summary>
/// [S-813]..[S-816] The surface's side of the interaction session: every press on it goes to the host
/// (which widget, on what), and the host closes this surface's popups when the user clicked elsewhere.
/// </summary>
internal sealed partial class DesktopWindow
{
    /// <summary>Hear every press on the surface, ones a widget or control already handled included.</summary>
    private void AttachSession()
        => _root.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler(OnSessionPressed), handledEventsToo: true);

    private void OnSessionPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_closed)
        {
            return;
        }

        var (card, target) = WidgetPress.Locate(e.OriginalSource as DependencyObject, e.Handled);
        var extending = (e.KeyModifiers & (VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift)) != 0;

        // Edit mode's presses select and move cards, not items: there only the popups matter.
        _host.SessionPress(this, EditMode ? null : card, target, extending);
    }

    /// <summary>
    /// [S-816] Close this surface's light-dismiss popups: the menus, flyouts and confirmations. A
    /// popup that stays until it is closed on purpose (the scratchpad's full-screen page) is left.
    /// </summary>
    internal void CloseLightDismissPopups()
    {
        if (_closed || _root.XamlRoot is not { } xamlRoot)
        {
            return;
        }

        try
        {
            foreach (var popup in VisualTreeHelper.GetOpenPopupsForXamlRoot(xamlRoot))
            {
                if (popup.IsLightDismissEnabled || popup.Child is FlyoutPresenter or MenuFlyoutPresenter or CommandBar)
                {
                    popup.IsOpen = false;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "closing the surface's popups threw", ex);
        }
    }
}
