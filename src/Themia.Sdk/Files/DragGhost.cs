using System.Globalization;

namespace Themia.Core.Files;

/// <summary>[S-5564] One dragged item's icon as its view holds it: its pixels (BGRA, premultiplied, top row first).</summary>
public readonly record struct GhostIcon(int Width, int Height, byte[] Bgra)
{
    /// <summary>Whether there is anything to draw: a size, and exactly its pixels.</summary>
    public bool Drawable => Width > 0 && Height > 0 && Bgra is not null && Bgra.Length == (long)Width * Height * 4;
}

/// <summary>[S-5565] The badge's number as drawn by the host's text renderer: one byte of coverage a pixel, top row first.</summary>
public readonly record struct GhostInk(int Width, int Height, byte[] Coverage)
{
    /// <summary>Whether there is anything to draw: a size, and exactly its bytes.</summary>
    public bool Drawable => Width > 0 && Height > 0 && Coverage is not null && Coverage.Length == (long)Width * Height;
}

/// <summary>[S-5565] An opaque colour of the picture (the badge's fill and its number).</summary>
public readonly record struct GhostColour(byte R, byte G, byte B);

/// <summary>[S-5564] A box of the drag's picture, in DIPs from its top left.</summary>
public readonly record struct GhostBox(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;

    public double Bottom => Y + Height;
}

/// <summary>[S-5564] The count badge: its words ("12", "99+") and its box.</summary>
public sealed record GhostBadge(string Text, GhostBox Box);

/// <summary>
/// [S-5564] The drag's picture laid out, in DIPs: the icons' squares, front first (the grabbed item's is
/// <c>Icons[0]</c>, the others behind it), the badge when several items are dragged, the picture's size
/// and the pointer's place in it.
/// </summary>
public sealed record DragGhostPlan(IReadOnlyList<GhostBox> Icons, GhostBadge? Badge, double Width, double Height, double HotX, double HotY);

/// <summary>
/// [S-5564] [S-5565] The picture a drag of items shows under the pointer, as Windows 11's File Explorer
/// draws its own: one item is its icon alone (no name), see-through, held where it was grabbed; several
/// are one stack, the grabbed item's icon in front and at most two more fanned behind it up and to the
/// left, with the number of dragged items in a rounded badge at the stack's lower right ("99+" past 99).
/// Never every icon in its own place, never a name. The layout is a pure rule (<see cref="Plan"/>), the
/// pixels a pure composition of the icons the views already hold (<see cref="Compose"/>): nothing is
/// rendered when the drag starts, so the picture is there with the drag. [S-13048] A move of the desktop's own icons is one
/// item's picture however many move (<see cref="OfMove"/>).
/// </summary>
public static class DragGhost
{
    /// <summary>The most icons the stack shows: the grabbed one and two behind it. More are counted by the badge alone.</summary>
    public const int MaxShown = 3;

    /// <summary>How far each icon behind stands out up and to the left of the one before it, in DIPs.</summary>
    public const double Step = 6;

    /// <summary>The badge's height and least width, in DIPs.</summary>
    public const double BadgeSide = 20;

    /// <summary>The room either side of the badge's number.</summary>
    public const double BadgePad = 5;

    /// <summary>The badge's corner radius.</summary>
    public const double BadgeRadius = 4;

    /// <summary>How far the badge hangs out of the front icon's lower right corner.</summary>
    public const double BadgeOut = 4;

    /// <summary>The height of the badge's number, in DIPs: the host draws its ink at this height times the display's scale.</summary>
    public const double BadgeTextHeight = 12;

    /// <summary>The largest count the badge writes out; more reads "99+".</summary>
    public const int MaxCount = 99;

    /// <summary>The badge's words for <paramref name="count"/> dragged items: none for one item, the number up to 99, then "99+".</summary>
    public static string? BadgeText(int count) => count switch
    {
        < 2 => null,
        > MaxCount => MaxCount.ToString(CultureInfo.InvariantCulture) + "+",
        _ => count.ToString(CultureInfo.InvariantCulture),
    };

    /// <summary>How many icons the picture shows of <paramref name="count"/> dragged items when <paramref name="pictures"/> of them have a picture to show.</summary>
    public static int Shown(int count, int pictures) => Math.Max(0, Math.Min(Math.Min(count, pictures), MaxShown));

