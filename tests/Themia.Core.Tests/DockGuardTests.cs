using System.Text.RegularExpressions;
using Themia.Core.Geometry;
using Themia.Tests;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-20] [S-21] [S-33f] No state of Themia paints or takes input where another program holds a dock:
/// the surface is cut to its display's work area by one rule and one call, nothing but the z-order
/// controller can raise it, and every window that is not the surface is on a list a reviewer reads.
/// <para>
/// Source guards beside the pure rules (<see cref="ZOrderPolicyTests"/>, the region below): what the
/// rules cannot hold is somebody adding a second way, a region set elsewhere, a window made topmost,
/// a popup in a window of its own, and that is a mistake a scan catches mechanically.
/// </para>
/// </summary>
public sealed partial class DockGuardTests
{
    private static readonly DeviceRect Window = new(0, 0, 1920, 1080);

    /// <summary>A dock along the left edge, registered as an appbar: the work area leaves its strip out, and so does the region.</summary>
    [Fact]
    public void S21_the_region_is_the_work_area_relative_to_the_window()
        => Assert.Equal(new DeviceRect(64, 0, 1856, 1032), GridGeometry.SurfaceRegion(Window, new DeviceRect(64, 0, 1856, 1032)));

    /// <summary>The region is relative to the window, which sits at its display's corner: a display left of or above the primary has a negative one.</summary>
    [Fact]
    public void S21_a_display_off_the_origin_is_measured_from_the_windows_own_corner()
    {
        var window = new DeviceRect(-2560, -200, 2560, 1440);

        Assert.Equal(new DeviceRect(72, 0, 2488, 1392), GridGeometry.SurfaceRegion(window, new DeviceRect(-2488, -200, 2488, 1392)));
    }

    /// <summary>
    /// [S-849] A display-scale change leaves the window, for a moment, on a rect Windows scaled from the
    /// old one, spilling onto the neighbour: the region never reaches past the work area of its own
    /// display, wherever the window is.
    /// </summary>
    [Fact]
    public void S21_the_region_never_reaches_past_the_work_area_whatever_the_window_is()
    {
        var spilled = new DeviceRect(-400, 0, 2880, 1620);
        var region = GridGeometry.SurfaceRegion(spilled, new DeviceRect(64, 0, 1856, 1032));

        Assert.Equal(new DeviceRect(464, 0, 1856, 1032), region);
    }

    /// <summary>The invariant over a grid of work areas: the region, put back on screen, is always inside the work area.</summary>
    [Fact]
    public void S21_the_region_on_screen_is_always_inside_the_work_area()
    {
        foreach (var left in new[] { 0, 48, 64, 300 })
        {
            foreach (var top in new[] { 0, 48 })
            {
                foreach (var right in new[] { 0, 48, 200 })
                {
                    foreach (var bottom in new[] { 0, 48, 72 })
                    {
                        var work = new DeviceRect(left, top, 1920 - left - right, 1080 - top - bottom);
                        var region = GridGeometry.SurfaceRegion(Window, work);

                        Assert.Equal(work, new DeviceRect(region.X + Window.X, region.Y + Window.Y, region.Width, region.Height));
                    }
                }
            }
        }
    }

    /// <summary>Windows mid-change can report a work area that is not on the display at all: the whole window, until the display pass that follows.</summary>
    [Fact]
    public void S21_a_work_area_off_the_window_gives_the_whole_window()
    {
        Assert.Equal(new DeviceRect(0, 0, 1920, 1080), GridGeometry.SurfaceRegion(Window, new DeviceRect(5000, 5000, 100, 100)));
        Assert.Equal(new DeviceRect(0, 0, 1920, 1080), GridGeometry.SurfaceRegion(Window, new DeviceRect(0, 0, 0, 0)));
    }

    // ---- source guards --------------------------------------------------------------------------

    private static readonly string App = RepoFiles.At("src", "Themia.App");

    private static string Relative(string file) => Path.GetRelativePath(RepoFiles.Root, file).Replace('\\', '/');

    /// <summary>The app's and the SDKs' and the modules' sources, the render flags' off-screen windows left out: they never reach a desktop.</summary>
    private static IEnumerable<string> Shipped()
        => RepoFiles.CSharp(RepoFiles.Src).Where(f => !Relative(f).Contains("/Shell/Gallery/", StringComparison.Ordinal));

