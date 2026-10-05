using Themia.Core.Geometry;
using Themia.Core.Widgets;

namespace Themia.Core.Model;

/// <summary>[S-906] Where a display's view bar sits: a corner or the middle of an edge of the work area.</summary>
public enum ViewBarAnchor
{
    BottomLeft,
    BottomCenter,
    BottomRight,
    TopLeft,
    TopCenter,
    TopRight,
    LeftCenter,
    RightCenter,
}

/// <summary>[S-911] The view bar's three sizes, as in the original (M, L, XL).</summary>
public enum ViewBarSize
{
    Medium,
    Large,
    ExtraLarge,
}

/// <summary>[S-907] Whether the view bar is drawn, and how.</summary>
public enum ViewBarShow
{
    /// <summary>Not drawn: hidden by the user, or nothing to switch between.</summary>
    None,

    /// <summary>Drawn as the user set it up.</summary>
    Shown,

    /// <summary>Edit mode on a display whose bar is hidden: drawn faded, so it can still be dragged and used.</summary>
    HiddenPreview,
}

/// <summary>
/// [S-906] One display's view bar. Every display has its own, keyed like
/// <see cref="AppState.MonitorViews"/>; a display with no entry uses these defaults, so only
/// displays the user changed are stored.
/// </summary>
public sealed class ViewBarSettings
{
    /// <summary>Bottom left by default, where the original put it.</summary>
    public ViewBarAnchor Anchor { get; set; } = ViewBarAnchor.BottomLeft;

    public ViewBarSize Size { get; set; } = ViewBarSize.Medium;

    /// <summary>Icons (or initials) alone, without the names.</summary>
    public bool IconOnly { get; set; }

    /// <summary>The user turned the bar off on this display. Edit mode still shows a faded preview.</summary>
    public bool Hidden { get; set; }

    /// <summary>
    /// [S-908] The views this display's bar offers, by id, in no particular order (the bar keeps the
    /// user's view order). Empty means every view, so a view created later appears by itself.
    /// </summary>
    public List<string> VisibleViews { get; set; } = [];

    public ViewBarSettings Clone() => new()
    {
        Anchor = Anchor,
        Size = Size,
        IconOnly = IconOnly,
        Hidden = Hidden,
        VisibleViews = [.. VisibleViews],
    };
}

/// <summary>
/// The rules of the desktop view bar: when it shows, which views it offers, where it sits and which
/// anchor a drag drops it on. Pure, so they are tested without a window. [S-906]..[S-913]
/// </summary>
public static class ViewBarRules
{
    /// <summary>[S-909] Distance between the bar and the edge of the work area, in DIPs.</summary>
    public const double EdgeMargin = 16;

    /// <summary>[S-913] How far the pointer has to travel before a press becomes a drag, in DIPs.</summary>
    public const double DragThreshold = 4;

    /// <summary>The display's settings, or the defaults when the user never changed them. Never stored by this.</summary>
    public static ViewBarSettings For(AppState state, string monitorId)
    {
        ArgumentNullException.ThrowIfNull(state);
        return state.ViewBars.TryGetValue(monitorId, out var own) && own is not null ? own : new ViewBarSettings();
    }

    /// <summary>Change one display's settings, creating its entry on first change.</summary>
    public static void Update(AppState state, string monitorId, Action<ViewBarSettings> change)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(change);

        if (!state.ViewBars.TryGetValue(monitorId, out var own) || own is null)
        {
            own = new ViewBarSettings();
            state.ViewBars[monitorId] = own;
        }