    /// <summary>
    /// [S-5564] The picture of <paramref name="count"/> dragged items of which <paramref name="pictures"/>
    /// have an icon to show, each drawn in a square <paramref name="icon"/> DIPs a side. The pointer holds
    /// the front icon where it was grabbed (<paramref name="grabX"/>, <paramref name="grabY"/>: its place
    /// from that icon's top left; a press beside the icon, on its name, holds the nearest point of it).
    /// <paramref name="badgeText"/> is the width of the badge's number in DIPs. Null when nothing can be shown.
    /// </summary>
    public static DragGhostPlan? Plan(int count, int pictures, double icon, double grabX, double grabY, double badgeText)
    {
        var shown = Shown(count, pictures);
        if (shown == 0 || !double.IsFinite(icon) || icon <= 0)
        {
            return null;
        }

        // The front icon lowest and rightmost; each one behind a step up and to the left.
        var back = shown - 1;
        var icons = new GhostBox[shown];
        for (var i = 0; i < shown; i++)
        {
            icons[i] = new GhostBox((back - i) * Step, (back - i) * Step, icon, icon);
        }

        var front = icons[0];
        GhostBadge? badge = null;
        if (BadgeText(count) is { } text)
        {
            var width = Math.Max(BadgeSide, Math.Ceiling(Math.Max(0, double.IsFinite(badgeText) ? badgeText : 0)) + (2 * BadgePad));
            badge = new GhostBadge(text, new GhostBox(front.Right + BadgeOut - width, front.Bottom + BadgeOut - BadgeSide, width, BadgeSide));
        }

        // The picture is the box round everything; a badge wider than a small icon moves the rest right.
        var left = Math.Min(0, badge?.Box.X ?? 0);
        var top = Math.Min(0, badge?.Box.Y ?? 0);
        if (left < 0 || top < 0)
        {
            for (var i = 0; i < shown; i++)
            {
                icons[i] = icons[i] with { X = icons[i].X - left, Y = icons[i].Y - top };
            }

            badge = badge is null ? null : badge with { Box = badge.Box with { X = badge.Box.X - left, Y = badge.Box.Y - top } };
            front = icons[0];
        }

        var right = Math.Max(front.Right, badge?.Box.Right ?? 0);
        var bottom = Math.Max(front.Bottom, badge?.Box.Bottom ?? 0);
        var hotX = front.X + Math.Clamp(double.IsFinite(grabX) ? grabX : icon / 2, 0, icon);
        var hotY = front.Y + Math.Clamp(double.IsFinite(grabY) ? grabY : icon / 2, 0, icon);
        return new DragGhostPlan(icons, badge, right, bottom, hotX, hotY);
    }

    /// <summary>
    /// [S-13048] The picture of a move of the desktop's own icons (the owner, 2026-11-20: "when i move
    /// around multiple desktop icons there is an additional preview with the count showing over the mouse. this should not
    /// be. remove that."): the held icon alone (<paramref name="held"/>, the icon grabbed), whole in its square
    /// <paramref name="icon"/> DIPs a side, see-through at <see cref="Themia.Core.Widgets.SelectionLook.DragOpacity"/>, held
    /// where it was grabbed (<paramref name="grabX"/>, <paramref name="grabY"/>, as <see cref="Plan"/>), at
    /// <paramref name="scale"/> pixels a DIP, whatever the number of icons moving (<paramref name="moving"/>, the held one
    /// among them): never the others stacked behind it, never a count badge. How many move and where shows where the block
    /// lands, each moving icon's whole ghost ([S-13046]). [S-16460] While the move stays on the desktop it is not drawn (the
    /// icons themselves move, nothing else under the pointer); the shell's drag the move becomes carries it ([S-13047]), and
    /// the move shows it over the Unplaced icons page ([S-13713]). Explorer's stack with its count stays a drag's that starts as the shell's elsewhere (a folder's rows, a
    /// search fence's files). Null when nothing moves or nothing can be shown.
    /// </summary>
    public static DragPicture? OfMove(GhostIcon held, int moving, double icon, double grabX, double grabY, double scale)
        => moving >= 1 && held.Drawable && Plan(count: 1, pictures: 1, icon, grabX, grabY, badgeText: 0) is { } plan
            ? Compose(plan, [held], scale, Themia.Core.Widgets.SelectionLook.DragOpacity, default, default, default)
            : null;

    /// <summary>
    /// [S-5565] The picture's pixels for <paramref name="plan"/> at <paramref name="scale"/> pixels a DIP:
    /// <paramref name="icons"/> (front first, as the plan's squares) each fitted into its square, the
    /// ones behind drawn first, the stack then faded as one to <paramref name="opacity"/>; then the badge, opaque, in
    /// <paramref name="badge"/> with its number (<paramref name="ink"/>) in <paramref name="text"/>. An
    /// icon that cannot be drawn leaves its square empty; a badge whose ink is missing is still drawn,
    /// without its number. Null when the plan and the icons do not match or nothing has a size.
    /// </summary>
    public static DragPicture? Compose(DragGhostPlan plan, IReadOnlyList<GhostIcon> icons, double scale, double opacity, GhostColour badge, GhostColour text, GhostInk ink)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(icons);
        if (!double.IsFinite(scale) || scale <= 0 || icons.Count < plan.Icons.Count)
        {
            return null;
        }

