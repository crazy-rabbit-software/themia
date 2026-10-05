namespace Themia.Core.Settings;

/// <summary>
/// One searchable thing in the settings window: a whole page, or one row on a page. [S-1542]
/// <para>
/// Built by the window from the rows the pages actually draw — the registry that every settings row
/// goes through — rather than from a list kept beside the pages, so a row added to a page is
/// searchable the moment it exists and a row taken off one cannot linger as a dead result.
/// </para>
/// </summary>
/// <param name="PageKey">Opaque key of the page the row lives on; the window maps it back to a page.</param>
/// <param name="PagePath">Where the row is, as the result shows it: "Appearance", "Desktop › Clock".</param>
/// <param name="Section">The section heading the row sits under, when it has one.</param>
/// <param name="Title">The row's title, or the page's for a page entry.</param>
/// <param name="Description">The row's description line, when it has one.</param>
/// <param name="Keywords">
/// Words that find the row without being on it: the options of its drop-down ("Dark" finds Theme),
/// and whatever the page adds.
/// </param>
/// <param name="IsPage">A page rather than a row: navigates to the top of the page.</param>
public sealed record SettingEntry(
    string PageKey,
    string PagePath,
    string? Section,
    string Title,
    string? Description,
    IReadOnlyList<string> Keywords,
    bool IsPage = false);

/// <summary>A search hit and how well it matched; higher is better.</summary>
public readonly record struct SettingMatch(SettingEntry Entry, int Score);

/// <summary>
/// Matching and ranking for the settings window's "Find a setting" box. [S-1542..S-1545]
/// <para>
/// Pure, so the whole behaviour is pinned by tests. The shape follows the original app's widget
/// search: both sides are normalised, every word of the query must land somewhere (AND), each word
/// is scored by <i>where</i> it landed (title over keyword over section over description) and by
/// <i>how</i> (whole field, whole word, prefix, word prefix, substring, small typo), and the whole
/// query being the start of a title is worth extra, so typing "acc" puts Accent above a row that
/// merely mentions an account.
/// </para>
/// </summary>
public static class SettingsSearch
{
    /// <summary>How many suggestions the box lists, as Windows Settings does.</summary>
    public const int DefaultLimit = 8;

    // Match qualities, best to worst. Wide gaps, so a weighted lower quality never outranks a
    // better one in the same field.
    private const int Exact = 100;
    private const int Word = 90;
    private const int Prefix = 70;
    private const int WordPrefix = 60;
    private const int Substring = 35;
    private const int Typo = 22;

    /// <summary>The whole query starts the title: "acc" means Accent, not "…account…".</summary>
    private const int TitlePrefixBonus = 40;

    /// <summary>The whole query is the title.</summary>
    private const int TitleExactBonus = 60;

    /// <summary>
    /// The page itself, when its name is what was typed, comes before its rows: "appearance" should
    /// offer the Appearance page first, as Windows does.
    /// </summary>
    private const int PageBonus = 10;

    /// <summary>
    /// British spellings folded to the American ones on both sides: the pages say "colour", and a
    /// user typing "color" (or the reverse) must still land. Word-level, so "colourful" is untouched.
    /// </summary>
    private static readonly Dictionary<string, string> Spellings = new(StringComparer.Ordinal)
    {
        ["colour"] = "color",
        ["colours"] = "colors",
        ["behaviour"] = "behavior",
        ["behaviours"] = "behaviors",
        ["grey"] = "gray",
        ["centre"] = "center",
        ["favourite"] = "favorite",
        ["favourites"] = "favorites",
        ["organise"] = "organize",
        ["licence"] = "license",
        ["customise"] = "customize",
        ["minimise"] = "minimize",
        ["maximise"] = "maximize",
    };

    /// <summary>
    /// The best <paramref name="limit"/> entries for <paramref name="query"/>, best first. An empty
    /// or blank query matches nothing — the box shows no suggestions until something is typed.
    /// </summary>
    public static IReadOnlyList<SettingMatch> Find(IEnumerable<SettingEntry> entries, string? query, int limit = DefaultLimit)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var tokens = Tokenize(query ?? string.Empty);
        if (tokens.Count == 0 || limit <= 0)
        {
            return [];
        }

        var whole = string.Join(' ', tokens);
        var matches = new List<SettingMatch>();
        var seen = new HashSet<(string, string, string, string?)>();

        foreach (var entry in entries)
        {
            // The same row reached twice (a page built twice, a row registered under two names) is
            // one suggestion, not two identical lines. The same title in two groups of a list page
            // ("Views › Desktop", "Views › Work") is two rows, told apart by the path ([S-1832]).
            if (!seen.Add((entry.PageKey, entry.PagePath, entry.Title, entry.Section)))
            {
                continue;
            }

            if (Score(entry, tokens, whole) is { } score)
            {
                matches.Add(new SettingMatch(entry, score));
            }
        }

        matches.Sort(static (a, b) =>
        {
            var byScore = b.Score.CompareTo(a.Score);
            if (byScore != 0)
            {
                return byScore;
            }

            // Shorter titles first: of two equally good hits the more specific name is the one meant.
            var byLength = a.Entry.Title.Length.CompareTo(b.Entry.Title.Length);
            return byLength != 0 ? byLength : string.CompareOrdinal(a.Entry.Title, b.Entry.Title);
        });

