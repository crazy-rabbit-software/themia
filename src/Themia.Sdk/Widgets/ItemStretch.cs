using Themia.Sdk.Kit;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-12410] One item along an axis: the length it is drawn at by itself (<see cref="Natural"/>) and the
/// most it may stretch to (<see cref="Max"/>, at least the natural; infinite for a part that takes any
/// room, a chart's plot). [S-12807] <see cref="Band"/> is the empty edge at each of its ends at its natural
/// length (a row's room over and under its centred text); a stretch adds to it evenly. <see cref="Ink"/> is the
/// room its content's box holds over its first ink and under its last (a text's leading, a glyph's room in its
/// line): a list stands that ink on its ends.
/// </summary>
public readonly record struct StretchItem(double Natural, double Max, double Band = 0, LineInk Ink = default)
{
    /// <summary>[S-12807] A family row of <paramref name="lines"/> lines led by a <paramref name="lead"/> (0: none): its height, up to <see cref="ItemStretch.RowCap"/> times, its band and its ink.</summary>
    public static StretchItem Row(int lines, double lead = 0)
        => new(FamilyShape.RowHeight(lines), FamilyShape.RowHeight(lines) * ItemStretch.RowCap, FamilyShape.RowBand(lines), LineInk.Row(lines, lead));

    /// <summary>An item that never stretches: a text line, a readout, a button.</summary>
    public static StretchItem Fixed(double natural, LineInk ink = default) => new(natural, natural, 0, ink);

    /// <summary>An item that takes any room it is given: a chart's plot, a tile's face.</summary>
    public static StretchItem Free(double natural) => new(natural, double.PositiveInfinity);
}

