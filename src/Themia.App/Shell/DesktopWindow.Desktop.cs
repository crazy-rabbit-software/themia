using Microsoft.UI.Xaml.Input;

namespace Themia.App.Shell;

/// <summary>[S-5501] [S-5502] The surface as the desktop itself, while Themia's items show.</summary>
internal sealed partial class DesktopWindow
{
    /// <summary>
    /// [S-5502] A press on empty desktop outside edit mode. It reaches the surface only under Themia's
    /// items, whose region covers the work area ([S-5501]); a press on a card is the card's. It ends a
    /// keyboard lease held on this surface, as a click on the desktop would.
    /// </summary>
    private void OnDesktopPressed(PointerRoutedEventArgs e)
    {
        if (_closed || !_host.ShowsDesktopItems || !ReferenceEquals(e.OriginalSource, _root))
        {
            return;
        }

        _host.KeyboardEndOn(this);
    }
}
