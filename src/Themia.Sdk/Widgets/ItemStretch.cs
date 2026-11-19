using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-12410] One item along an axis: the length it is drawn at by itself (<see cref="Natural"/>) and the
/// most it may stretch to (<see cref="Max"/>, at least the natural; infinite for a part that takes any
/// room, a chart's plot). [S-12807] <see cref="Band"/> is the empty edge at each of its ends at its natural
/// length (a row's room over and under its centred text); a stretch adds to it evenly.
/// </summary>
public readonly record struct StretchItem(double Natural, double Max, double Band = 0)
{
    /// <summary>[S-12807] A family row of <paramref name="lines"/> lines: its height, up to <see cref="ItemStretch.RowCap"/> times, its band.</summary>
    public static StretchItem Row(int lines) => new(FamilyShape.RowHeight(lines), FamilyShape.RowHeight(lines) * ItemStretch.RowCap, FamilyShape.RowBand(lines));

    /// <summary>An item that never stretches: a text line, a readout, a button.</summary>
    public static StretchItem Fixed(double natural) => new(natural, natural);

    /// <summary>An item that takes any room it is given: a chart's plot, a tile's face.</summary>
    public static StretchItem Free(double natural) => new(natural, double.PositiveInfinity);
}

/// <summary>
/// [S-12807] Rows of one pitch laid by <see cref="ItemStretch.Reach(double, int, double, double, double, double, double)"/>:
/// each row's height (<see cref="Pitch"/>), how far the first row's box starts before the room's start
/// (<see cref="Over"/>: its band, so its text stands on the start; less where one row is centred) and the
/// spacing between two rows past their cap (<see cref="Gap"/>).
/// </summary>
public readonly record struct RowReach(double Pitch, double Over, double Gap)
{
    /// <summary>The length <paramref name="count"/> rows take, first box to last box.</summary>
    public double Length(int count) => count <= 0 ? 0 : (count * Pitch) + ((count - 1) * Gap);
}

/// <summary>[S-12410] Where an item is drawn along the axis: its start and its length, in DIPs from the axis's start.</summary>
public readonly record struct ItemSlot(double Start, double Length)
{
    public double End => Start + Length;
}

/// <summary>
/// [S-12410] [S-12400] The one rule for a widget's items in a box bigger than they need (owner, 2026-11-15:
/// "stretching their contents to some extent … but not overstretch the items, they should still only have
/// a natural size"). [S-12807] Along one axis a list's first and last content reach its ends and the spare
/// room between them stretches the items' bands toward their max (never past the inset); what they cannot
/// take is spacing between them (<see cref="Reach(double, IReadOnlyList{StretchItem}, double, double, double)"/>).
/// Short of room the items keep their natural lengths one after another from the start: the stretch never
/// shrinks an item, adds one or drops one ([S-11800]).
/// <para>
/// Every share is whole device pixels at the display's scale (the left-over pixels to the first items, one
/// each), so items land on pixels; the sub-pixel rest is left at the end.
/// </para>
/// <para>
/// The caps per block (docs/WIDGET-KIT.md 17.4): a row and a cell up to <see cref="RowCap"/> × natural; a
/// text, a readout, a status line, a button its natural (the type never grows with the box); a chart's plot,
/// a tile's face, a picture any room (<see cref="StretchItem.Free"/>).
/// </para>
/// </summary>
public static class ItemStretch
{
    /// <summary>The most a row (or a grid's cell) stretches: half again its natural height.</summary>
    public const double RowCap = 1.5;

