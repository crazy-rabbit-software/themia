using Themia.Sdk.Kit;

namespace Themia.Widget.NextMeeting;

/// <summary>[S-2591] Where Join stands: none without a link, beside the meeting's lines, or under them as the Controls row.</summary>
public enum JoinPlace
{
    /// <summary>No join link: no button, the lines take the full width.</summary>
    None,

    /// <summary>At the end side of the meeting's lines, vertically centred on them.</summary>
    Beside,

    /// <summary>The Controls row at the content's foot, the card too narrow for the title beside it.</summary>
    Under,
}

/// <summary>
/// [S-2591] [S-2592] Join beside the meeting, not under it (owner decision 2026-11-12): the meeting's
/// lines are a column at the start and Join stands at the end, the family's column gap between them,
/// as long as the column keeps a title a sensible width; narrower, Join goes back under the lines.
/// Decided on the measured widths alone, so a language with a longer "Join" moves it sooner.
/// </summary>
public static class NextMeetingJoin
{
    /// <summary>
    /// The narrowest text column the title keeps beside Join: about a dozen Title characters, under
    /// which a meeting's name is cut to a word or two and reads better on its own line.
    /// </summary>
    public const double MinTextColumn = 160;

    /// <summary>Between the lines' column and Join.</summary>
    public const double Gap = FamilySpace.ColumnGap;

    /// <summary>
    /// Where Join stands in a content box <paramref name="contentWidth"/> wide (the card less its side
    /// insets) for a button <paramref name="joinWidth"/> wide.
    /// </summary>
    public static JoinPlace Place(bool hasJoin, double contentWidth, double joinWidth)
    {
        if (!hasJoin)
        {
            return JoinPlace.None;
        }

        return double.IsFinite(contentWidth) && TextWidth(JoinPlace.Beside, contentWidth, joinWidth) >= MinTextColumn
            ? JoinPlace.Beside
            : JoinPlace.Under;
    }

    /// <summary>The width the meeting's lines have: the content's, less Join and the gap where Join stands beside them.</summary>
    public static double TextWidth(JoinPlace place, double contentWidth, double joinWidth)
        => place == JoinPlace.Beside ? Math.Max(0, contentWidth - joinWidth - Gap) : Math.Max(0, contentWidth);
}