        return matches.Count > limit ? matches.GetRange(0, limit) : matches;
    }

    /// <summary>
    /// Lower-case words, split on anything that is not a letter or digit, with British spellings
    /// folded. Public for the tests and for the window, which highlights nothing but uses the same
    /// notion of "empty query".
    /// </summary>
    public static IReadOnlyList<string> Tokenize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var words = new List<string>();
        var start = -1;

        for (var i = 0; i <= text.Length; i++)
        {
            var isWordChar = i < text.Length && char.IsLetterOrDigit(text[i]);

            if (isWordChar && start < 0)
            {
                start = i;
            }
            else if (!isWordChar && start >= 0)
            {
                var word = text[start..i].ToLowerInvariant();
                words.Add(Spellings.TryGetValue(word, out var folded) ? folded : word);
                start = -1;
            }
        }

        return words;
    }

    private static int? Score(SettingEntry entry, IReadOnlyList<string> tokens, string whole)
    {
        var title = Field.Of(entry.Title, 100, typos: true);
        var keywords = Field.Of(string.Join(' ', entry.Keywords), 80, typos: true);
        var section = Field.Of(entry.Section, 55, typos: false);
        var page = Field.Of(entry.PagePath, 55, typos: false);
        var description = Field.Of(entry.Description, 45, typos: false);

        var total = 0;

        foreach (var token in tokens)
        {
            var best = 0;

            foreach (var field in (ReadOnlySpan<Field>)[title, keywords, section, page, description])
            {
                best = Math.Max(best, field.Match(token) * field.Weight / 100);
            }

            if (best == 0)
            {
                return null; // every word has to land somewhere
            }

            total += best;
        }

        if (title.Text == whole)
        {
            total += TitleExactBonus;
        }
        else if (title.Text.StartsWith(whole, StringComparison.Ordinal))
        {
            total += TitlePrefixBonus;
        }

        if (entry.IsPage)
        {
            total += PageBonus;
        }

        return total;
    }

    /// <summary>One field of an entry, normalised once: the joined words and the words themselves.</summary>
    private readonly record struct Field(string Text, string[] Words, int Weight, bool Typos)
    {
        public static Field Of(string? value, int weight, bool typos)
        {
            var words = Tokenize(value ?? string.Empty);
            return new Field(string.Join(' ', words), [.. words], weight, typos);
        }

        public int Match(string token)
        {
            if (Text.Length == 0)
            {
                return 0;
            }

            if (Text == token)
            {
                return Exact;
            }

            if (Array.IndexOf(Words, token) >= 0)
            {
                return Word;
            }

            if (Text.StartsWith(token, StringComparison.Ordinal))
            {
                return Prefix;
            }

            foreach (var word in Words)
            {
                if (word.StartsWith(token, StringComparison.Ordinal))
                {
                    return WordPrefix;
                }
            }

            if (Text.Contains(token, StringComparison.Ordinal))
            {
                return Substring;
            }

            if (!Typos)
            {
                return 0;
            }

            var budget = TypoBudget(token);
            if (budget == 0)
            {
                return 0;
            }

            foreach (var word in Words)
            {
                if (TypoMatch(token, word, budget))
                {
                    return Typo;
                }
            }

            return 0;
        }
    }

    /// <summary>
    /// How many edits a word may be off by. None for three letters or fewer: at that length one edit
    /// reaches half the settings, which is noise rather than tolerance.
    /// </summary>
    public static int TypoBudget(string token) => token.Length switch
    {
        <= 3 => 0,
        <= 6 => 1,
        _ => 2,
    };

    private static bool TypoMatch(string token, string word, int budget)
    {
        if (word.Length < 3)
        {
            return false;
        }

        // A short word whose first letter is already wrong is a different word: "font" must not
        // find "front".
        if (token.Length <= 5 && token[0] != word[0])
        {
            return false;
        }

        if (EditDistance(token, word, budget) <= budget)
        {
            return true;
        }

        // A mistyped prefix still lands: "apearan" reaches "appearance" through "appeara".
        return word.Length > token.Length && EditDistance(token, word[..token.Length], budget) <= budget;
    }

    /// <summary>
    /// Damerau-Levenshtein (optimal string alignment) distance, bounded: returns
    /// <paramref name="max"/> + 1 as soon as no alignment can stay inside the budget.
    /// </summary>
    public static int EditDistance(string a, string b, int max)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        if (a == b)
        {
            return 0;
        }

        if (Math.Abs(a.Length - b.Length) > max)
        {
            return max + 1;
        }

        if (a.Length == 0 || b.Length == 0)
        {
            return Math.Max(a.Length, b.Length);
        }

        var before = new int[b.Length + 1];
        var previous = new int[b.Length + 1];
        var current = new int[b.Length + 1];

        for (var j = 0; j <= b.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            var rowMin = i;

            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                var value = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);

                // A transposition ("teh" for "the") is one edit, not two.
                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                {
                    value = Math.Min(value, before[j - 2] + 1);
                }

                current[j] = value;
                rowMin = Math.Min(rowMin, value);
            }

            if (rowMin > max)
            {
                return max + 1;
            }

            (before, previous, current) = (previous, current, before);
        }

        return Math.Min(previous[b.Length], max + 1);
    }
}
