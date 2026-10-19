namespace Themia.Core.Settings;

/// <summary>
/// [S-3725] How a search's answers fill their room on the properties menu: the first
/// <see cref="Answers"/> answers, the Show all line under them when there are more, and the lines
/// left free. The room is always <see cref="Lines"/> lines (five answers and the Show all line's
/// slot), whatever was answered — nothing, a word in place of answers ("Searching…", no match), a
/// few answers or many — so the submenu never changes height while it is typed into; only Show all
/// makes it grow, by the answers past the fifth. Pure: the menu draws what this says.
/// </summary>
public static class SearchRoom
{
    /// <summary>The answers shown before the Show all line.</summary>
    public const int Answers = 5;

    /// <summary>The lines the answers' room always takes: the answers and the Show all line's slot.</summary>
    public const int Lines = Answers + 1;

    /// <summary>
    /// The room for <paramref name="answers"/> answers (a word in their place counts as one line),
    /// every one shown once <paramref name="showAll"/> was clicked.
    /// </summary>
    public static SearchFill Fill(int answers, bool showAll)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(answers);

        var cut = !showAll && answers > Answers;
        var shown = cut ? Answers : answers;
        var used = shown + (cut ? 1 : 0);
        return new SearchFill(shown, cut, Math.Max(0, Lines - used));
    }
}

/// <summary>
/// [S-3725] A search's answers as drawn: how many show, whether the Show all line is under them, and
/// the lines kept free below so the room keeps its height.
/// </summary>
public readonly record struct SearchFill(int Shown, bool ShowAll, int Free);
