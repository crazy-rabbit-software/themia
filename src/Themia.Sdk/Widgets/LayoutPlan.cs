using Themia.Core.Localization;
using Themia.Core.Model;

namespace Themia.Core.Widgets;

/// <summary>A rectangle in percent of a display's work area (0..100 on each axis).</summary>
public readonly record struct PercentRect(double X, double Y, double Width, double Height);

/// <summary>A rectangle in physical screen pixels (whole pixels, as Win32 takes them).</summary>
public readonly record struct ScreenRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;

    public int Bottom => Y + Height;
}

/// <summary>[S-1407] One window to open and where it goes: the walk of one assigned pane.</summary>
public sealed record LayoutEntry(LayoutSource Source, string Target, string Label, bool ReuseIfRunning, PercentRect Area);

/// <summary>
/// [S-1407] Turning an app layout into windows (original <c>rootToEntries</c> and the backend's
/// <c>compute_rect</c>): the tree walked against the work area, each assigned pane's rectangle in
/// pixels, the correction for a window's invisible resize border, and when a window has moved off
/// where it was put. Pure; the Win32 half is <c>AppLayoutLauncher</c>.
/// </summary>
public static class LayoutPlan
{
    /// <summary>The smallest window a pane is given, in pixels (original: 100 × 80).</summary>
    public const int MinWidth = 100;

    /// <inheritdoc cref="MinWidth"/>
    public const int MinHeight = 80;

    /// <summary>How far a window may sit from its pane before it is put back (original: 8 px).</summary>
    public const int PositionSlack = 8;

    /// <summary>How far its size may differ (original: 24 px — some apps snap to their own grid).</summary>
    public const int SizeSlack = 24;

    /// <summary>
    /// [S-1407] One entry per assigned pane, in tree order, with its rectangle in percent of the
    /// work area (original S-52: a 50/50 side-by-side split puts the first pane at x 0..50 and the
    /// second at 50..100, both full height). An empty layout walks to nothing (S-53). The same
    /// target twice opens once, in the first pane that names it: a second launch of one app gives
    /// one window, or two that fight for one place. A folder and a web address always open (they
    /// never "reuse a running app").
    /// </summary>
    public static IReadOnlyList<LayoutEntry> Walk(LayoutNode? root)
    {
        var entries = new List<LayoutEntry>();
        Walk(root, new PercentRect(0, 0, 100, 100), entries, 0);
        return entries;
    }

    private static void Walk(LayoutNode? node, PercentRect area, List<LayoutEntry> into, int depth)
    {
        if (node is null || depth > LayoutTree.MaxDepth)
        {
            return;
        }

        if (node.Kind == LayoutNodeKind.Pane)
        {
            if (!LayoutTree.IsAssigned(node) || node.Assignment is not { } a)
            {
                return;
            }

            var target = a.Target.Trim();

            if (into.Any(e => e.Source == a.Source && LaunchTargets.SameTarget(e.Target, target)))
            {
                return;
            }

            var reuse = a.Source is LayoutSource.App or LayoutSource.File && a.ReuseIfRunning;
            into.Add(new LayoutEntry(a.Source, target, a.Label ?? string.Empty, reuse, area));
            return;
        }

        var r = LayoutTree.ClampRatio(node.Ratio);

        if (node.Direction == SplitDirection.Vertical)
        {
            Walk(node.A, area with { Width = area.Width * r }, into, depth + 1);
            Walk(node.B, area with { X = area.X + area.Width * r, Width = area.Width * (1 - r) }, into, depth + 1);
        }
        else
        {
            Walk(node.A, area with { Height = area.Height * r }, into, depth + 1);
            Walk(node.B, area with { Y = area.Y + area.Height * r, Height = area.Height * (1 - r) }, into, depth + 1);
        }
    }

    /// <summary>
    /// [S-1407] A pane's rectangle in pixels on <paramref name="workArea"/> (original
    /// <c>compute_rect</c>): percentages held to 0..100, rounded, and never smaller than
    /// <see cref="MinWidth"/> × <see cref="MinHeight"/>.
    /// </summary>
    public static ScreenRect Place(PercentRect area, ScreenRect workArea)
    {
        static double Clamp(double v) => double.IsNaN(v) ? 0 : Math.Clamp(v, 0, 100);

        var x = workArea.X + (int)Math.Round(workArea.Width * Clamp(area.X) / 100, MidpointRounding.AwayFromZero);
        var y = workArea.Y + (int)Math.Round(workArea.Height * Clamp(area.Y) / 100, MidpointRounding.AwayFromZero);
        var w = Math.Max(MinWidth, (int)Math.Round(workArea.Width * Clamp(area.Width) / 100, MidpointRounding.AwayFromZero));
        var h = Math.Max(MinHeight, (int)Math.Round(workArea.Height * Clamp(area.Height) / 100, MidpointRounding.AwayFromZero));
        return new ScreenRect(x, y, w, h);
    }

    /// <summary>
    /// [S-1408] The window rectangle that makes a window's <i>visible</i> frame fill
    /// <paramref name="pane"/>: Windows 10/11 windows carry an invisible resize border (about 7 px
    /// left, right and bottom), so a window set to the pane's rectangle shows a gap. The borders are
    /// the window rect less DWM's extended frame bounds; negative ones (none measured) count as 0.
    /// </summary>
    public static ScreenRect WithInvisibleBorders(ScreenRect pane, int left, int top, int right, int bottom)
    {
        left = Math.Max(0, left);
        top = Math.Max(0, top);
        right = Math.Max(0, right);
        bottom = Math.Max(0, bottom);
        return new ScreenRect(pane.X - left, pane.Y - top, pane.Width + left + right, pane.Height + top + bottom);
    }

    /// <summary>
    /// [S-1408] Whether a window has moved off its pane since it was put there (an app that restores
    /// its own last position right after showing): more than <see cref="PositionSlack"/> off in
    /// position or <see cref="SizeSlack"/> in size, judged on the visible frame.
    /// </summary>
    public static bool Drifted(ScreenRect pane, ScreenRect visible)
        => Math.Abs(visible.X - pane.X) > PositionSlack
        || Math.Abs(visible.Y - pane.Y) > PositionSlack
        || Math.Abs(visible.Width - pane.Width) > SizeSlack
        || Math.Abs(visible.Height - pane.Height) > SizeSlack;

    /// <summary>
    /// [S-1405] The assignments a layout's icon previews: up to <paramref name="max"/> assigned
    /// panes in tree order (original S-72).
    /// </summary>
    public static IReadOnlyList<LayoutAssignment> Preview(LayoutNode? root, int max = 4)
        => LayoutTree.Panes(root)
            .Where(LayoutTree.IsAssigned)
            .Select(pane => pane.Assignment!)
            .Take(Math.Max(0, max))
            .ToList();

    /// <summary>[S-1405] A layout's detail line (original S-57): "No apps yet", "1 app", "3 apps".</summary>
    public static string Summary(LayoutNode? root) => Summary(Strings.Current, root);

    /// <inheritdoc cref="Summary(LayoutNode?)"/>
    public static string Summary(Locale locale, LayoutNode? root)
    {
        var count = LayoutTree.AssignedCount(root);
        return count == 0
            ? Strings.Get(locale, Str.WidgetsC_Shortcuts_AppLayout_NoApps)
            : Strings.Plural(locale, Str.Shortcut_AppCount, count);
    }
}