        var width = (int)Math.Ceiling(plan.Width * scale);
        var height = (int)Math.Ceiling(plan.Height * scale);
        if (width <= 0 || height <= 0 || width > MaxSide || height > MaxSide)
        {
            return null;
        }

        // The stack is drawn whole first, then faded as one: the icons behind never show through the front one.
        var pixels = new byte[width * height * 4];
        for (var i = plan.Icons.Count - 1; i >= 0; i--)
        {
            if (icons[i].Drawable)
            {
                DrawIcon(pixels, width, height, icons[i], plan.Icons[i], scale, 256);
            }
        }

        var alpha = (int)Math.Round(Math.Clamp(opacity, 0, 1) * 256);
        if (alpha < 256)
        {
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = (byte)((pixels[i] * alpha) >> 8);
            }
        }

        if (plan.Badge is { } mark)
        {
            DrawBadge(pixels, width, height, mark.Box, scale, badge);
            if (ink.Drawable)
            {
                DrawInk(pixels, width, height, mark.Box, scale, text, ink);
            }
        }

        return new DragPicture(
            pixels,
            width,
            height,
            Math.Clamp((int)Math.Round(plan.HotX * scale), 0, width - 1),
            Math.Clamp((int)Math.Round(plan.HotY * scale), 0, height - 1),
            scale);
    }

    /// <summary>No picture is larger than this a side: a plan that asks for more is a mistake, not a drag image.</summary>
    public const int MaxSide = 1024;

    /// <summary>An icon fitted into its square (its shape kept, centred), drawn over what is there at <paramref name="alpha"/> of 256.</summary>
    private static void DrawIcon(byte[] pixels, int width, int height, GhostIcon icon, GhostBox box, double scale, int alpha)
    {
        var side = Math.Max(1, (int)Math.Round(box.Width * scale));
        var fit = Math.Min(side / (double)icon.Width, side / (double)icon.Height);
        var drawnWidth = Math.Max(1, (int)Math.Round(icon.Width * fit));
        var drawnHeight = Math.Max(1, (int)Math.Round(icon.Height * fit));
        var source = drawnWidth == icon.Width && drawnHeight == icon.Height ? icon.Bgra : Resize(icon, drawnWidth, drawnHeight);
        var left = (int)Math.Round(box.X * scale) + ((side - drawnWidth) / 2);
        var top = (int)Math.Round(box.Y * scale) + ((side - drawnHeight) / 2);
        for (var y = 0; y < drawnHeight; y++)
        {
            var toY = top + y;
            if (toY < 0 || toY >= height)
            {
                continue;
            }

            for (var x = 0; x < drawnWidth; x++)
            {
                var toX = left + x;
                if (toX < 0 || toX >= width)
                {
                    continue;
                }

                var from = ((y * drawnWidth) + x) * 4;
                var to = ((toY * width) + toX) * 4;

                // Premultiplied source over premultiplied destination, the source faded first.
                var sourceAlpha = (source[from + 3] * alpha) >> 8;
                var keep = 255 - sourceAlpha;
                for (var c = 0; c < 4; c++)
                {
                    pixels[to + c] = (byte)Math.Min(255, ((source[from + c] * alpha) >> 8) + ((pixels[to + c] * keep) / 255));
                }
            }
        }
    }

    /// <summary>
    /// [S-5565] <paramref name="icon"/> at another size (premultiplied, so colours never bleed out of
    /// transparent pixels): each new pixel is the average of the area of the old picture it covers when
    /// it is drawn smaller, and the blend of its four neighbours when it is drawn larger.
    /// </summary>
    public static byte[] Resize(GhostIcon icon, int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        if (!icon.Drawable)
        {
            throw new ArgumentException("an icon with no pixels cannot be resized", nameof(icon));
        }

        var result = new byte[width * height * 4];
        var stepX = icon.Width / (double)width;
        var stepY = icon.Height / (double)height;
        Span<double> sum = stackalloc double[4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                sum.Clear();
                double weight;
                if (stepX > 1 || stepY > 1)
                {
                    weight = Area(icon, x * stepX, y * stepY, (x + 1) * stepX, (y + 1) * stepY, sum);
                }
                else
                {
                    weight = Blend(icon, ((x + 0.5) * stepX) - 0.5, ((y + 0.5) * stepY) - 0.5, sum);
                }

                var to = ((y * width) + x) * 4;
                for (var c = 0; c < 4; c++)
                {
                    result[to + c] = (byte)Math.Clamp(Math.Round(sum[c] / weight), 0, 255);
                }
            }
        }

        return result;
    }

    /// <summary>The sum of the pixels under the box, each weighed by how much of it the box covers; returns the weights' sum.</summary>
    private static double Area(GhostIcon icon, double left, double top, double right, double bottom, Span<double> sum)
    {
        var total = 0.0;
        for (var y = (int)Math.Floor(top); y < Math.Min(icon.Height, (int)Math.Ceiling(bottom)); y++)
        {
            var tall = Math.Min(bottom, y + 1) - Math.Max(top, y);
            for (var x = (int)Math.Floor(left); x < Math.Min(icon.Width, (int)Math.Ceiling(right)); x++)
            {
                var part = tall * (Math.Min(right, x + 1) - Math.Max(left, x));
                var from = ((y * icon.Width) + x) * 4;
                for (var c = 0; c < 4; c++)
                {
                    sum[c] += icon.Bgra[from + c] * part;
                }

                total += part;
            }
        }

        return total > 0 ? total : 1;
    }

    /// <summary>The four pixels round a point, blended by its distance to each; the edge's pixel repeats past the edge.</summary>
    private static double Blend(GhostIcon icon, double x, double y, Span<double> sum)
    {
        var x0 = (int)Math.Floor(x);
        var y0 = (int)Math.Floor(y);
        var fx = x - x0;
        var fy = y - y0;
        for (var dy = 0; dy < 2; dy++)
        {
            for (var dx = 0; dx < 2; dx++)
            {
                var part = (dx == 0 ? 1 - fx : fx) * (dy == 0 ? 1 - fy : fy);
                var from = ((Math.Clamp(y0 + dy, 0, icon.Height - 1) * icon.Width) + Math.Clamp(x0 + dx, 0, icon.Width - 1)) * 4;
                for (var c = 0; c < 4; c++)
                {
                    sum[c] += icon.Bgra[from + c] * part;
                }
            }
        }

        return 1;
    }

    /// <summary>The badge's rounded box, opaque, its edge smoothed over one pixel.</summary>
    private static void DrawBadge(byte[] pixels, int width, int height, GhostBox box, double scale, GhostColour colour)
    {
        var left = box.X * scale;
        var top = box.Y * scale;
        var right = box.Right * scale;
        var bottom = box.Bottom * scale;
        var radius = Math.Min(BadgeRadius * scale, Math.Min(right - left, bottom - top) / 2);
        for (var y = Math.Max(0, (int)Math.Floor(top)); y < Math.Min(height, (int)Math.Ceiling(bottom)); y++)
        {
            for (var x = Math.Max(0, (int)Math.Floor(left)); x < Math.Min(width, (int)Math.Ceiling(right)); x++)
            {
                // How far the pixel's middle is inside the rounded box, in pixels: half a pixel either side of the edge is the smoothing.
                var px = x + 0.5;
                var py = y + 0.5;
                var dx = Math.Max(Math.Max(left + radius - px, px - (right - radius)), 0);
                var dy = Math.Max(Math.Max(top + radius - py, py - (bottom - radius)), 0);
                var inside = dx > 0 || dy > 0
                    ? radius - Math.Sqrt((dx * dx) + (dy * dy))
                    : Math.Min(Math.Min(px - left, right - px), Math.Min(py - top, bottom - py));
                var cover = Math.Clamp(inside + 0.5, 0, 1);
                if (cover > 0)
                {
                    Over(pixels, ((y * width) + x) * 4, colour, (int)Math.Round(cover * 255));
                }
            }
        }
    }

    /// <summary>The badge's number, centred in its box.</summary>
    private static void DrawInk(byte[] pixels, int width, int height, GhostBox box, double scale, GhostColour colour, GhostInk ink)
    {
        var left = (int)Math.Round(((box.X + (box.Width / 2)) * scale) - (ink.Width / 2.0));
        var top = (int)Math.Round(((box.Y + (box.Height / 2)) * scale) - (ink.Height / 2.0));
        for (var y = 0; y < ink.Height; y++)
        {
            var toY = top + y;
            if (toY < 0 || toY >= height)
            {
                continue;
            }

            for (var x = 0; x < ink.Width; x++)
            {
                var toX = left + x;
                if (toX >= 0 && toX < width && ink.Coverage[(y * ink.Width) + x] is var cover and > 0)
                {
                    Over(pixels, ((toY * width) + toX) * 4, colour, cover);
                }
            }
        }
    }

    /// <summary>An opaque colour at <paramref name="cover"/> of 255 over a premultiplied pixel.</summary>
    private static void Over(byte[] pixels, int at, GhostColour colour, int cover)
    {
        var keep = 255 - cover;
        pixels[at] = (byte)(((colour.B * cover) + (pixels[at] * keep)) / 255);
        pixels[at + 1] = (byte)(((colour.G * cover) + (pixels[at + 1] * keep)) / 255);
        pixels[at + 2] = (byte)(((colour.R * cover) + (pixels[at + 2] * keep)) / 255);
        pixels[at + 3] = (byte)(cover + ((pixels[at + 3] * keep) / 255));
    }
}
