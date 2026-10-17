using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-940] Where in a widget's description a query word landed. The weights in
/// <see cref="WidgetSearch"/> say how much a hit there means the user meant this widget.
/// </summary>
public enum SearchField
{
    Label,
    Keyword,
    Type,
    Category,
    Provider,
    Description,
}

/// <summary>
/// One widget as the picker's search sees it. Labels come first (the shown one, then any other
/// spelling, such as an English original next to a translation), so the localisation round only has
/// to add strings here, not change the matcher.
/// </summary>
/// <param name="Type">The kind's name, e.g. <c>GitHubNotifications</c>; split on camel case.</param>
/// <param name="Labels">Display names. The first one is the label the whole-query prefix bonus is judged on.</param>
public sealed record WidgetSearchDoc(
    string Type,
    IReadOnlyList<string> Labels,
    IReadOnlyList<string>? Keywords = null,
    IReadOnlyList<string>? Descriptions = null,
    IReadOnlyList<string>? Categories = null,
    IReadOnlyList<string>? Providers = null);

/// <summary>
/// The normalised form of a <see cref="WidgetSearchDoc"/>. Building it is the expensive half of a
/// query, so the picker builds one per widget, once, and reuses it for every keystroke.
/// </summary>
public sealed class IndexedWidget
{
    internal IndexedWidget(string type, string labelText, IReadOnlyList<IndexedField> fields)
    {
        Type = type;
        LabelText = labelText;
        Fields = fields;
    }

    public string Type { get; }

    /// <summary>The normalised primary label, for the whole-query prefix bonus and the tie-break.</summary>
    public string LabelText { get; }

    internal IReadOnlyList<IndexedField> Fields { get; }
}

/// <summary>One normalised field value and how much a hit in it is worth.</summary>
internal sealed record IndexedField(double Weight, string Text, string[] Words, bool Typo);

/// <summary>
/// [S-940..S-947] Matching and ranking for the widget picker's search box, ported from the
/// original's <c>widgetSearch.ts</c> (WidgetPicker.SPEC S-01..S-11).
/// <para>
/// A plain "label contains the query" missed every typo ("calender") and every word a user reaches
/// for that the label does not use ("timer", "wifi", "temperature"). This normalises both sides,
/// matches each query word against weighted fields including a curated synonym list
/// (each widget's <c>WidgetDescriptor.Keywords</c>), tolerates typos within a bounded edit distance, and returns a
/// score so the list can be ordered best match first rather than alphabetically.
/// </para>
/// </summary>
public static partial class WidgetSearch
{
    // Match qualities, best to worst. The gaps are wide enough that a lower quality in a heavier
    // field does not outrank a higher one in the same class of field.
    private const double Exact = 100;      // the whole field is the token
    private const double Word = 90;        // a whole word of the field is the token
    private const double Prefix = 70;      // the field starts with the token ("we" -> "weather")
    private const double WordPrefix = 60;  // a word in the field starts with the token
    private const double Substring = 35;   // the token appears anywhere ("core" -> "per-core load")
    private const double Typo = 22;        // within the edit budget ("calender")

    /// <summary>
    /// [S-947] Extra points when the whole query is a prefix of the label, so typing "cl" puts Clock
    /// above widgets that merely mention a clock in their description.
    /// </summary>
    public const double LabelPrefixBonus = 30;

    /// <summary>[S-940] How much a hit in each field means the user meant this widget.</summary>
    public static double Weight(SearchField field) => field switch
    {
        SearchField.Label => 1,
        SearchField.Keyword => 0.9,
        SearchField.Type => 0.8,
        SearchField.Category => 0.55,
        SearchField.Provider => 0.55,
        _ => 0.45,
    };

    /// <summary>
    /// [S-941] Lower case, diacritics stripped and ß folded, so "Wetter" and "wetter", "Größe" and
    /// "grosse" compare equal.
    /// </summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var lower = text.ToLowerInvariant();
        var builder = new StringBuilder(lower.Length);

        foreach (var c in lower)
        {
            // Already-decomposed input carries its accents as separate marks: drop them.
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark)
            {
                continue;
            }

            if (c is >= 'À' and <= 'ſ' && Folds.TryGetValue(c, out var plain))
            {
                builder.Append(plain);
                continue;
            }

