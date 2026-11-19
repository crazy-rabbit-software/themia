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
/// reuse the same base"): the weather and air facts, the CPU, GPU, memory, network, ping, battery and drive facts, a
/// detail widget's facts, the stopwatch's laps, the next meeting's lines. The family's fact rows ([S-6711]), one line
/// each, exactly as many as the pick, in its order; a list whose facts have glyphs leads every row with one (the slot
/// kept for a fact without, so the columns line up), a list without has no lead; a name or a value too long for the
/// width ends in its ellipsis; a fact that cannot be read now is drawn unavailable. [S-12807] The rows fill the content from its top to the foot's inset (its margin), touching;
/// <see cref="SpreadStack.Rows"/> the pick's count, so fewer facts leave the rest empty.
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
}