        change(own);
    }

    /// <summary>[S-910] The two middle-of-a-side anchors stack the chips; every other anchor lays them in a row.</summary>
    public static bool IsVertical(ViewBarAnchor anchor) => anchor is ViewBarAnchor.LeftCenter or ViewBarAnchor.RightCenter;

    /// <summary>
    /// [S-908] The views the bar offers, in the user's view order. The whitelist filters; an empty
    /// one, or one naming no view that still exists, offers every view — a bar with no chips is a
    /// bar that has silently stopped working.
    /// </summary>
    public static IReadOnlyList<ViewState> VisibleViews(IReadOnlyList<ViewState> views, ViewBarSettings settings)
    {
        ArgumentNullException.ThrowIfNull(views);
        ArgumentNullException.ThrowIfNull(settings);

        if (settings.VisibleViews.Count == 0)
        {
            return views;
        }

        var wanted = new HashSet<string>(settings.VisibleViews, StringComparer.Ordinal);
        var shown = views.Where(v => wanted.Contains(v.Id)).ToList();
        return shown.Count == 0 ? views : shown;
    }

    /// <summary>
    /// [S-907] Whether the bar is drawn. Outside edit mode: not when the user hid it, and not when
    /// the only thing it could offer is one view — unless the user picked that single view on
    /// purpose. In edit mode it is always there, so it can be dragged and its chips reordered; a bar
    /// the user hid is drawn faded, so it is plain it will be gone when editing ends.
    /// </summary>
    public static ViewBarShow Show(bool editMode, ViewBarSettings settings, IReadOnlyList<ViewState> views)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(views);

        if (editMode)
        {
            return settings.Hidden ? ViewBarShow.HiddenPreview : ViewBarShow.Shown;
        }

        if (settings.Hidden)
        {
            return ViewBarShow.None;
        }

        var explicitChoice = settings.VisibleViews.Any(id => views.Any(v => v.Id == id));
        return VisibleViews(views, settings).Count > 1 || explicitChoice ? ViewBarShow.Shown : ViewBarShow.None;
    }

    /// <summary>
    /// [S-909] Where the bar's top-left corner goes: at its anchor inside the work area, a margin
    /// from the edges, and never over the taskbar — the work area is the only space it is placed in.
    /// <para>
    /// <paramref name="avoid"/> is the edit toolbar. Here it sits along the top (the original's was
    /// along the bottom and the bar lifted its bottom anchors above it), so a bar that would overlap
    /// it moves down below it instead. Only an overlap moves it: a top-left bar beside a centred
    /// toolbar stays where the user put it.
    /// </para>
    /// <para>
    /// A bar bigger than the area is pinned to the area's top-left, so its first chips are reachable.
    /// </para>
    /// </summary>
    public static (double X, double Y) Place(PixelRect area, double width, double height, ViewBarAnchor anchor, PixelRect? avoid = null)
    {
        var m = EdgeMargin;
        var left = area.X + m;
        var right = area.X + area.Width - m - width;
        var centreX = area.X + ((area.Width - width) / 2);
        var top = area.Y + m;
        var bottom = area.Y + area.Height - m - height;
        var middleY = area.Y + ((area.Height - height) / 2);

        var (x, y) = anchor switch
        {
            ViewBarAnchor.TopLeft => (left, top),
            ViewBarAnchor.TopCenter => (centreX, top),
            ViewBarAnchor.TopRight => (right, top),
            ViewBarAnchor.LeftCenter => (left, middleY),
            ViewBarAnchor.RightCenter => (right, middleY),
            ViewBarAnchor.BottomLeft => (left, bottom),
            ViewBarAnchor.BottomCenter => (centreX, bottom),
            ViewBarAnchor.BottomRight => (right, bottom),
            _ => (left, bottom),
        };

        if (avoid is { } chrome && GridGeometry.Intersect(new PixelRect(x, y, width, height), chrome) is not null)
        {
            y = chrome.Y + chrome.Height + (m / 2);
        }

        // Inside the area, the top-left corner winning when the bar does not fit at all.
        x = Math.Max(area.X, Math.Min(x, area.X + area.Width - width));
        y = Math.Max(area.Y, Math.Min(y, area.Y + area.Height - height));
        return (x, y);
    }

    /// <summary>
    /// [S-913] The anchor a dragged bar drops on: the area is cut into thirds each way; the middle
    /// band of the left and right thirds is the vertical anchors, the dead centre goes to the top
    /// or bottom middle by which half the pointer is in. As in the original.
    /// </summary>
    public static ViewBarAnchor PickAnchor(PixelRect area, double x, double y)
    {
        var xz = area.Width > 0 ? (x - area.X) / area.Width : 0.5;
        var yz = area.Height > 0 ? (y - area.Y) / area.Height : 0.5;

        var column = xz < 1.0 / 3 ? 0 : xz > 2.0 / 3 ? 2 : 1;

        if (yz >= 1.0 / 3 && yz <= 2.0 / 3)
        {
            return column switch
            {
                0 => ViewBarAnchor.LeftCenter,
                2 => ViewBarAnchor.RightCenter,
                _ => yz < 0.5 ? ViewBarAnchor.TopCenter : ViewBarAnchor.BottomCenter,
            };
        }

        var upper = yz < 0.5;
        return column switch
        {
            0 => upper ? ViewBarAnchor.TopLeft : ViewBarAnchor.BottomLeft,
            2 => upper ? ViewBarAnchor.TopRight : ViewBarAnchor.BottomRight,
            _ => upper ? ViewBarAnchor.TopCenter : ViewBarAnchor.BottomCenter,
        };
    }

    /// <summary>
    /// [S-914] Which chip slot a dragged chip is over: the one whose centre is nearest the pointer
    /// along the bar's axis, so the drag keeps working when the pointer leaves the bar.
    /// </summary>
    /// <param name="centres">Each chip's centre along the bar's axis, in bar order.</param>
    /// <returns>The nearest index, or -1 for no chips.</returns>
    public static int NearestSlot(IReadOnlyList<double> centres, double pointer)
    {
        ArgumentNullException.ThrowIfNull(centres);

        var best = -1;
        var bestDistance = double.PositiveInfinity;
        for (var i = 0; i < centres.Count; i++)
        {
            var distance = Math.Abs(pointer - centres[i]);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = i;
            }
        }

        return best;
    }

    /// <summary>
    /// [S-908] The display's whitelist after ticking or unticking one view on its page: the last view
    /// cannot be unticked, and ticking every view collapses back to "all", which keeps following views
    /// added later.
    /// </summary>
    public static List<string> ToggleVisible(IReadOnlyList<ViewState> views, ViewBarSettings settings, string viewId, bool on)
    {
        ArgumentNullException.ThrowIfNull(views);
        ArgumentNullException.ThrowIfNull(settings);

        var summaries = views.Select(v => new ViewSummary(v.Id, v.Name)).ToList();
        return ViewListRules.Toggle(summaries, settings.VisibleViews, viewId, on);
    }
}
