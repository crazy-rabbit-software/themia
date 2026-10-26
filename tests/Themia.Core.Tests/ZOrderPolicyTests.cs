using Themia.Core.Shell;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// The z-order decision. Every case here is a bug that actually shipped in the original app, which
/// is why the decision is a pure function in the first place.
/// </summary>
public sealed class ZOrderPolicyTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    [InlineData(false, false, true)]
    public void ShowDesktopAlwaysPins(bool pinned, bool shellSurface, bool ownSurface)
        => Assert.Equal(ZAction.PinTopmost, ZOrderPolicy.Decide(true, pinned, shellSurface, ownSurface, applicationOnScreen: false));

    [Fact]
    public void DismissedShowDesktopDropsTheHeldPin()
        => Assert.Equal(ZAction.DropToAnchor, ZOrderPolicy.Decide(false, pinnedTopmost: true, shellSurfaceForeground: false, ownSurfaceForeground: false, applicationOnScreen: false));

    /// <summary>
    /// The bug: Win+D, then click a widget. The click gives the surface the foreground, Show Desktop
    /// stops reading as active, and dropping the pin buried the widgets under the still-elevated
    /// desktop — they vanished on the first click.
    /// </summary>
    [Fact]
    public void S28_ClickingAWidgetDuringShowDesktopHoldsThePin()
        => Assert.Equal(ZAction.HoldTopmost, ZOrderPolicy.Decide(false, pinnedTopmost: true, shellSurfaceForeground: false, ownSurfaceForeground: true, applicationOnScreen: false));

    /// <summary>Outside Show Desktop, clicking a widget is ordinary and must not lift anything.</summary>
    [Fact]
    public void S28_ClickingAWidgetWithoutAPinIsOrdinary()
        => Assert.Equal(ZAction.SelfHeal, ZOrderPolicy.Decide(false, pinnedTopmost: false, shellSurfaceForeground: false, ownSurfaceForeground: true, applicationOnScreen: false));

    /// <summary>
    /// Pressing the Windows key must not drop the pin: the desktop layer is still elevated, so
    /// dropping would bury the widgets, and dismissing Start hands the foreground to the taskbar
    /// rather than the desktop, so they would never come back.
    /// </summary>
    [Fact]
    public void ShellSurfaceHoldsTheHeldPin()
        => Assert.Equal(ZAction.HoldTopmost, ZOrderPolicy.Decide(false, pinnedTopmost: true, shellSurfaceForeground: true, ownSurfaceForeground: false, applicationOnScreen: false));

    [Fact]
    public void NothingHappeningSelfHeals()
        => Assert.Equal(ZAction.SelfHeal, ZOrderPolicy.Decide(false, pinnedTopmost: false, shellSurfaceForeground: false, ownSurfaceForeground: false, applicationOnScreen: false));

    /// <summary>
    /// A shell surface while nothing is pinned is an ordinary moment, not a reason to act: the user
    /// opening Start must not lift the widgets out of the desktop band.
    /// </summary>
    [Fact]
    public void ShellSurfaceWithoutAPinIsOrdinary()
        => Assert.Equal(ZAction.SelfHeal, ZOrderPolicy.Decide(false, pinnedTopmost: false, shellSurfaceForeground: true, ownSurfaceForeground: false, applicationOnScreen: false));

    /// <summary>
    /// [S-33e] The bug (issues #132, #167): Show Desktop, then the taskbar's Show Desktop button,
    /// Win+D or a three-finger swipe brings the windows back while the taskbar, Task View or a widget
    /// still owns the foreground. Holding the pin then left the widgets drawn over the restored
    /// windows until the user clicked one of them.
    /// </summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void S33e_windows_back_on_screen_drop_the_pin_whoever_has_the_foreground(bool shellSurface, bool ownSurface)
        => Assert.Equal(ZAction.DropToAnchor, ZOrderPolicy.Decide(false, pinnedTopmost: true, shellSurface, ownSurface, applicationOnScreen: true));

    /// <summary>[S-33e] Without a pin there is nothing to drop: an application on screen is ordinary.</summary>
    [Fact]
    public void S33e_an_application_on_screen_without_a_pin_is_ordinary()
        => Assert.Equal(ZAction.SelfHeal, ZOrderPolicy.Decide(false, pinnedTopmost: false, shellSurfaceForeground: true, ownSurfaceForeground: false, applicationOnScreen: true));

    /// <summary>[S-34c] Its own siblings, hidden windows and the desktop layer below a surface are its resting place.</summary>
    [Fact]
    public void S34c_the_anchor_holds_over_own_surfaces_hidden_windows_and_the_desktop_layer()
        => Assert.True(ZOrderPolicy.AnchorHolds(false, [StackWindow.Own, StackWindow.Hidden, StackWindow.DesktopLayer, StackWindow.Hidden]));

    [Fact]
    public void S34c_nothing_below_holds()
        => Assert.True(ZOrderPolicy.AnchorHolds(false, []));

    /// <summary>[S-34c] An application sent to the bottom lands under the widgets: they would cover it.</summary>
    [Fact]
    public void S34c_a_window_on_screen_below_the_surface_breaks_the_anchor()
        => Assert.False(ZOrderPolicy.AnchorHolds(false, [StackWindow.Own, StackWindow.Other]));

    /// <summary>[S-20] A topmost surface is above every application, wherever it sits in its band.</summary>
    [Fact]
    public void S34c_a_topmost_surface_is_not_at_rest()
        => Assert.False(ZOrderPolicy.AnchorHolds(true, []));

    /// <summary>
    /// [S-34b] The regression the busy cadence existed for: a secondary display's <c>WorkerW</c>
    /// rises into the topmost band after the surfaces were lifted, and the widgets on it vanish.
    /// </summary>
    [Fact]
    public void S34b_the_desktop_rising_above_a_lifted_surface_breaks_the_pin()
        => Assert.False(ZOrderPolicy.PinHolds(true, [StackWindow.Other, StackWindow.DesktopLayer]));

    /// <summary>[S-33c] Start, the taskbar or a hidden desktop helper above the lifted surface is not a reason to re-raise.</summary>
    [Fact]
    public void S34b_start_the_taskbar_and_hidden_windows_above_keep_the_pin()
        => Assert.True(ZOrderPolicy.PinHolds(true, [StackWindow.Other, StackWindow.Hidden, StackWindow.Own, StackWindow.Other]));

    /// <summary>[S-33d] <c>SetWindowPos(HWND_TOPMOST)</c> that did not take leaves the surface under the raised desktop.</summary>
    [Fact]
    public void S34b_a_surface_without_the_topmost_bit_is_not_lifted()
        => Assert.False(ZOrderPolicy.PinHolds(false, []));
}