    /// <summary>
    /// [S-12807] The one rule for a list of rows (owner, 2026-11-19: "the items should reach the top and
    /// bottom"): the first item's content stands on the room's start and the last item's on its end, so no band
    /// is left between a list's text and the inset ([S-12800]). <paramref name="available"/> is the room the
    /// contents span: the content's top (the card's inset line) to the foot's inset line. What the contents
    /// leave goes between them; each item takes up to half the room on either side of it as its
    /// <see cref="StretchItem.Band"/>, at most its <see cref="StretchItem.Max"/> and never more than
    /// <paramref name="bleed"/> (the inset: the end items' bands reach into it, so a hover fill stays inside
    /// the card); the rest is spacing. Slots may start before 0 and end past <paramref name="available"/> by the
    /// end items' bands. One item is centred in the room; short of room (less than the items' own bands between
    /// them) the items keep their natural lengths one after another, the first's content on the start. Whole
    /// device pixels (the first gaps a pixel more; the sub-pixel rest is left at the end).
    /// </summary>
    public static ItemSlot[] Reach(double available, IReadOnlyList<StretchItem> items, double gap = 0, double bleed = FamilySpace.Inset, double scale = 1)
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
        bleed = double.IsFinite(bleed) ? Math.Max(0, bleed) : 0;
        var natural = new double[n];
        var band = new double[n];
        var most = new double[n];
        var need = (n - 1) * gap;
        for (var i = 0; i < n; i++)
        {
            natural[i] = double.IsFinite(items[i].Natural) ? Math.Max(0, items[i].Natural) : 0;
            band[i] = double.IsFinite(items[i].Band) ? Math.Clamp(items[i].Band, 0, natural[i] / 2) : 0;
            var max = double.IsNaN(items[i].Max) ? natural[i] : Math.Max(natural[i], items[i].Max);
            most[i] = Math.Min(band[i] + ((max - natural[i]) / 2), Math.Max(band[i], bleed));
            need += natural[i] - (2 * band[i]);
        }

        var spare = double.IsFinite(available) ? available - need : double.NaN;
        if (n == 1)
        {
            var content = natural[0] - (2 * band[0]);
            if (!(spare >= 2 * band[0]))
            {
                slots[0] = new ItemSlot(-band[0], natural[0]);
                return slots;
            }

            var off = Math.Floor(((spare / 2) * scale) + 1e-6) / scale;
            var halo = Math.Clamp(off, band[0], most[0]);
            slots[0] = new ItemSlot(off - halo, content + (2 * halo));
            return slots;
        }

        // The room between two contents in whole pixels (the first ones a pixel more), then each side's part of it.
        var pixels = spare > 0 ? Math.Floor((spare * scale) + 1e-6) : 0;
        var per = new double[n - 1];
        var fits = true;
        for (var j = 0; j < n - 1; j++)
        {
            per[j] = Share(pixels, n - 1, j) / scale;
            fits &= per[j] >= band[j] + band[j + 1] - 1e-6;
        }

        if (!fits)
        {
            var at = -band[0];
            for (var i = 0; i < n; i++)
            {
                slots[i] = new ItemSlot(at, natural[i]);
                at += natural[i] + gap;
            }

            return slots;
        }

        var ahead = new double[n - 1];
        for (var j = 0; j < n - 1; j++)
        {
            var half = Math.Floor(((per[j] / 2) * scale) + 1e-6) / scale;
            ahead[j] = Math.Clamp(half, band[j], per[j] - band[j + 1]);
        }

        var start = 0.0;
        for (var i = 0; i < n; i++)
        {
            // A band is the same over and under its content: the lesser of the two sides' parts.
            var under = i < n - 1 ? ahead[i] : per[n - 2] - ahead[n - 2];
            var over = i > 0 ? per[i - 1] - ahead[i - 1] : ahead[0];
            var halo = Math.Clamp(Math.Min(under, over), band[i], most[i]);
            var content = natural[i] - (2 * band[i]);
            slots[i] = new ItemSlot(start - halo, content + (2 * halo));
            start += content + (i < n - 1 ? gap + per[i] : 0);
        }