            builder.Append(c);
        }

        return builder.ToString().Trim();
    }

    /// <summary>
    /// Accented Latin letters and their plain spelling. A table rather than
    /// <see cref="string.Normalize(NormalizationForm)"/>: the app runs with invariant globalization,
    /// where decomposing a precomposed "ü" is a no-op, so the accent would never come off. Latin-1 and
    /// Latin Extended-A cover the nine languages the app is being translated into.
    /// </summary>
    private static readonly Dictionary<char, string> Folds = BuildFolds();

    private static Dictionary<char, string> BuildFolds()
    {
        (string Accented, string Plain)[] groups =
        [
            ("àáâãäåāăą", "a"), ("çćĉċč", "c"), ("ďđð", "d"), ("èéêëēĕėęě", "e"), ("ĝğġģ", "g"),
            ("ĥħ", "h"), ("ìíîïĩīĭįı", "i"), ("ĵ", "j"), ("ķĸ", "k"), ("ĺļľŀł", "l"), ("ñńņňŉŋ", "n"),
            ("òóôõöøōŏő", "o"), ("ŕŗř", "r"), ("śŝşšſ", "s"), ("ţťŧ", "t"), ("ùúûüũūŭůűų", "u"),
            ("ŵ", "w"), ("ýÿŷ", "y"), ("źżž", "z"), ("ß", "ss"), ("æ", "ae"), ("œ", "oe"), ("þ", "th"),
            ("ĳ", "ij"),
        ];

        var folds = new Dictionary<char, string>();
        foreach (var (accented, plain) in groups)
        {
            foreach (var c in accented)
            {
                folds[c] = plain;
            }
        }

        return folds;
    }

    /// <summary>
    /// [S-941] Split into comparable words. A camel-case kind name ("GitHubNotifications") is split
    /// first so its parts are searchable on their own; anything that is not a letter or a digit
    /// separates words. Unsegmented scripts (Chinese, Japanese) stay one token, where substring
    /// matching still applies.
    /// </summary>
    public static string[] Tokenize(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        var normalized = Normalize(CamelBoundary().Replace(text, "$1 $2"));
        var words = new List<string>();
        var word = new StringBuilder();

        foreach (var c in normalized)
        {
            if (IsWordChar(c))
            {
                word.Append(c);
            }
            else if (word.Length > 0)
            {
                words.Add(word.ToString());
                word.Clear();
            }
        }

        if (word.Length > 0)
        {
            words.Add(word.ToString());
        }

        return [.. words];
    }

    /// <summary>
    /// [S-944] The typo budget for a query word. Three characters or fewer get none: at that length
    /// one edit reaches half the catalogue, which is noise rather than tolerance.
    /// </summary>
    public static int MaxTypos(string token)
    {
        ArgumentNullException.ThrowIfNull(token);
        return token.Length <= 3 ? 0 : token.Length <= 6 ? 1 : 2;
    }

    /// <summary>
    /// [S-944] Damerau-Levenshtein distance (optimal string alignment), bounded by
    /// <paramref name="max"/>: returns <c>max + 1</c> as soon as no alignment can stay inside the
    /// budget, so a long description word never costs a full table. A transposition ("teh") is one
    /// edit, not two.
    /// </summary>
    public static int EditDistance(string a, string b, int max)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        if (string.Equals(a, b, StringComparison.Ordinal))
        {
            return 0;
        }

        if (Math.Abs(a.Length - b.Length) > max)
        {
            return max + 1;
        }

        if (a.Length == 0)
        {
            return b.Length;
        }

        if (b.Length == 0)
        {
            return a.Length;
        }

        var prev2 = new int[b.Length + 1];
        var prev = new int[b.Length + 1];
        var cur = new int[b.Length + 1];

        for (var j = 0; j <= b.Length; j++)
        {
            prev[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            cur[0] = i;
            var rowMin = cur[0];

            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                var v = Math.Min(Math.Min(cur[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);

                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                {
                    v = Math.Min(v, prev2[j - 2] + 1);
                }

                cur[j] = v;
                rowMin = Math.Min(rowMin, v);
            }

            if (rowMin > max)
            {
                return max + 1;
            }

            (prev2, prev, cur) = (prev, cur, prev2);
        }

        return prev[b.Length];
    }

    /// <summary>Build the reusable index of one widget.</summary>
    public static IndexedWidget Index(WidgetSearchDoc doc)
    {
        ArgumentNullException.ThrowIfNull(doc);

        var fields = new List<IndexedField>();

        foreach (var label in doc.Labels)
        {
            Add(fields, SearchField.Label, label);
        }

        foreach (var keyword in doc.Keywords ?? [])
        {
            Add(fields, SearchField.Keyword, keyword);
        }

        Add(fields, SearchField.Type, CamelBoundary().Replace(doc.Type, "$1 $2"));

        foreach (var category in doc.Categories ?? [])
        {
            Add(fields, SearchField.Category, category);
        }

        foreach (var provider in doc.Providers ?? [])
        {
            Add(fields, SearchField.Provider, provider);
        }

        foreach (var description in doc.Descriptions ?? [])
        {
            Add(fields, SearchField.Description, description);
        }

        return new IndexedWidget(doc.Type, Normalize(doc.Labels.Count > 0 ? doc.Labels[0] : string.Empty), fields);
    }

    /// <summary>
    /// [S-942] Score one widget against a raw query. 0 means "not a match": <b>every</b> query word
    /// has to land somewhere, so "weather forecast" is not satisfied by a widget that only knows
    /// "weather". An empty query matches everything.
    /// </summary>
    public static double Score(IndexedWidget widget, string? query)
    {
        ArgumentNullException.ThrowIfNull(widget);

        var tokens = Tokenize(query);
        if (tokens.Length == 0)
        {
            return 1;
        }

        double total = 0;

        foreach (var token in tokens)
        {
            double best = 0;

            foreach (var field in widget.Fields)
            {
                var quality = Match(token, field);
                if (quality > 0)
                {
                    best = Math.Max(best, quality * field.Weight);
                }
            }

            if (best <= 0)
            {
                return 0;
            }

            total += best;
        }

        var whole = Normalize(query);
        if (whole.Length > 0 && widget.LabelText.StartsWith(whole, StringComparison.Ordinal))
        {
            total += LabelPrefixBonus;
        }

        return total;
    }

    /// <summary>
    /// [S-946] Filter and rank in one pass: non-matches drop out, the rest come back best first, ties
    /// broken by label so the order is stable from one keystroke to the next.
    /// </summary>
    public static List<T> Rank<T>(IEnumerable<(IndexedWidget Index, T Value)> items, string? query)
    {
        ArgumentNullException.ThrowIfNull(items);

        return items
            .Select(item => (Score: Score(item.Index, query), item.Index.LabelText, item.Value))
            .Where(scored => scored.Score > 0)
            .OrderByDescending(scored => scored.Score)
            .ThenBy(scored => scored.LabelText, StringComparer.Ordinal)
            .Select(scored => scored.Value)
            .ToList();
    }

    private static void Add(List<IndexedField> fields, SearchField kind, string? value)
    {
        var text = Normalize(value);
        if (text.Length == 0)
        {
            return;
        }

        // [S-945] Descriptions are prose: one edit away from any of their common words is mostly
        // noise, so they match literally only.
        fields.Add(new IndexedField(Weight(kind), text, Tokenize(value), kind != SearchField.Description));
    }

    /// <summary>[S-947] The best quality with which a token matches one field. 0 is no match.</summary>
    private static double Match(string token, IndexedField field)
    {
        var text = field.Text;

        if (string.Equals(text, token, StringComparison.Ordinal))
        {
            return Exact;
        }

        if (field.Words.Contains(token, StringComparer.Ordinal))
        {
            return Word;
        }

        if (text.StartsWith(token, StringComparison.Ordinal))
        {
            return Prefix;
        }

        if (field.Words.Any(w => w.StartsWith(token, StringComparison.Ordinal)))
        {
            return WordPrefix;
        }

        if (text.Contains(token, StringComparison.Ordinal))
        {
            return Substring;
        }

        if (!field.Typo)
        {
            return 0;
        }

        var budget = MaxTypos(token);
        return budget > 0 && field.Words.Any(w => TypoMatch(token, w, budget)) ? Typo : 0;
    }

    /// <summary>
    /// [S-944] Whether a token reaches a word within its budget. Also compared against the word's
    /// leading slice of the same length, so a mistyped <i>prefix</i> lands too ("kalend" reaches
    /// "calendar"). A short token whose first letter is already wrong is a different word, not a
    /// typo: "mail" must not reach "daily".
    /// </summary>
    private static bool TypoMatch(string token, string word, int budget)
    {
        if (word.Length < 3)
        {
            return false;
        }

        if (token.Length <= 5 && token[0] != word[0])
        {
            return false;
        }

        if (EditDistance(token, word, budget) <= budget)
        {
            return true;
        }

        return word.Length > token.Length && EditDistance(token, word[..token.Length], budget) <= budget;
    }

    private static bool IsWordChar(char c) => CharUnicodeInfo.GetUnicodeCategory(c) switch
    {
        UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter or UnicodeCategory.TitlecaseLetter
            or UnicodeCategory.ModifierLetter or UnicodeCategory.OtherLetter
            or UnicodeCategory.DecimalDigitNumber or UnicodeCategory.LetterNumber or UnicodeCategory.OtherNumber => true,
        _ => false,
    };

    [GeneratedRegex("([a-z0-9])([A-Z])", RegexOptions.CultureInvariant)]
    private static partial Regex CamelBoundary();
}
