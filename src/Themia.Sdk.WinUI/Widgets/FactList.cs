using Microsoft.UI.Xaml;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;

namespace Themia.App.Widgets;

/// <summary>[S-11170] [S-12811] One fact as drawn: its 16 lead glyph (empty: none), its name, its value, and whether it can be read now.</summary>
public readonly record struct FactLine(string Glyph, string Name, string Value, bool Available = true)
{
    /// <summary>[S-12811] A detail fact as a row: its name and value, no glyph of its own.</summary>
    public static FactLine Of(DetailFact fact) => new(string.Empty, fact.Label, fact.Value, fact.Available);
}

/// <summary>
/// [S-11170] [S-12811] Every facts widget's rows, one component (owner, 2026-11-19: "the drive facts and similar
/// widgets should also show their entries like the items"; "they dont need to have an icon neccsarily, but they should
/// reuse the same base"): every single metric's one row ([S-14601], a list of one, its trend arrow beside the value,
/// <see cref="Beside"/>), the stopwatch's laps, the next meeting's lines. The family's fact rows ([S-6711]), one line
/// each, exactly as many as the pick, in its order; a list whose facts have glyphs leads every row with one (the slot
/// kept for a fact without, so the columns line up), a list without has no lead; a name or a value too long for the
/// width ends in its ellipsis; a fact that cannot be read now is drawn unavailable. [S-14810] The rows stand at their one
/// fixed size from the card's top edge (its margin), touching; the room under the last stays empty, and a row the card's
/// foot reaches is cut by it.
/// </summary>
public partial class FactList : RowStack
{
    private readonly List<RowBlock> _rows = [];
    private bool _led;

    // [S-12813] The status glyph over the card's top end corner stands beside the first row's value, never over it
    // (the stack's StatusRoom: the first row ends that much sooner).
    public FactList() => Margin = ListEdge.Margin;

    /// <summary>Draw <paramref name="facts"/>, one row each, reusing the rows already built.</summary>
    public void Show(IReadOnlyList<FactLine> facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        // Led or not is the list's: rows built for the other are built again.
        var led = facts.Any(f => f.Glyph.Length > 0);
        if (led != _led)
        {
            _led = led;
            foreach (var row in _rows)
            {
                Children.Remove(row);
            }

            _rows.Clear();
        }

        while (_rows.Count < facts.Count)
        {
            var row = new RowBlock(RowKind.Fact, led ? FamilyShape.Icon : 0);
            _rows.Add(row);
            Children.Add(row);
        }

        for (var i = 0; i < _rows.Count; i++)
        {
            var shown = i < facts.Count;
            _rows[i].Visibility = shown ? Visibility.Visible : Visibility.Collapsed;
            if (shown)
            {
                if (led)
                {
                    _rows[i].LeadGlyph(facts[i].Glyph);
                }

                _rows[i].Show(facts[i].Name, facts[i].Value);
                _rows[i].Available = facts[i].Available;
            }
        }

        ApplyRoom();
    }

    /// <summary>
    /// [S-14705] [S-14601] A single metric's one row is its whole face, one item from the card's edge to its edge: the row
    /// <paramref name="height"/> tall (the face's box, or the reading's share over its graph); NaN gives the rows back their
    /// one fixed size ([S-14810]).
    /// </summary>
    public void Face(double height)
    {
        foreach (var row in _rows)
        {
            row.Height = height;
        }
    }

    /// <summary>
    /// [S-14603] An element inline beside the value of the row at <paramref name="index"/> (a single metric's trend
    /// arrow; its line is the band under the row, [S-14220]), <paramref name="width"/> wide, 12 from the name and the
    /// value; null takes it away.
    /// </summary>
    public void Beside(int index, FrameworkElement? element, double width)
    {
        if (index >= 0 && index < _rows.Count)
        {
            _rows[index].Meter(element, width);
        }
    }
}
