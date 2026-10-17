using System.Globalization;
using Themia.Core.Geometry;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>A widget kind as a showcase view names and marks it.</summary>
/// <param name="Kind">The kind shown at every tier.</param>
/// <param name="Name">The view's name: the kind's label.</param>
/// <param name="Glyph">The view's icon: the kind's glyph.</param>
public readonly record struct ShowcaseKind(WidgetId Kind, string Name, string Glyph);

/// <summary>
/// [S-1810..S-1814] One view per widget kind holding that widget at every <see cref="WidgetTiers"/>
/// size, so the whole responsive ladder can be looked at on the real desktop. Built by the Debug
/// flag <c>--add-showcase-views</c>; nothing in the app's own UI creates them.
/// <para>
/// Ids are derived from the kind and the tier, never random, so a second run replaces the first
/// instead of adding another set beside it.
/// </para>
/// </summary>
public static class ShowcaseViews
{
    /// <summary>Every showcase view's and widget's id starts with this; nothing else's does (ids are GUIDs otherwise).</summary>
    public const string IdPrefix = "showcase-";

    /// <summary>Space kept clear inside the work area's edges, in DIPs.</summary>
    public const double Margin = 16;

    /// <summary>[S-1811] The view for <paramref name="kind"/>; a second page of the same kind gets "-2".</summary>
    public static string ViewId(WidgetId kind, int page = 0)
        => page == 0 ? IdPrefix + Slug(kind) : $"{IdPrefix}{Slug(kind)}-{(page + 1).ToString(CultureInfo.InvariantCulture)}";

    /// <summary>[S-1811] The widget showing <paramref name="kind"/> at <paramref name="tier"/>.</summary>
    public static string WidgetId(WidgetId kind, WidgetTier tier)
        => $"{IdPrefix}{Slug(kind)}-{tier.Name.ToLowerInvariant()}";

    public static bool IsShowcase(ViewState view)
    {
        ArgumentNullException.ThrowIfNull(view);
        return view.Id.StartsWith(IdPrefix, StringComparison.Ordinal);
    }

    /// <summary>
    /// [S-1810] The showcase views for <paramref name="kinds"/>, in that order, laid out for
    /// <paramref name="screen"/> (the whole monitor in DIPs, with its work area as
    /// <see cref="ScreenMetrics.Allowed"/>). Each widget's cell is its tier times
    /// <paramref name="contentScale"/> plus <paramref name="gap"/>, so after the gap inset ([S-04])
    /// and the content scale ([S-850]) the widget lays out in exactly the tier's DIPs.
    /// <para>
    /// [S-1812] When the tiers do not all fit in one work area, the rest go on a second view of the
    /// same kind ("Clock (2)"); a tier larger than the whole work area is shrunk to it.
    /// </para>
    /// </summary>
    public static List<ViewState> Build(IReadOnlyList<ShowcaseKind> kinds, ScreenMetrics screen, double gap, double contentScale = 1)
    {
        ArgumentNullException.ThrowIfNull(kinds);
        if (!screen.IsUsable)
        {
            throw new ArgumentException("the screen has no usable size", nameof(screen));
        }

        var pages = Layout(screen, gap, contentScale);
        var views = new List<ViewState>();

        foreach (var kind in kinds)
        {
            for (var p = 0; p < pages.Count; p++)
            {
                var view = new ViewState
                {
                    Id = ViewId(kind.Kind, p),
                    Name = pages.Count == 1 ? kind.Name : $"{kind.Name} ({(p + 1).ToString(CultureInfo.InvariantCulture)})",
                    Icon = string.IsNullOrEmpty(kind.Glyph) ? null : kind.Glyph,
                };

                foreach (var (tier, cell) in pages[p])
                {
                    var widget = WidgetInstance.Create(kind.Kind, new FracRect(
                        cell.X / screen.Width,
                        cell.Y / screen.Height,
                        cell.Width / screen.Width,
                        cell.Height / screen.Height));
                    widget.Id = WidgetId(kind.Kind, tier);
                    view.Widgets.Add(widget);
                }

                views.Add(view);
            }
        }

        return views;
    }

    /// <summary>
    /// [S-1813] Put <paramref name="fresh"/> into <paramref name="state"/> in place of every showcase
    /// view already there. The fresh views go to the end; an old one whose id comes back is simply
    /// replaced (a display showing it keeps showing it), and one that does not come back is deleted
    /// through <see cref="ViewOps.Delete"/>, which repairs everything that named it. Returns how many
    /// old showcase views were taken out. Widgets the user added to a showcase view go with it.
    /// </summary>
    public static int Replace(AppState state, IReadOnlyList<ViewState> fresh)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(fresh);

