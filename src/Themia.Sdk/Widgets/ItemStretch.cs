using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-12410] One item along an axis: the length it is drawn at by itself (<see cref="Natural"/>) and whether it
/// grows with the room (<see cref="Grows"/>: a row, a chart's plot, a tile's face) or keeps its length (a text, a
/// readout, a button).
/// </summary>
public readonly record struct StretchItem(double Natural, bool Grows)
{
    /// <summary>[S-12807] A family row of <paramref name="lines"/> lines: its natural height, growing with the room.</summary>
    public static StretchItem Row(int lines) => new(FamilyShape.RowHeight(lines), true);

    /// <summary>An item that never stretches: a text line, a readout, a button.</summary>
    public static StretchItem Fixed(double natural) => new(natural, false);

    /// <summary>An item that takes any room it is given: a chart's plot, a tile's face.</summary>
    public static StretchItem Free(double natural) => new(natural, true);
}

/// <summary>[S-12807] Rows of one pitch laid by <see cref="ItemStretch.Rows"/>: each row's height, the rows touching.</summary>
public readonly record struct RowReach(double Pitch)
{
    /// <summary>The length <paramref name="count"/> rows take, first box to last box.</summary>
    public double Length(int count) => count <= 0 ? 0 : count * Pitch;
}

/// <summary>[S-12410] Where an item is drawn along the axis: its start and its length, in DIPs from the axis's start.</summary>
public readonly record struct ItemSlot(double Start, double Length)
{
    public double End => Start + Length;
}

/// <summary>
/// [S-12410] [S-12400] [S-12807] The one rule for a widget's items in a box (owner, 2026-11-19: "the items have no
/// spacing between them and they try to take up the whole space from top to bottom and left to right"): along an axis
/// the items that grow (rows, charts, tiles) share the room their natural lengths leave, evenly, so they touch one
/// another and the room's ends; the items that do not (a text, a readout, a button) keep their length. With nothing
/// that grows, the items stand together, centred in the room. Short of room the items keep their natural lengths one
/// after another from the start: the stretch never shrinks an item, adds one or drops one ([S-11800]).
/// <para>
/// Every share is whole device pixels at the display's scale (the left-over pixels to the first items, one each),
/// so items land on pixels.
/// </para>
/// </summary>
public static class ItemStretch
{
    /// <summary>
    /// [S-12807] Lay <paramref name="items"/> (with <paramref name="gap"/> between two, a family token) in a room
    /// <paramref name="available"/> long.
    /// </summary>
    public static ItemSlot[] Reach(double available, IReadOnlyList<StretchItem> items, double gap = 0, double scale = 1)
    {
        ArgumentNullException.ThrowIfNull(items);
        var n = items.Count;
        var slots = new ItemSlot[n];
        if (n == 0)
        {
            return slots;
        }

        scale = double.IsFinite(scale) && scale > 0 ? scale : 1;
        gap = double.IsFinite(gap) ? Math.Max(0, gap) : 0;
        var natural = new double[n];
        var need = (n - 1) * gap;
        var growing = 0;
        for (var i = 0; i < n; i++)
        {
            natural[i] = double.IsFinite(items[i].Natural) ? Math.Max(0, items[i].Natural) : 0;
            need += natural[i];
            growing += items[i].Grows ? 1 : 0;
        }

        var spare = double.IsFinite(available) ? available - need : 0;
        var pixels = spare > 0 ? Math.Floor((spare * scale) + 1e-6) : 0;
        var at = growing == 0 && pixels > 0 ? Math.Floor(pixels / 2) / scale : 0;
        var share = 0;
        for (var i = 0; i < n; i++)
        {
            var length = natural[i];
            if (items[i].Grows && growing > 0)
            {
                length += Share(pixels, growing, share++) / scale;
            }

            slots[i] = new ItemSlot(at, length);
            at += length + gap;
        }

        return slots;
    }

    /// <summary>
    /// [S-12807] <paramref name="count"/> like rows of <paramref name="natural"/> height in a room
    /// <paramref name="available"/> long: each as tall as the room over the rows (whole pixels, the rest at the end),
    /// never less than natural; the rows touch.
    /// </summary>
    public static RowReach Reach(double available, int count, double natural, double scale = 1)
    {
        natural = double.IsFinite(natural) ? Math.Max(0, natural) : 0;
        scale = double.IsFinite(scale) && scale > 0 ? scale : 1;
        if (count <= 0 || !double.IsFinite(available))
        {
            return new RowReach(natural);
        }

        return new RowReach(Math.Max(natural, Math.Floor(((available / count) * scale) + 1e-6) / scale));
    }

    /// <summary>
    /// [S-12807] A list's <paramref name="count"/> rows of <paramref name="lines"/> lines in a view
    /// <paramref name="height"/> tall (the card's inset over it, the list from its top; less
    /// <paramref name="reserved"/> for what stands under or over the rows): their boxes from the view's top to the
    /// foot's inset line, touching. A list applies it as its rows' height (<see cref="RowReach.Pitch"/>).
    /// </summary>
    public static RowReach Rows(double height, int count, int lines, double reserved = 0, double scale = 1)
        => Reach(height - FamilySpace.Inset - reserved, count, FamilyShape.RowHeight(lines), scale);

    /// <summary>[S-12807] <see cref="Rows"/> with an add row of <paramref name="footLines"/> lines standing right under them ([S-3850]).</summary>
    public static RowReach RowsAbove(double height, int count, int lines, int footLines = 1, double scale = 1)
        => Rows(height, count, lines, FamilyShape.RowHeight(footLines), scale);

    /// <summary>[S-12807] Natural rows of <paramref name="lines"/> lines, before a list's box is measured.</summary>
    public static RowReach Natural(int lines) => new(FamilyShape.RowHeight(lines));

    /// <summary>The <paramref name="i"/>th of <paramref name="parts"/> whole-pixel shares of <paramref name="pixels"/>, the first ones a pixel more.</summary>
    private static double Share(double pixels, int parts, int i)
    {
        var each = Math.Floor(pixels / parts);
        return each + (i < pixels - (each * parts) ? 1 : 0);
    }
}
