using Themia.Core.Geometry;
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

    private const StackWindow Own = StackWindow.Own;
    private const StackWindow Hidden = StackWindow.Hidden;
    private const StackWindow Desktop = StackWindow.DesktopLayer;
    private const StackWindow Other = StackWindow.Other;

    /// <summary>
    /// [S-33f] The bug (2026-11-14, "themia sometimes clips over karpo"): the lift was the top of the
    /// topmost band, so a dock, which is a topmost window Show Desktop leaves on screen, ended up
    /// under the widgets. The stack is the one of that moment: the dock on top, the raised desktop
    /// under it, the surface still at rest below. The surface goes under the dock, never to the top.
    /// </summary>
    [Fact]
    public void S33f_a_lifted_surface_goes_under_a_dock_that_is_above_the_raised_desktop()
        => Assert.Equal(new LiftPlace(LiftMove.Under, 0), ZOrderPolicy.Lift([Other, Desktop, Hidden, Own], self: 3));

    /// <summary>[S-33f] Whatever is above the desktop (the dock, its flyout, Start, the taskbar), the place is under the lowest of them.</summary>
    [Fact]
    public void S33f_the_place_is_directly_above_the_desktop_under_every_other_window()
        => Assert.Equal(new LiftPlace(LiftMove.Under, 2), ZOrderPolicy.Lift([Other, Other, Other, Desktop, Own], self: 4));

    /// <summary>[S-33f] A surface the old lift (or an activation) left above a dock is out of place and comes down.</summary>
    [Fact]
    public void S33f_a_surface_above_another_programs_window_is_out_of_place()
        => Assert.Equal(new LiftPlace(LiftMove.Under, 1), ZOrderPolicy.Lift([Own, Other, Desktop], self: 0));

    /// <summary>[S-33c] Start, the taskbar and a dock above the lifted surface are where they belong: nothing is written.</summary>
    [Fact]
    public void S33f_start_the_taskbar_and_a_dock_above_keep_the_place()
        => Assert.Equal(LiftMove.Stay, ZOrderPolicy.Lift([Other, Hidden, Other, Own, Desktop], self: 3).Move);

    /// <summary>[S-33f] The other display's surface and hidden windows between a surface and the desktop cover nothing.</summary>
    [Fact]
    public void S33f_own_surfaces_and_hidden_windows_under_a_lifted_surface_keep_the_place()
        => Assert.Equal(LiftMove.Stay, ZOrderPolicy.Lift([Other, Own, Own, Hidden, Desktop, Other], self: 1).Move);

    /// <summary>[S-33f] Two surfaces: the second goes under the first, both above the desktop and under the dock.</summary>
    [Fact]
    public void S33f_a_second_surface_goes_under_the_first()
        => Assert.Equal(new LiftPlace(LiftMove.Under, 1), ZOrderPolicy.Lift([Other, Own, Desktop, Own], self: 3));

    /// <summary>[S-33f] From above, the window to go under is the one over the desktop, never the surface itself.</summary>
    [Fact]
    public void S33f_the_neighbour_is_never_the_surface_itself()
        => Assert.Equal(new LiftPlace(LiftMove.Under, 2), ZOrderPolicy.Lift([Other, Own, Other, Desktop], self: 1));

    /// <summary>
    /// [S-34b] The regression the busy cadence existed for: a secondary display's <c>WorkerW</c>
    /// rises into the topmost band after the surfaces were lifted, and the widgets on it vanish.
    /// The highest desktop window decides: the surface goes above that one.
    /// </summary>
    [Fact]
    public void S34b_the_desktop_rising_above_a_lifted_surface_moves_it_above_that_desktop()
        => Assert.Equal(new LiftPlace(LiftMove.Under, 0), ZOrderPolicy.Lift([Other, Desktop, Own, Desktop], self: 2));

    /// <summary>[S-33f] With the raised desktop as the very top window there is nothing to go under: the top.</summary>
    [Fact]
    public void S33f_a_desktop_that_is_the_top_window_puts_the_surface_on_top()
        => Assert.Equal(new LiftPlace(LiftMove.Top, -1), ZOrderPolicy.Lift([Desktop, Own], self: 1));

    /// <summary>[S-33f] A hidden, minimised or cloaked desktop window is not on screen; with none on screen nothing is written.</summary>
    [Fact]
    public void S33f_no_desktop_on_screen_writes_nothing()
        => Assert.Equal(LiftMove.Stay, ZOrderPolicy.Lift([Other, Hidden, Own, Other], self: 2).Move);

    /// <summary>
    /// [S-33f] The invariant, over every stack of up to six windows: carrying out what the rule says
    /// leaves the surface above the highest desktop window with no other program's window between
    /// them, and the rule then says Stay. So a lifted surface is never above a dock, whatever the
    /// stack was.
    /// </summary>
    [Fact]
    public void S33f_carrying_out_the_place_always_ends_under_every_other_window_and_above_the_desktop()
    {
        StackWindow[] kinds = [Own, Hidden, Desktop, Other];

        for (var length = 1; length <= 6; length++)
        {
            var total = (int)Math.Pow(kinds.Length, length - 1);
            for (var code = 0; code < total; code++)
            {
                for (var self = 0; self < length; self++)
                {
                    var stack = new List<(StackWindow Kind, int Id)>();
                    var rest = code;
                    for (var i = 0; i < length; i++)
                    {
                        if (i == self)
                        {
                            stack.Add((Own, -1));
                            continue;
                        }

                        stack.Add((kinds[rest % kinds.Length], i));
                        rest /= kinds.Length;
                    }

                    var place = ZOrderPolicy.Lift([.. stack.Select(w => w.Kind)], self);
                    if (place.Move != LiftMove.Stay)
                    {
                        var target = place.Move == LiftMove.Top ? -2 : stack[place.After].Id;
                        Assert.NotEqual(-1, target);
                        var me = stack[self];
                        stack.RemoveAt(self);
                        stack.Insert(place.Move == LiftMove.Top ? 0 : stack.FindIndex(w => w.Id == target) + 1, me);
                    }

                    var at = stack.FindIndex(w => w.Id == -1);
                    var desktop = stack.FindIndex(w => w.Kind == Desktop);
                    Assert.Equal(LiftMove.Stay, ZOrderPolicy.Lift([.. stack.Select(w => w.Kind)], at).Move);

                    if (desktop >= 0)
                    {
                        Assert.True(at < desktop, "the surface is above the desktop layer");
                        Assert.DoesNotContain(stack.GetRange(at + 1, desktop - at - 1), w => w.Kind == Other);
                    }
                }
            }
        }
    }

    /// <summary>[S-20a] At rest every z-order move asked of a surface becomes the anchor, the controller's own included.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void S20a_at_rest_every_move_becomes_the_anchor(bool ownPlacement)
        => Assert.Equal(ZMove.ToAnchor, ZOrderPolicy.Move(lifted: false, ownPlacement));

    /// <summary>
    /// [S-33g] The second way over a dock: while lifted, a click that activates the surface (edit mode,
    /// or empty desktop taking the keys, [S-5550]) asked Windows to raise it to the top of its band,
    /// and nothing rewrote that. Only the controller's own placement moves a lifted surface.
    /// </summary>
    [Fact]
    public void S33g_an_activation_does_not_move_a_lifted_surface()
        => Assert.Equal(ZMove.KeepOrder, ZOrderPolicy.Move(lifted: true, ownPlacement: false));

    [Fact]
    public void S33g_the_controllers_own_placement_moves_a_lifted_surface()
        => Assert.Equal(ZMove.Allow, ZOrderPolicy.Move(lifted: true, ownPlacement: true));

    private static readonly PixelRect Display1 = new(0, 0, 3840, 2160);

    private static AboveWindow Window(double left, double top, double right, double bottom, bool onScreen = true, bool own = false, bool maximized = false)
        => new(PixelRect.FromEdges(left, top, right, bottom), onScreen, own, maximized);

    /// <summary>
    /// The owner's report (2026-11-19, "in fullscreen mode when clicking themia sometimes karpo comes to
    /// front"): a surface that took the foreground under a full-screen video told Karpo the video was no
    /// longer its display's front, and Karpo's bars came back over it. A covered surface never asks.
    /// </summary>
    [Fact]
    public void S34f_a_surface_under_a_full_screen_app_never_takes_the_foreground()
    {
        Assert.False(ZOrderPolicy.MayTakeForeground(Display1, [Window(0, 0, 3840, 2160)]));
        Assert.False(ZOrderPolicy.MayTakeForeground(Display1, [Window(200, 200, 900, 700), Window(-10, -10, 3850, 2170)]));
    }

    [Fact]
    public void S34f_a_surface_takes_the_foreground_where_nothing_covers_its_display()
    {
        Assert.True(ZOrderPolicy.MayTakeForeground(Display1, []));
        Assert.True(ZOrderPolicy.MayTakeForeground(Display1, [Window(0, 0, 3777, 2160), Window(0, 0, 3840, 2159)]));

        // A full-screen app on the other display covers nothing here.
        Assert.True(ZOrderPolicy.MayTakeForeground(Display1, [Window(-3840, 0, 0, 2160)]));
    }

    [Fact]
    public void S34f_what_is_not_on_screen_its_own_windows_and_a_maximised_window_cover_nothing()
    {
        Assert.True(ZOrderPolicy.MayTakeForeground(Display1, [Window(0, 0, 3840, 2160, onScreen: false)]));
        Assert.True(ZOrderPolicy.MayTakeForeground(Display1, [Window(0, 0, 3840, 2160, own: true)]));
        Assert.True(ZOrderPolicy.MayTakeForeground(Display1, [Window(-8, -8, 3848, 2168, maximized: true)]));
    }

    [Fact]
    public void S34f_covering_means_every_edge_of_the_display()
    {
        Assert.True(ZOrderPolicy.Covers(PixelRect.FromEdges(-11, -11, 3851, 2171), Display1));
        Assert.False(ZOrderPolicy.Covers(PixelRect.FromEdges(0, 0, 3840, 2159), Display1));
        Assert.False(ZOrderPolicy.Covers(PixelRect.FromEdges(0, 0, 3840, 2160), new PixelRect(0, 0, 0, 0)));
    }
}