        return slots;
    }

    /// <summary>
    /// [S-12807] <see cref="Reach(double, IReadOnlyList{StretchItem}, double, double, double)"/> for
    /// <paramref name="count"/> like rows (a list's, which share one pitch): <paramref name="natural"/> tall
    /// with <paramref name="band"/> over and under their text, stretched up to <paramref name="cap"/> times;
    /// every gap's room the same, whole pixels.
    /// </summary>
    public static RowReach Reach(double available, int count, double natural, double band, double cap = RowCap, double bleed = FamilySpace.Inset, double scale = 1)
    {
        natural = double.IsFinite(natural) ? Math.Max(0, natural) : 0;
        band = double.IsFinite(band) ? Math.Clamp(band, 0, natural / 2) : 0;
        cap = double.IsFinite(cap) ? Math.Max(1, cap) : 1;
        if (count <= 0 || natural <= 0)
        {
            return new RowReach(natural, band, 0);
        }

        if (count == 1)
        {
            var one = Reach(available, [new StretchItem(natural, natural * cap, band)], 0, bleed, scale)[0];
            return new RowReach(one.Length, -one.Start, 0);
        }

        scale = double.IsFinite(scale) && scale > 0 ? scale : 1;
        bleed = double.IsFinite(bleed) ? Math.Max(0, bleed) : 0;
        var content = natural - (2 * band);
        var room = double.IsFinite(available) ? available - (count * content) : double.NaN;
        var per = room > 0 ? Math.Floor(((room / (count - 1)) * scale) + 1e-6) / scale : 0;
        if (!(per >= (2 * band) - 1e-6))
        {
            return new RowReach(natural, band, 0);
        }

        var most = Math.Min(band + ((natural * (cap - 1)) / 2), Math.Max(band, bleed));
        var halo = Math.Clamp(Math.Floor(((per / 2) * scale) + 1e-6) / scale, band, most);
        return new RowReach(content + (2 * halo), halo, Math.Max(0, per - (2 * halo)));
    }

    /// <summary>
    /// [S-12807] A list's <paramref name="count"/> rows of <paramref name="lines"/> lines in a view
    /// <paramref name="height"/> tall (the card's inset over it, the list from its top; less
    /// <paramref name="reserved"/> for what stands under or over the rows): their text from the view's top to
    /// the foot's inset line, as <see cref="Reach(double, int, double, double, double, double, double)"/> lays
    /// them. A list applies it as its rows' height (<see cref="RowReach.Pitch"/>), its first row's band over
    /// its top (a margin of <c>-</c><see cref="RowReach.Over"/>) and the gap between rows past their cap.
    /// </summary>
    public static RowReach Rows(double height, int count, int lines, double reserved = 0, double scale = 1)
        => Reach(height - FamilySpace.Inset - reserved, count, FamilyShape.RowHeight(lines), FamilyShape.RowBand(lines), scale: scale);

    /// <summary>
    /// [S-12807] <see cref="Rows"/> with a row of <paramref name="footLines"/> lines standing right under them (an
    /// add row, [S-3850]): its text on the foot's inset line, the rows' text from the view's top down to it.
    /// </summary>
    public static RowReach RowsAbove(double height, int count, int lines, int footLines = 1, double scale = 1)
    {
        // The add row's box from its top to its text's end; the rows' last band stands over it.
        var foot = FootRow(footLines);
        var first = Rows(height, count, lines, foot + FamilyShape.RowBand(lines), scale);
        return Rows(height, count, lines, foot + first.Over, scale);
    }

    /// <summary>[S-12807] What a row of <paramref name="lines"/> lines standing under a list takes over the foot's inset line: its height less its band under its text.</summary>
    public static double FootRow(int lines = 1) => FamilyShape.RowHeight(lines) - FamilyShape.RowBand(lines);

    /// <summary>The <paramref name="i"/>th of <paramref name="parts"/> whole-pixel shares of <paramref name="pixels"/>, the first ones a pixel more.</summary>
    private static double Share(double pixels, int parts, int i)
    {
        var each = Math.Floor(pixels / parts);
        return each + (i < pixels - (each * parts) ? 1 : 0);
    }
}
