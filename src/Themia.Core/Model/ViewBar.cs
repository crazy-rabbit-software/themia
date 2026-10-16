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

/// <summary>[S-5400] How the view bar offers its views: a chip each, or one drop-down button.</summary>
public enum ViewBarStyle
{
    /// <summary>A row (or column) of chips, one per view ([S-906]).</summary>
    Chips,

    /// <summary>One button with the shown view's icon and name, opening a list of the bar's views.</summary>
    Dropdown,
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

    /// <summary>[S-5400] Chips by default, as the bar always was.</summary>
    public ViewBarStyle Style { get; set; } = ViewBarStyle.Chips;

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
        Style = Style,
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

    /// <summary>
    /// [S-910] The two middle-of-a-side anchors stack the chips; every other anchor lays them in a
    /// row. [S-5401] A drop-down is one button, so it is a row at every anchor.
    /// </summary>
    public static bool IsVertical(ViewBarAnchor anchor, ViewBarStyle style = ViewBarStyle.Chips)
        => style == ViewBarStyle.Chips && anchor is ViewBarAnchor.LeftCenter or ViewBarAnchor.RightCenter;

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
    /// [S-917] How long the bar may be along its axis at <paramref name="anchor"/>: the work area's
    /// width (a row) or height (a column), less the margin at both ends. A column in edit mode also
    /// gives up the strip the edit toolbar takes at the top, so a column pushed below the toolbar
    /// ([S-909]) still ends a margin above the bottom.
    /// </summary>
    public static double Room(PixelRect area, ViewBarAnchor anchor, PixelRect? avoid = null, ViewBarStyle style = ViewBarStyle.Chips)
    {
        var m = EdgeMargin;
        if (!IsVertical(anchor, style))
        {
            return Math.Max(0, area.Width - (2 * m));
        }

        var room = area.Height - (2 * m);
        if (avoid is { } chrome)
        {
            room -= Math.Max(0, chrome.Y + chrome.Height + (m / 2) - (area.Y + m));
        }

        return Math.Max(0, room);
    }

    /// <summary>
    /// [S-917] Which chips the bar draws when they do not all fit in <paramref name="room"/>, the way a
    /// Windows command bar overflows: the chips in the user's order from the start, as many as fit
    /// beside a final overflow chip, and the shown view's chip always among them (it takes the place
    /// of the last chip that would have fit). Every chip that fits is drawn; nothing is reordered.
    /// </summary>
    /// <param name="lengths">Each chip's length along the bar's axis, in bar order.</param>
    /// <param name="shown">The index of the shown view's chip, or -1 when the bar does not offer it.</param>
    /// <param name="room">The bar's whole length budget ([S-917] <see cref="Room"/>).</param>
    /// <param name="fixedLength">What the bar spends besides chips: its padding at both ends, and the grip with its gap in edit mode.</param>
    /// <param name="spacing">The gap between two chips (and between the last chip and the overflow chip).</param>
    /// <param name="overflowLength">The overflow chip's length.</param>
    public static ViewBarFit Fit(IReadOnlyList<double> lengths, int shown, double room, double fixedLength, double spacing, double overflowLength)
    {
        ArgumentNullException.ThrowIfNull(lengths);

        var count = lengths.Count;
        var whole = fixedLength + lengths.Sum() + (spacing * Math.Max(0, count - 1));
        if (count == 0 || whole <= room || !double.IsFinite(room))
        {
            return new ViewBarFit([.. Enumerable.Range(0, count)], Overflow: false);
        }

        var budget = room - fixedLength - overflowLength - spacing;
        var keep = new SortedSet<int>();
        var used = 0.0;

        if (shown >= 0 && shown < count)
        {
            // The shown view is drawn even when it alone overruns the budget: the bar says which view
            // this is, and placement pins it inside the area ([S-909]).
            keep.Add(shown);
            used = lengths[shown];
        }

        for (var i = 0; i < count; i++)
        {
            if (i == shown)
            {
                continue;
            }

            var need = lengths[i] + (keep.Count > 0 ? spacing : 0);
            if (used + need > budget)
            {
                break;
            }

            keep.Add(i);
            used += need;
        }

        return new ViewBarFit([.. keep], Overflow: keep.Count < count);
    }

    /// <summary>
    /// [S-5402] The views the bar's menu lists, as indexes in bar order: a drop-down lists every
    /// view on the bar; the chip bar's overflow chip ([S-918]) lists the ones <paramref name="fit"/>
    /// left out. One menu, two callers.
    /// </summary>
    public static IReadOnlyList<int> MenuViews(ViewBarStyle style, int count, ViewBarFit fit)
    {
        ArgumentNullException.ThrowIfNull(fit);

        if (style == ViewBarStyle.Dropdown)
        {
            return [.. Enumerable.Range(0, Math.Max(0, count))];
        }

        var drawn = new HashSet<int>(fit.Shown);
        return [.. Enumerable.Range(0, Math.Max(0, count)).Where(i => !drawn.Contains(i))];
    }