    /// <summary>The files, of <see cref="Shipped"/>, with a line of code (not a comment) matching <paramref name="pattern"/>.</summary>
    private static List<string> FilesWith(Regex pattern)
        => [.. Shipped()
            .Where(f => File.ReadLines(f).Any(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal) && pattern.IsMatch(line)))
            .Select(Relative)
            .Order(StringComparer.Ordinal)];

    [GeneratedRegex(@"\bSetWindowRgn\s*\(")]
    private static partial Regex SetsRegion();

    [GeneratedRegex(@"\bSetHitRegion\s*\(")]
    private static partial Regex SetsHitRegion();

    [GeneratedRegex(@"\bHwndTopmost\b|IsAlwaysOnTop\s*=\s*true")]
    private static partial Regex RaisesTopmost();

    [GeneratedRegex(@"\bWsExTopmost\s*\|")]
    private static partial Regex MakesTopmostWindow();

    [GeneratedRegex(@"ShouldConstrainToRootBounds\s*=\s*false")]
    private static partial Regex WindowedPopup();

    /// <summary>
    /// [S-21] One call sets a window's region (<c>DesktopLayer.SetHitRegion</c>), one place calls it
    /// (<c>DesktopWindow.ApplyHitRegion</c>), and what it sets is the pure rule's rectangle. A second
    /// region anywhere is how a surface gets wider than its work area again.
    /// </summary>
    [Fact]
    public void S21_the_surfaces_region_is_set_in_one_place_from_the_one_rule()
    {
        Assert.Equal(["src/Themia.App/Interop/DesktopLayer.cs", "src/Themia.App/Interop/Win32.cs"], FilesWith(SetsRegion()));
        Assert.Equal(["src/Themia.App/Interop/DesktopLayer.cs", "src/Themia.App/Shell/DesktopWindow.Layout.cs"], FilesWith(SetsHitRegion()));

        var layout = File.ReadAllText(Path.Combine(App, "Shell", "DesktopWindow.Layout.cs"));
        Assert.Matches(@"DesktopLayer\.SetHitRegion\(_hwnd,\s*ToRegionRect\(GridGeometry\.SurfaceRegion\(", layout);
    }

    /// <summary>
    /// [S-851] The region is applied when the window is made and on every path a display's bounds or
    /// work area reach the surface by: a path added without it leaves the surface on the old work area.
    /// </summary>
    [Fact]
    public void S851_every_path_that_takes_a_new_work_area_cuts_the_surface_again()
    {
        var placement = File.ReadAllText(Path.Combine(App, "Shell", "DesktopWindow.Placement.cs"));

        foreach (var method in new[] { "private void ConfigureWindow()", "public void UpdateMonitor(MonitorTarget next)", "public void EnsurePlacement()" })
        {
            var from = placement.IndexOf(method, StringComparison.Ordinal);
            Assert.True(from >= 0, $"{method} is gone from DesktopWindow.Placement.cs: where is the region applied now?");
            var body = placement[from..placement.IndexOf("\n    }", from, StringComparison.Ordinal)];
            Assert.Contains("ApplyHitRegion();", body, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// [S-848] What can change a work area requests a display pass ([S-844], which re-cuts every
    /// surface) and a z-order check: a display change, a scale change, the work area itself (a dock
    /// taking or giving back its strip arrives as this one) and an Explorer restart.
    /// </summary>
    [Fact]
    public void S848_every_notice_that_can_change_a_work_area_reaches_the_display_pass()
    {
        var command = File.ReadAllText(Path.Combine(App, "Interop", "CommandWindow.cs"));
        var raised = command.IndexOf("self.DisplaysChanged?.Invoke();", StringComparison.Ordinal);
        Assert.True(raised >= 0);
        var condition = command[command.LastIndexOf("if (", raised, StringComparison.Ordinal)..raised];

        foreach (var notice in new[] { "msg == WmDisplayChange", "msg == WmDpiChanged", "msg == WmSettingChange && wParam == SpiSetWorkArea", "msg == _taskbarCreatedMessage" })
        {
            Assert.Contains(notice, condition, StringComparison.Ordinal);
        }

        var host = File.ReadAllText(Path.Combine(App, "Services", "AppHost.cs"));
        Assert.Contains("_command.DisplaysChanged += RequestDisplayPass;", host, StringComparison.Ordinal);
        Assert.Contains("_command.DisplaysChanged += DesktopZOrder.Refresh;", host, StringComparison.Ordinal);
    }

    /// <summary>
    /// [S-20] [S-33f] Only the z-order controller can put a window of ours in the topmost band, and the
    /// one window made topmost is the eyedropper's (the user holds it, it covers the screen on purpose
    /// and goes with the click). Anything else topmost is above a dock.
    /// </summary>
    [Fact]
    public void S20_nothing_but_the_z_order_controller_raises_a_window()
    {
        Assert.Equal(["src/Themia.App/Interop/DesktopZOrder.cs", "src/Themia.App/Interop/Win32.cs"], FilesWith(RaisesTopmost()));
        Assert.Equal(["src/Themia.App/Interop/ScreenColorPicker.cs"], FilesWith(MakesTopmostWindow()));
    }

    /// <summary>
    /// [S-33g] Inside the controller the top of the topmost band is reached only through
    /// <c>Placing</c>, and the lift only through <c>ZOrderPolicy.Lift</c>: a direct pin is the bug
    /// this guards (the surfaces above a dock for as long as Windows showed the desktop).
    /// </summary>
    [Fact]
    public void S33f_the_controller_pins_only_as_the_lifts_rule_says()
    {
        var controller = File.ReadAllText(Path.Combine(App, "Interop", "DesktopZOrder.cs"));

        Assert.DoesNotContain("ForEachSurface(PinToTopOfTopmost)", controller, StringComparison.Ordinal);
        Assert.Equal(2, Regex.Matches(controller, @"Placing\(hwnd, PinToTopOfTopmost\)").Count);
        Assert.Single(Regex.Matches(controller, @"\bPinToTopOfTopmost\("));
        Assert.Contains("ZOrderPolicy.Lift(", controller, StringComparison.Ordinal);
        Assert.Contains("ZOrderPolicy.Move(_pinnedTopmost, _placing)", controller, StringComparison.Ordinal);
    }

    /// <summary>
    /// A popup in a window of its own (<c>ShouldConstrainToRootBounds = false</c>) is not cut by the
    /// surface's region and stands in the topmost band while it is open: a menu or a flyout the user
    /// just opened, placed by WinUI inside the monitor's work area. These are today's; the list only
    /// shrinks. A new one is a decision (can it stay inside the surface?), not a default.
    /// </summary>
    [Fact]
    public void S21_the_popups_in_windows_of_their_own_are_the_known_ones()
        => Assert.Equal(
            [
                "src/Themia.App/Shell/WidgetCard.Fault.cs",
                "src/Themia.Sdk.WinUI/Widgets/ConfirmFlyout.cs",
                "src/Themia.Sdk.WinUI/Widgets/ItemMenu.cs",
                "src/Themia.Sdk.WinUI/Widgets/RenameFlyout.cs",
                "src/Widgets/Themia.Widget.Folder/FolderView.Commands.cs",
                "src/Widgets/Themia.Widget.Scratchpad/ScratchpadView.FullScreen.cs",
            ],
            FilesWith(WindowedPopup()));

    // ---- the surface is never a full-screen app ([S-852]) ---------------------------------------

    /// <summary>The test a dock (Karpo's FullscreenApps.Covers) and the shell make of a front window: every edge of the display is inside it.</summary>
    private static bool CoversDisplay(DeviceRect window, DeviceRect display)
        => window.X <= display.X && window.Y <= display.Y
            && window.X + window.Width >= display.X + display.Width && window.Y + window.Height >= display.Y + display.Height;

    [Theory]
    [InlineData(0, 0, 3840, 2160)]
    [InlineData(-3840, 0, 3840, 2160)]
    [InlineData(1920, -200, 1280, 1024)]
    public void S852_a_surfaces_window_never_covers_its_whole_display_so_no_dock_takes_it_for_a_full_screen_app(double x, double y, double width, double height)
    {
        // The bug: the surface was exactly its display, and Karpo hid its bar whenever a click made it the front window.
        var display = new DeviceRect(x, y, width, height);
        var window = GridGeometry.SurfaceWindow(display);

        Assert.True(CoversDisplay(display, display));
        Assert.False(CoversDisplay(window, display));

        // Its corner and width are the display's, so every stored fraction still resolves as before; one pixel row is given up.
        Assert.Equal((display.X, display.Y, display.Width, display.Height - 1), (window.X, window.Y, window.Width, window.Height));
    }

    [Fact]
    public void S852_the_cut_to_the_work_area_stays_inside_the_shortened_window()
    {
        var display = new DeviceRect(0, 0, 1920, 1080);
        var region = GridGeometry.SurfaceRegion(GridGeometry.SurfaceWindow(display), display);

        Assert.Equal(new DeviceRect(0, 0, 1920, 1079), region);
    }

    [Fact]
    public void S852_the_surface_is_placed_by_that_rule_and_marks_itself_as_no_full_screen_app()
    {
        var placement = File.ReadAllText(Path.Combine(App, "Shell", "DesktopWindow.Placement.cs"));

        Assert.Contains("GridGeometry.SurfaceWindow(", placement, StringComparison.Ordinal);
        Assert.Contains("\"NonRudeHWND\"", placement, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"MoveAndResize\(new RectInt32\(\s*_monitor", placement);
    }
}
