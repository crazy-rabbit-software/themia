using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;

namespace Themia.Core.Notes;

/// <summary>
/// [S-1363] [S-14770] The note widget's body: the text alone, from the card's top inset (the editor's own padding on all
/// four sides, [S-6703]; no row over it, WR-1), at every size, with the pill over its top end ([S-14771]) and nothing else.
/// A larger tile gives the text more room, never a note list beside it (switching notes is the pill's picker, [S-1364]).
/// </summary>
public static class NotesLayout
{
    /// <summary>
    /// [S-11206] [S-14770] The note's sizes: the text alone, a free range 160..600 × 80..600 (a new one 300 × 200). The least
    /// is the inset over and under a title's line and one line of text (12 + 28 + 20 + 12, in whole cells); with no row over
    /// the text the range is that row's 20 lower than it was. The text wraps and scrolls; nothing else appears with the size.
    /// </summary>
    public static WidgetSizing Sizing { get; } = WidgetSizing.Range(new(160, 80), new(300, 200), new(600, 600));

    /// <summary>The rename box under the pill: room for a title of a few words.</summary>
    public const double RenameWidth = 200;

    /// <summary>[S-14771] Where the pill stands from the card's top: on the text's first line, at the inset.</summary>
    public const double PillTop = FamilySpace.Inset;

    /// <summary>
    /// [S-14771] How tall the pill stands over a first line <paramref name="firstLine"/> tall (a body line's 20, a title's
    /// 28): that line's box, never less than a line of 20, so it hides the whole of the words under it (a title's lower
    /// half showed under a pill of a body line's height) and none of the next line's; its buttons stand centred on it, 2
    /// over and under a line of 20 as a tool line's do ([S-6710]).
    /// </summary>
    public static double PillHeight(double firstLine)
        => double.IsFinite(firstLine) ? Math.Max(FamilySpace.HeaderLine, firstLine) : FamilySpace.HeaderLine;

    /// <summary>[S-14771] Where the pill ends from the card's right edge: its last button's glyph on the inset ([S-6710]).</summary>
    public const double PillEnd = FamilySpace.Inset - LineRules.ButtonOverhang;

    /// <summary>
    /// [S-14771] The pill's room besides the note's name: its lead (4, between the text cut under it and the picker), the
    /// picker's 8 before the name and its chevron's 24, and the "…" button's 24.
    /// </summary>
    public const double PillParts = FamilySpace.S1 + FamilySpace.S2 + FamilyShape.LineButton + FamilyShape.LineButton;

    /// <summary>[S-14771] [S-1362] The problem glyph's room in the pill while the note's last write failed: its 16 and 4 either side.</summary>
    public const double ProblemPart = FamilyShape.Icon + (2 * FamilySpace.S1);

    /// <summary>[S-14771] The longest the note's name stands in the pill: a title of a few words; a longer one ends in an ellipsis.</summary>
    public const double NameMax = 160;

    /// <summary>
    /// [S-14771] Of the text's first line, the share the pill always leaves standing beside it: half, so at the least size
    /// (160) the pill is its chevron and "…" alone and the title reads (a name in it there covered all of "# Groceries" but
    /// "# Grc").
    /// </summary>
    public const double TextKept = 0.5;

    /// <summary>[S-14771] Under this the name says nothing ("…" alone): the picker is its chevron.</summary>
    public const double NameLeast = 32;

    /// <summary>[S-14771] How long the pill takes to come and go: Windows' fast control fade.</summary>
    public static TimeSpan Fade { get; } = TimeSpan.FromMilliseconds(167);

    /// <summary>
    /// [S-14771] How wide the note's name may stand in the pill on a card <paramref name="cardWidth"/> wide: at most
    /// <see cref="NameMax"/>, never so wide that the pill (the problem glyph's room too, while it stands:
    /// <paramref name="problem"/>) takes more than the share of the text's first line <see cref="TextKept"/> leaves it; none
    /// where that leaves less than <see cref="NameLeast"/> (the chevron alone picks).
    /// </summary>
    public static double NameWidth(double cardWidth, bool problem = false)
    {
        if (!double.IsFinite(cardWidth) || cardWidth <= 0)
        {
            return 0;
        }

        var line = cardWidth - (2 * FamilySpace.Inset);
        var room = Math.Min(NameMax, (line * (1 - TextKept)) - PillParts - (problem ? ProblemPart : 0));
        return room < NameLeast ? 0 : room;
    }

    /// <summary>
    /// [S-14773] Whole lines at the foot (owner's rule for the note at its least: no line cut mid-height at the foot). The
    /// room the text's lines have under the top inset is <paramref name="room"/> (the card's height less the inset over and
    /// under them); <paramref name="bottoms"/> are where its lines end, from the first line's top, in order. What the foot
    /// takes besides its inset: the part of the room under the last line that ends inside it, so the line it would cut is
    /// not drawn; none where every line ends inside the room, or where not even the first does (it is drawn as it can be).
    /// </summary>
    public static double CutLine(double room, IReadOnlyList<double> bottoms)
    {
        ArgumentNullException.ThrowIfNull(bottoms);
        if (!double.IsFinite(room) || room <= 0 || bottoms.Count == 0 || bottoms[^1] <= room + 0.5)
        {
            return 0;
        }

        var whole = 0.0;
        foreach (var bottom in bottoms)
        {
            if (bottom > room + 0.5)
            {
                break;
            }

            whole = bottom;
        }

        return whole > 0 ? Math.Max(0, room - whole) : 0;
    }
}

/// <summary>
/// [S-14771] When the note's pill (its picker and "…" over the text's top end) shows: while the pointer is over the card,
/// until a key is typed (the text under it is then what the user needs; the pointer moving again brings it back); while
/// one of its menus, its rename box or its question is open (their anchor stays); and while the note's last write failed
/// (the problem glyph in it says so, [S-1362]). At rest it is not there: the text is wholly seen. It fades in and out
/// (opacity alone: nothing changes size, WR-12) and takes no pointer while it is not shown.
/// </summary>
/// <param name="Pointer">The pointer is over the card.</param>
/// <param name="Typing">A key was typed since the pointer last moved.</param>
/// <param name="Menu">A menu, the rename box or the delete question of the pill is open.</param>
/// <param name="Problem">The note's last write failed.</param>
public readonly record struct NotesPill(bool Pointer = false, bool Typing = false, bool Menu = false, bool Problem = false)
{
    /// <summary>The pill is shown (and takes the pointer).</summary>
    public bool Shown => Menu || Problem || (Pointer && !Typing);

    /// <summary>The pointer came onto the card or moved over it.</summary>
    public NotesPill Moved() => this with { Pointer = true, Typing = false };

    /// <summary>The pointer left the card.</summary>
    public NotesPill Left() => this with { Pointer = false };

    /// <summary>A key was typed in the text.</summary>
    public NotesPill Typed() => this with { Typing = true };

    /// <summary>One of the pill's menus (or its rename box, its question) opened or closed.</summary>
    public NotesPill WithMenu(bool open) => this with { Menu = open };

    /// <summary>The note's last write failed, or a write succeeded again.</summary>
    public NotesPill WithProblem(bool problem) => this with { Problem = problem };
}
