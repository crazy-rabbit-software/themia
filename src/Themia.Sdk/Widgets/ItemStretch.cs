namespace Themia.Core.Widgets;

/// <summary>
/// [S-12410] One item along an axis: the length it is drawn at by itself (<see cref="Natural"/>) and the
/// most it may stretch to (<see cref="Max"/>, at least the natural; infinite for a part that takes any
/// room, a chart's plot).
/// </summary>
public readonly record struct StretchItem(double Natural, double Max)
{
    /// <summary>An item that never stretches: a text line, a readout, a button.</summary>
    public static StretchItem Fixed(double natural) => new(natural, natural);

    /// <summary>An item that takes any room it is given: a chart's plot, a tile's face.</summary>
    public static StretchItem Free(double natural) => new(natural, double.PositiveInfinity);

    /// <summary>An item that stretches up to <paramref name="cap"/> times its natural length (a row: <see cref="ItemStretch.RowCap"/>).</summary>
    public static StretchItem Capped(double natural, double cap) => new(natural, natural * Math.Max(1, cap));
}

/// <summary>[S-12410] Where the room the items cannot take (each at its max) goes.</summary>
public enum StretchRest
{
    /// <summary>Shared evenly round the items, each centred in its share: the spacing grows evenly ([S-12400]).</summary>
    Around,

    /// <summary>Between the items, the first and the last at the ends (one item: centred).</summary>
    Between,

    /// <summary>Before and after the items, which stay together.</summary>
    Centre,

    /// <summary>After the items.</summary>
    Start,

    /// <summary>Before the items.</summary>
    End,
}

/// <summary>[S-12410] Where an item is drawn along the axis: its start and its length, in DIPs from the axis's start.</summary>
public readonly record struct ItemSlot(double Start, double Length)
{
    public double End => Start + Length;
}

/// <summary>
/// [S-12410] [S-12400] The one rule for a widget's items in a box bigger than they need (owner, 2026-11-15:
/// "stretching their contents to some extent … but not overstretch the items, they should still only have
/// a natural size"). Along one axis the spare room first stretches the items toward their max, evenly
/// (an item at its max stops, the others go on); what they cannot take is spacing, as
/// <see cref="StretchRest"/> says. Short of room the items keep their natural lengths one after another
/// from the start: the stretch never shrinks an item, adds one or drops one ([S-11800]).
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
    /// Each item's place along the axis in <paramref name="available"/> DIPs, <paramref name="gap"/> apart
    /// (the family's gap, which the rest adds to).
    /// </summary>
    public static ItemSlot[] Lay(double available, IReadOnlyList<StretchItem> items, double gap = 0, StretchRest rest = StretchRest.Around, double scale = 1)
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
        var room = new double[n];
        var need = (n - 1) * gap;
        for (var i = 0; i < n; i++)
        {
            natural[i] = double.IsFinite(items[i].Natural) ? Math.Max(0, items[i].Natural) : 0;
            var max = double.IsNaN(items[i].Max) ? natural[i] : Math.Max(natural[i], items[i].Max);

            // How many whole pixels the item may still grow by (a hair under one is that pixel).
            room[i] = double.IsPositiveInfinity(max) ? double.PositiveInfinity : Math.Floor(((max - natural[i]) * scale) + 1e-6);
            need += natural[i];
        }

        var spare = double.IsFinite(available) ? available - need : 0;
        var pixels = spare > 0 ? Math.Floor((spare * scale) + 1e-6) : 0;
        var grow = Grow(room, ref pixels);

        // What the items cannot take goes round them, in whole pixels (what is not before an item is after the last).
        var before = new double[n];
        switch (pixels > 0 ? rest : StretchRest.Start)
        {
            case StretchRest.Around:
                for (var i = 0; i < n; i++)
                {
                    var share = Share(pixels, n, i);
                    before[i] += Math.Floor(share / 2);
                    if (i + 1 < n)
                    {
                        before[i + 1] += share - Math.Floor(share / 2);
                    }
                }

                break;
            case StretchRest.Between when n > 1:
                for (var i = 1; i < n; i++)
                {
                    before[i] = Share(pixels, n - 1, i - 1);
                }

                break;
            case StretchRest.Between:
            case StretchRest.Centre:
                before[0] = Math.Floor(pixels / 2);
                break;
            case StretchRest.End:
                before[0] = pixels;
                break;
            default:
                break;
        }

        var at = 0.0;
        for (var i = 0; i < n; i++)
        {
            at += before[i] / scale;
            var length = natural[i] + (grow[i] / scale);
            slots[i] = new ItemSlot(at, length);
            at += length + gap;
        }

        return slots;
    }

    /// <summary>
    /// [S-12410] One length for <paramref name="count"/> like items (a list's rows, which share one pitch) in
    /// <paramref name="available"/>: their natural length stretched to fill it, at most <paramref name="cap"/>
    /// times, whole device pixels; never under the natural.
    /// </summary>
    public static double Pitch(double available, int count, double natural, double cap = RowCap, double scale = 1)
    {
        if (count <= 0 || !double.IsFinite(natural) || natural <= 0)
        {
            return Math.Max(0, double.IsFinite(natural) ? natural : 0);
        }

        scale = double.IsFinite(scale) && scale > 0 ? scale : 1;
        var most = natural * Math.Max(1, double.IsFinite(cap) ? cap : 1);
        var each = double.IsFinite(available) ? Math.Floor(((available / count) * scale) + 1e-6) / scale : natural;
        return Math.Clamp(each, natural, most);
    }

    /// <summary>Hands <paramref name="pixels"/> out evenly to the items with room left, whole pixels; what none can take stays in it.</summary>
    private static double[] Grow(double[] room, ref double pixels)
    {
        var grow = new double[room.Length];
        while (pixels > 0)
        {
            var open = Enumerable.Range(0, room.Length).Where(i => room[i] - grow[i] > 0).ToList();
            if (open.Count == 0)
            {
                break;
            }

            var each = Math.Floor(pixels / open.Count);
            if (each == 0)
            {
                // Fewer pixels than open items: one each to the first.
                foreach (var i in open.Take((int)pixels))
                {
                    grow[i]++;
                    pixels--;
                }

                break;
            }

            foreach (var i in open)
            {
                var take = Math.Min(each, room[i] - grow[i]);
                grow[i] += take;
                pixels -= take;
            }
        }

        return grow;
    }

    /// <summary>The <paramref name="i"/>th of <paramref name="parts"/> whole-pixel shares of <paramref name="pixels"/>, the first ones a pixel more.</summary>
    private static double Share(double pixels, int parts, int i)
    {
        var each = Math.Floor(pixels / parts);
        return each + (i < pixels - (each * parts) ? 1 : 0);
    }
}