        var freshIds = fresh.Select(v => v.Id).ToHashSet(StringComparer.Ordinal);
        var old = state.Views.Where(IsShowcase).ToList();

        // Added first, so removing the old ones can never leave the state without a view.
        state.Views.AddRange(fresh);

        foreach (var view in old)
        {
            if (freshIds.Contains(view.Id))
            {
                state.Views.Remove(view);
            }
            else
            {
                ViewOps.Delete(state, view.Id);
            }
        }

        return old.Count;
    }

    /// <summary>
    /// The cells, in DIPs of the whole monitor, page by page. Skyline bottom-left packing of the tiers
    /// tallest first, each placed at the lowest, then leftmost, spot inside the work area less
    /// <see cref="Margin"/>. Whole DIPs, so the painted edges ([S-04]) land on the tier exactly.
    /// </summary>
    internal static List<List<(WidgetTier Tier, PixelRect Cell)>> Layout(ScreenMetrics screen, double gap, double contentScale)
    {
        var scale = double.IsFinite(contentScale) && contentScale > 0 ? contentScale : 1;
        var spacing = double.IsFinite(gap) && gap > 0 ? gap : 0;

        var work = GridGeometry.AllowedPixels(screen);
        var left = Math.Ceiling(work.X + Margin);
        var top = Math.Ceiling(work.Y + Margin);
        var width = Math.Max(1, Math.Floor(work.X + work.Width - Margin) - left);
        var height = Math.Max(1, Math.Floor(work.Y + work.Height - Margin) - top);

        var pending = WidgetTiers.All
            .Select(tier => (Tier: tier,
                W: Math.Min(width, Math.Ceiling((tier.Width * scale) + spacing)),
                H: Math.Min(height, Math.Ceiling((tier.Height * scale) + spacing))))
            .OrderByDescending(t => t.H)
            .ThenByDescending(t => t.W)
            .ToList();

        var pages = new List<List<(WidgetTier, PixelRect)>>();
        while (pending.Count > 0)
        {
            var skyline = new List<Segment> { new(0, width, 0) };
            var page = new List<(WidgetTier, PixelRect)>();

            foreach (var item in pending.ToList())
            {
                if (Fit(skyline, item.W, item.H, width, height) is not { } spot)
                {
                    continue;
                }

                Raise(skyline, spot.X, item.W, spot.Y + item.H);
                page.Add((item.Tier, new PixelRect(left + spot.X, top + spot.Y, item.W, item.H)));
                pending.Remove(item);
            }

            // Every item fits an empty page (sizes are capped to it), so a page is never empty; the
            // check only keeps a future change to that from looping for ever.
            if (page.Count == 0)
            {
                throw new InvalidOperationException("a showcase tier fits no empty page");
            }

            pages.Add(page);
        }

        return pages;
    }

    /// <summary>The lowest, then leftmost, place a w × h box can sit on the skyline, or null.</summary>
    private static (double X, double Y)? Fit(List<Segment> skyline, double w, double h, double width, double height)
    {
        (double X, double Y)? best = null;
        foreach (var start in skyline)
        {
            var x = start.X;
            if (x + w > width)
            {
                continue;
            }

            var y = skyline.Where(s => s.X < x + w && s.X + s.Width > x).Max(s => s.Y);
            if (y + h > height)
            {
                continue;
            }

            if (best is null || y < best.Value.Y || (y == best.Value.Y && x < best.Value.X))
            {
                best = (x, y);
            }
        }

        return best;
    }

    /// <summary>Lift the skyline over [x, x + w) to <paramref name="y"/>.</summary>
    private static void Raise(List<Segment> skyline, double x, double w, double y)
    {
        var right = x + w;
        var next = new List<Segment>();
        foreach (var s in skyline)
        {
            var sRight = s.X + s.Width;
            if (sRight <= x || s.X >= right)
            {
                next.Add(s);
                continue;
            }

            if (s.X < x)
            {
                next.Add(new Segment(s.X, x - s.X, s.Y));
            }

            if (sRight > right)
            {
                next.Add(new Segment(right, sRight - right, s.Y));
            }
        }

        next.Add(new Segment(x, w, y));
        next.Sort((a, b) => a.X.CompareTo(b.X));

        skyline.Clear();
        foreach (var s in next)
        {
            if (skyline.Count > 0 && skyline[^1].Y == s.Y)
            {
                skyline[^1] = skyline[^1] with { Width = skyline[^1].Width + s.Width };
            }
            else
            {
                skyline.Add(s);
            }
        }
    }

    private static string Slug(WidgetId kind) => kind.ToString().ToLowerInvariant();

    private readonly record struct Segment(double X, double Width, double Y);
}