/// <summary>
/// [S-12807] Rows of one pitch laid by <see cref="ItemStretch.Reach(double, int, double, double, double, double, double, LineInk)"/>:
/// each row's height (<see cref="Pitch"/>), how far the first row's box starts before the room's start
/// (<see cref="Over"/>: its band and its text's leading over its first ink, so that ink stands on the start; less
/// where one row is centred), the spacing between two rows past their cap (<see cref="Gap"/>) and how far the
/// last row's box ends past the room's end (<see cref="Under"/>).
/// </summary>
public readonly record struct RowReach(double Pitch, double Over, double Gap, double Under)
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
/// a natural size"). [S-12807] Along one axis a list's first and last ink reach its ends and the spare
/// room between them stretches the items' bands toward their max (never past the inset); what they cannot
/// take is spacing between them (<see cref="Reach(double, IReadOnlyList{StretchItem}, double, double, double)"/>).
/// Only when the items' contents themselves do not fit do they keep their natural lengths one after another
/// from the start: the stretch never adds an item or drops one ([S-11800]).
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
    /// bottom"; "they still have a little / small gap. they should stretch if they dont reach all the way"):
    /// the first item's ink (<see cref="StretchItem.Ink"/>: its capitals or its glyph, not its line's leading)
    /// stands on the room's start and the last item's ink on its end, so no band is left between a list's ink
    /// and the inset ([S-12800]). <paramref name="available"/> is the room the ink spans: the content's top (the
    /// card's inset line) to the foot's inset line. What the contents leave goes between them; each item takes up
    /// to half the room on either side of it as its <see cref="StretchItem.Band"/>, at most its
    /// <see cref="StretchItem.Max"/> and never further over the end than <paramref name="bleed"/> (the inset: the
    /// end items' bands reach into it, so a hover fill stays inside the card); the rest is spacing. Less room
    /// between two contents than their bands narrows the bands, the contents still reaching the ends. Slots may
    /// start before 0 and end past <paramref name="available"/>. One item is centred in the room; when the
    /// contents themselves do not fit, the items keep their natural lengths one after another, the first's ink on
    /// the start. Whole device pixels (the first gaps a pixel more; the sub-pixel rest is left at the end).
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
            need += natural[i] - (2 * band[i]);
        }

        // The ink's leading at the ends: the boxes' contents span that much more than the ink does.
        var over = Lead(items[0].Ink.Over, natural[0] - (2 * band[0]));
        var under = Lead(items[n - 1].Ink.Under, natural[n - 1] - (2 * band[n - 1]));
        var edge = Math.Max(0, bleed - Math.Max(over, under));
        for (var i = 0; i < n; i++)
        {
            var max = double.IsNaN(items[i].Max) ? natural[i] : Math.Max(natural[i], items[i].Max);
            most[i] = Math.Min(band[i] + ((max - natural[i]) / 2), Math.Max(band[i], edge));
        }

        var spare = double.IsFinite(available) ? available + over + under - need : double.NaN;
        if (n == 1)
        {
            var content = natural[0] - (2 * band[0]);
            if (!(spare >= 0))
            {
                slots[0] = new ItemSlot(-band[0] - over, natural[0]);
                return slots;
            }

            var off = Floor(spare / 2, scale);
            var halo = off >= band[0] ? Math.Min(off, Math.Max(band[0], most[0])) : off;
            slots[0] = new ItemSlot(off - halo - over, content + (2 * halo));
            return slots;
        }

        if (!(spare >= 0))
        {
            var at = -band[0] - over;
            for (var i = 0; i < n; i++)
            {
                slots[i] = new ItemSlot(at, natural[i]);
                at += natural[i] + gap;
            }

            return slots;
        }

        // The room between two contents in whole pixels (the first ones a pixel more), then each side's part of it.
        var pixels = Math.Floor((spare * scale) + 1e-6);
        var per = new double[n - 1];
        var ahead = new double[n - 1];
        for (var j = 0; j < n - 1; j++)
        {
            per[j] = Share(pixels, n - 1, j) / scale;
            var half = Floor(per[j] / 2, scale);
            ahead[j] = per[j] >= band[j] + band[j + 1] - 1e-6 ? Math.Clamp(half, band[j], per[j] - band[j + 1]) : half;
        }

        var start = -over;
        for (var i = 0; i < n; i++)
        {
            // A band is the same over and under its content: the lesser of the two sides' parts, narrower than
            // its own where the room between is.
            var below = i < n - 1 ? ahead[i] : per[n - 2] - ahead[n - 2];
            var above = i > 0 ? per[i - 1] - ahead[i - 1] : ahead[0];
            var room = Math.Max(0, Math.Min(below, above));
            var halo = room >= band[i] ? Math.Min(room, Math.Max(band[i], most[i])) : room;
            var content = natural[i] - (2 * band[i]);
            slots[i] = new ItemSlot(start - halo, content + (2 * halo));
            start += content + (i < n - 1 ? gap + per[i] : 0);
        }

        return slots;
    }

    /// <summary>
    /// [S-12807] <see cref="Reach(double, IReadOnlyList{StretchItem}, double, double, double)"/> for
    /// <paramref name="count"/> like rows (a list's, which share one pitch): <paramref name="natural"/> tall
    /// with <paramref name="band"/> over and under their text and <paramref name="ink"/>'s leading inside it,
    /// stretched up to <paramref name="cap"/> times in whole pixels; every gap's room the same, to the end exactly.
    /// </summary>
    public static RowReach Reach(double available, int count, double natural, double band, double cap = RowCap, double bleed = FamilySpace.Inset, double scale = 1, LineInk ink = default)
    {
        natural = double.IsFinite(natural) ? Math.Max(0, natural) : 0;
        band = double.IsFinite(band) ? Math.Clamp(band, 0, natural / 2) : 0;
        cap = double.IsFinite(cap) ? Math.Max(1, cap) : 1;
        var content = natural - (2 * band);
        var over = Lead(ink.Over, content);
        var under = Lead(ink.Under, content);
        if (count <= 0 || natural <= 0)
        {
            return new RowReach(natural, band + over, 0, band + under);
        }

        if (count == 1)
        {
            var one = Reach(available, [new StretchItem(natural, natural * cap, band, new LineInk(over, under))], 0, bleed, scale)[0];
            return new RowReach(one.Length, -one.Start, 0, double.IsFinite(available) ? one.End - available : band + under);
        }

        scale = double.IsFinite(scale) && scale > 0 ? scale : 1;
        bleed = double.IsFinite(bleed) ? Math.Max(0, bleed) : 0;
        var room = double.IsFinite(available) ? available + over + under - (count * content) : double.NaN;
        if (!(room >= 0))
        {
            return new RowReach(natural, band + over, 0, band + under);
        }

        // Each row's band whole pixels (so a row's height is); the room between the rows exact, so the last ink
        // lands on the end (the layout rounds where each row stands).
        var per = room / (count - 1);
        var most = Math.Min(band + ((natural * (cap - 1)) / 2), Math.Max(band, Math.Max(0, bleed - Math.Max(over, under))));
        var half = Floor(per / 2, scale);
        var halo = half >= band ? Math.Min(half, most) : half;
        return new RowReach(content + (2 * halo), halo + over, Math.Max(0, per - (2 * halo)), halo + under);
    }

    /// <summary>
    /// [S-12807] A list's <paramref name="count"/> rows of <paramref name="lines"/> lines, led by a
    /// <paramref name="lead"/> (0: none), in a view <paramref name="height"/> tall (the card's inset over it, the
    /// list from its top; less <paramref name="reserved"/> for what stands under or over the rows): their ink
    /// from the view's top to the foot's inset line, as
    /// <see cref="Reach(double, int, double, double, double, double, double, LineInk)"/> lays them. A list applies
    /// it as its rows' height (<see cref="RowReach.Pitch"/>), its first row's box over its top (a margin of
    /// <c>-</c><see cref="RowReach.Over"/>) and the gap between rows past their cap.
    /// </summary>
    public static RowReach Rows(double height, int count, int lines, double reserved = 0, double scale = 1, double lead = 0)
        => Reach(height - FamilySpace.Inset - reserved, count, FamilyShape.RowHeight(lines), FamilyShape.RowBand(lines), scale: scale, ink: LineInk.Row(lines, lead));

    /// <summary>[S-12807] Natural rows of <paramref name="lines"/> lines led by a <paramref name="lead"/>, before a list's box is measured: their ink from its top.</summary>
    public static RowReach Natural(int lines, double lead = 0)
        => Reach(double.NaN, 2, FamilyShape.RowHeight(lines), FamilyShape.RowBand(lines), ink: LineInk.Row(lines, lead));

    /// <summary>
    /// [S-12807] <see cref="Rows"/> with a row of <paramref name="footLines"/> lines standing right under them (an
    /// add row, [S-3850], led by its 16 glyph): its ink on the foot's inset line, the rows' ink from the view's top
    /// down to its box.
    /// </summary>
    public static RowReach RowsAbove(double height, int count, int lines, int footLines = 1, double scale = 1, double lead = 0)
    {
        // The add row's box from its top to its ink's end; the rows' last band and leading stand over it.
        var foot = FootRow(footLines);
        var first = Rows(height, count, lines, foot + FamilyShape.RowBand(lines) + LineInk.Row(lines, lead).Under, scale, lead);
        return Rows(height, count, lines, foot + first.Under, scale, lead);
    }

    /// <summary>
    /// [S-12807] What a row of <paramref name="lines"/> lines led by a 16 glyph (an add row) standing under a list
    /// takes over the foot's inset line: its height less its band and its leading under its ink.
    /// </summary>
    public static double FootRow(int lines = 1)
        => FamilyShape.RowHeight(lines) - FamilyShape.RowBand(lines) - LineInk.Row(lines, FamilyShape.Icon).Under;

    /// <summary>An end's leading made safe: finite, and never more than half the content it is in.</summary>
    private static double Lead(double ink, double content)
        => double.IsFinite(ink) ? Math.Clamp(ink, -content, Math.Max(0, content / 2)) : 0;

    private static double Floor(double length, double scale) => Math.Floor((length * scale) + 1e-6) / scale;

    /// <summary>The <paramref name="i"/>th of <paramref name="parts"/> whole-pixel shares of <paramref name="pixels"/>, the first ones a pixel more.</summary>
    private static double Share(double pixels, int parts, int i)
    {
        var each = Math.Floor(pixels / parts);
        return each + (i < pixels - (each * parts) ? 1 : 0);
    }
}