    /// <summary>
    /// [S-5403] Type-ahead in the bar's menu: the next view after <paramref name="from"/> whose name
    /// starts with the typed character, wrapping round, so pressing a letter again steps through the
    /// views that start with it. Case-insensitive; -1 when no name starts with it.
    /// </summary>
    /// <param name="names">The menu's view names, in menu order.</param>
    /// <param name="from">The focused item, or negative when none is.</param>
    public static int TypeAhead(IReadOnlyList<string> names, int from, string typed)
    {
        ArgumentNullException.ThrowIfNull(names);

        var key = (typed ?? string.Empty).Trim();
        if (key.Length == 0 || names.Count == 0)
        {
            return -1;
        }

        var start = from < 0 || from >= names.Count ? -1 : from;
        for (var step = 1; step <= names.Count; step++)
        {
            var i = (start + step) % names.Count;
            if ((names[i] ?? string.Empty).TrimStart().StartsWith(key, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>[S-918] Which way a menu hanging from the bar opens: into the display, away from the edge the bar sits on.</summary>
    public static ViewBarMenuSide MenuSide(ViewBarAnchor anchor) => anchor switch
    {
        ViewBarAnchor.TopLeft or ViewBarAnchor.TopCenter or ViewBarAnchor.TopRight => ViewBarMenuSide.Below,
        ViewBarAnchor.LeftCenter => ViewBarMenuSide.Right,
        ViewBarAnchor.RightCenter => ViewBarMenuSide.Left,
        _ => ViewBarMenuSide.Above,
    };

    /// <summary>
    /// [S-918] The tallest a menu opened from <paramref name="from"/> (the bar, or the edit toolbar) may
    /// be: the work area on that side of it, a margin short of the edge, so a long list scrolls inside
    /// the menu instead of running under the taskbar or off the display. A menu beside a column may use
    /// the area's whole height.
    /// </summary>
    public static double MenuHeight(PixelRect area, PixelRect from, ViewBarMenuSide side)
    {
        var m = EdgeMargin;
        var height = side switch
        {
            ViewBarMenuSide.Above => from.Y - area.Y - m,
            ViewBarMenuSide.Below => area.Y + area.Height - (from.Y + from.Height) - m,
            _ => area.Height - (2 * m),
        };

        return Math.Max(0, height);
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

    /// <summary>
    /// [S-1708] The whitelist after the "All views" box: on is the empty "all" whitelist; off keeps
    /// only <paramref name="keepViewId"/> (the view the display shows), or the first view when that
    /// is not a view — the bar never offers nothing.
    /// </summary>
    public static List<string> SetAllVisible(IReadOnlyList<ViewState> views, bool on, string? keepViewId)
    {
        ArgumentNullException.ThrowIfNull(views);

        if (on || views.Count == 0)
        {
            return [];
        }

        var keep = views.FirstOrDefault(v => v.Id == keepViewId) ?? views[0];
        return views.Count == 1 ? [] : [keep.Id];
    }

    /// <summary>[S-1708] What the "Views on the bar" button says: every view, one view by name, or how many of how many.</summary>
    public static ViewBarOffer Offer(IReadOnlyList<ViewState> views, ViewBarSettings settings)
    {
        ArgumentNullException.ThrowIfNull(views);
        ArgumentNullException.ThrowIfNull(settings);

        var offered = VisibleViews(views, settings);
        return new ViewBarOffer(
            offered.Count == views.Count,
            offered.Count,
            views.Count,
            offered.Count == 1 && views.Count > 1 ? offered[0].Name : null);
    }
}

/// <summary>[S-917] The chips a view bar draws: their indexes in bar order, and whether the rest go behind an overflow chip.</summary>
public sealed record ViewBarFit(IReadOnlyList<int> Shown, bool Overflow);

/// <summary>[S-918] The side of the bar a menu opens on.</summary>
public enum ViewBarMenuSide
{
    Above,
    Below,
    Left,
    Right,
}

/// <summary>[S-1708] A display's view bar choice, summed up for its settings button.</summary>
/// <param name="All">Every view is offered (the whitelist is "all", or names every view).</param>
/// <param name="Single">The one view's name when exactly one of several is offered.</param>
public readonly record struct ViewBarOffer(bool All, int Count, int Total, string? Single);
