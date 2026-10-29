using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Themia.Core.Diagnostics;

namespace Themia.App.Shell;

/// <summary>
/// A fully transparent window backdrop.
/// <para>
/// A widget surface covers a whole monitor, so any backdrop that paints is a sheet over the user's
/// desktop. The SDK ships only Mica and Desktop Acrylic, and a window with <c>SystemBackdrop = null</c>
/// composes opaque black — which is what made edit mode (where the click-through region is dropped)
/// black out the screen.
/// </para>
/// <para>
/// <see cref="SystemBackdrop"/> is the supported extension point, so this installs a transparent
/// composition brush on the window's backdrop target. Note the brush type: the projection types
/// <c>ICompositionSupportsSystemBackdrop.SystemBackdrop</c> as a <b>system</b>
/// <c>Windows.UI.Composition.CompositionBrush</c>, not the <c>Microsoft.UI.Composition</c> one, so
/// the brush has to come from a system compositor.
/// </para>
/// </summary>
internal sealed partial class TransparentBackdrop : SystemBackdrop
{
    private const string Scope = "backdrop";

    private Windows.UI.Composition.Compositor? _compositor;

    protected override void OnTargetConnected(
        Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop connectedTarget,
        XamlRoot xamlRoot)
    {
        base.OnTargetConnected(connectedTarget, xamlRoot);

        try
        {
            // The system compositor needs a system dispatcher queue on this thread; WinUI's own
            // dispatcher queue is a different type and does not satisfy it.
            if (!Services.SystemDispatcherQueue.EnsureOnCurrentThread())
            {
                Log.Error(Scope, "no system dispatcher queue; the surface will not be see-through");
                return;
            }

            _compositor ??= new Windows.UI.Composition.Compositor();
            connectedTarget.SystemBackdrop = _compositor.CreateColorBrush(ShellColours.None);
        }
        catch (Exception ex)
        {
            // Failing here leaves the window opaque, i.e. a black sheet over the whole desktop.
            // Very visible, so never silent.
            Log.Error(Scope, "could not install a transparent backdrop brush; the surface will not be see-through", ex);
        }
    }

    protected override void OnTargetDisconnected(Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop disconnectedTarget)
    {
        base.OnTargetDisconnected(disconnectedTarget);

        // Release the brush with the target; leaving it attached keeps composition objects alive for
        // a window that is going away.
        disconnectedTarget.SystemBackdrop = null;
        _compositor = null;
    }
}
