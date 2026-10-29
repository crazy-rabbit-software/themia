namespace Themia.Sdk.Kit;

/// <summary>[S-6714] The three states a Status says.</summary>
public enum StatusKind
{
    /// <summary>Nothing to show right now: the widget's glyph, tertiary; an action when the widget has one (a standard button).</summary>
    Empty,

    /// <summary>Something went wrong: the caution warning; Try again or Open Accounts as a standard button.</summary>
    Problem,

    /// <summary>The widget waits to be set up: its glyph, tertiary; Set up as the one accent main action ([S-1862]).</summary>
    Setup,
}

/// <summary>[S-6714] How a Status of a kind is drawn: its glyph's role and whether its action is the accent main action.</summary>
public readonly record struct StatusLook(ColourRole Glyph, bool MainAction);

/// <summary>
/// [S-6714] The Status block (empty, problem, set up): one Row where the content's first row would
/// be, under the header, so a list with nothing in it looks like itself with one line and nothing
/// jumps when data arrives. [state glyph 16] 8 [one sentence, Body secondary, ellipsis, its whole
/// text in the tooltip] 12 [its one action]. Tile widgets show their state in the tile instead.
/// <para>
/// When the row is too narrow for both, the sentence goes and the glyph and the action stay (XS,
/// S): the sentence shows only with <see cref="MinSentence"/> left for it.
/// </para>
/// </summary>
public static class StatusRules
{
    /// <summary>
    /// The least room a sentence is drawn in beside an action: about twelve characters of Body, so the
    /// row of an S tile (168 wide, with Set up) is the glyph and the button (DECISIONS 5).
    /// </summary>
    public const double MinSentence = 96;

    /// <summary>A kind's look.</summary>
    public static StatusLook Look(StatusKind kind) => kind switch
    {
        StatusKind.Problem => new StatusLook(ColourRole.Caution, MainAction: false),
        StatusKind.Setup => new StatusLook(ColourRole.TextTertiary, MainAction: true),
        _ => new StatusLook(ColourRole.TextTertiary, MainAction: false),
    };

    /// <summary>
    /// Whether the sentence shows in a row <paramref name="width"/> wide whose action is
    /// <paramref name="actionWidth"/> wide (0 for none). An unmeasured row shows it, and so does a row
    /// with no action: the sentence is all it has to say.
    /// </summary>
    public static bool ShowsSentence(double width, double actionWidth)
    {
        if (!double.IsFinite(width) || width <= 0 || actionWidth <= 0)
        {
            return true;
        }

        return Room(width, actionWidth) >= MinSentence;
    }

    /// <summary>The room the sentence has: the row less the glyph and its gap, and the action and its gap.</summary>
    public static double Room(double width, double actionWidth)
        => width - (FamilyShape.Icon + FamilySpace.GlyphGap) - (actionWidth > 0 ? actionWidth + FamilySpace.ColumnGap : 0);
}
