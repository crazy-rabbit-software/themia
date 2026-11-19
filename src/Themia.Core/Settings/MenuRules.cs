using System.Text.RegularExpressions;
using Themia.App.Shell;
using Themia.Core.Localization;

namespace Themia.Core.Settings;

/// <summary>[S-2374] What a row of a settings page is, as the rules read it: a page's line or a widget's entry alike.</summary>
public enum RuleKind
{
    /// <summary>On or off.</summary>
    Toggle,

    /// <summary>A choice of one: its options under it.</summary>
    Choice,

    /// <summary>One option of a choice.</summary>
    Option,

    /// <summary>A number in a number box.</summary>
    Number,

    /// <summary>A line of text, a search, a colour, a font or an icon: a value of its own.</summary>
    Value,

    /// <summary>A row that does something once.</summary>
    Action,

    /// <summary>A way to where a setting is made.</summary>
    Link,

    /// <summary>Words only.</summary>
    Note,

    /// <summary>A heading over the rows after it.</summary>
    Heading,

    /// <summary>A rule between groups.</summary>
    Separator,

    /// <summary>A level of rows: an expander, a card, a sub-page.</summary>
    Level,
}

/// <summary>
/// [S-2374] One row as the menu rules read it (Karpo's <c>FlyoutItem</c> in its MenuRulesTests): its words, what it says at
/// its end, its icon, whether it is greyed and why, and the rows it opens.
/// </summary>
public sealed record RuleRow(string Text, RuleKind Kind)
{
    public string? Value { get; init; }

    public string? Glyph { get; init; }

    public bool Danger { get; init; }

    public bool Disabled { get; init; }

    /// <summary>[S-2375] Why it is greyed; null for nothing said.</summary>
    public string? Why { get; init; }

    /// <summary>[S-2310] A card that opens in place (a thing's, or one of General's): it says what it holds at its end.</summary>
    public bool Card { get; init; }

    /// <summary>[S-2310] One of the user's own things (a view, a display, an account): a card whose words are the user's.</summary>
    public bool Thing { get; init; }

    public IReadOnlyList<RuleRow> Below { get; init; } = [];
}

/// <summary>A rule broken: which, where (the rows' words from the page down), what.</summary>
public sealed record RuleOffence(string Rule, string Path, string What)
{
    /// <summary>What an allowance names it by: the rule and the path.</summary>
    public string Key => $"{Rule} {Path}";
}

/// <summary>
/// [S-2374] Karpo's menu rules (karpo-app <c>MenuRulesTests</c>, docs/widgets.md §302, §309, §411, §518), held over every
/// page of the edit bar and every widget's page so Themia's settings keep them (owner, 2026-11-19: "have you even compared
/// the seetings in themia with those of karpo? ... make sure that you put the features of the karpo settings into themia
/// too"). Read off the rows as the user sees them, in English. Pure.
/// <list type="bullet">
/// <item>R3: no choice of two with Off in it (that is a switch).</item>
/// <item>R4: a choice, a number and a value say what holds at their end; a thing's card where it holds settings.</item>
/// <item>R7: one word per concept (<see cref="Glossary"/>); a level's title a noun, not a sentence.</item>
/// <item>R8: "and", never "&amp;"; "Add a …"; sentence case (names keep their capitals, <see cref="Proper"/>).</item>
/// <item>R10: a heading stands over rows of its own: never last, never over a rule or another heading.</item>
/// <item>R11: a reset is called Reset all and stands last on its level.</item>
/// <item>R12: nothing destructive mid-level.</item>
/// <item>R13: no icon twice on one level (two rows of different words).</item>
/// <item>R14: a value at most 24 characters (a default's inside its "Default (…)"), a title never cut (37 with its control).</item>
/// <item>R18 (§518): what is left to follow says "Default (…)" with what it comes to: never "Windows setting (…)",
/// "Follow Windows", "… (default)" or a bare "Default".</item>
/// <item>R20 (§309): a greyed row says why under its name.</item>
/// <item>R21 (§302): every number says its unit ("12 rows", "30 min"), never bare digits.</item>
/// </list>
/// </summary>
public static class MenuRules
{
    /// <summary>R14: the most a value says, in characters.</summary>
    public const int LongestValue = 24;

    /// <summary>R14: the most a row's words say before its line cuts them, its control off them.</summary>
    public const int LongestTitle = 37;

    /// <summary>R7: the one word for a concept, and the words that may not stand for it.</summary>
    public static readonly IReadOnlyList<(string Word, string[] Not)> Glossary =
    [
        ("Place", ["Location"]),
        ("Update interval", ["How often it updates", "Refresh interval", "Refresh every"]),
        ("Reset all", ["Reset to defaults", "Reset colors", "Reset colours", "Reset everything"]),
        ("Duration", ["How long"]),
        ("Animations", ["Transitions"]),
    ];

    /// <summary>R8: words that keep their capital mid-title: names, and what Windows writes so.</summary>
    public static readonly IReadOnlySet<string> Proper = new HashSet<string>(StringComparer.Ordinal)
    {
        "Windows", "Themia", "Pro", "Microsoft", "Store", "To", "Do", "IMAP", "SMTP", "CalDAV",
        "CardDAV", "JSON", "URL", "RSS", "PC", "CPU", "GPU", "RAM", "VPN", "Wi-Fi", "Bluetooth", "UTC",
        "GMT", "Lometics", "GmbH", "Explorer", "File", "Liquid", "Celsius", "Fahrenheit", "°C", "°F",
        "AQI", "US", "EU", "UV", "Open-Meteo", "ID", "OK", "PIN", "Ctrl", "Alt", "Shift", "F2", "API", "HTTP", "HTTPS",
    };

    private static readonly Regex Sentence = new(@"^(How|What|Which|When|Where|Who|Why)\b|\b(it|its|it's|you)\b|\?$", RegexOptions.Compiled);

    private static readonly Regex AddA = new(@"^Add (a|an) \S+( \S+)?$", RegexOptions.Compiled);

    private static readonly Regex Digits = new(@"^[\d\s.,  −-]+$", RegexOptions.Compiled);

    /// <summary>R18: the old ways of saying a value left to follow.</summary>
    private static readonly string[] OldDefaults = ["Windows setting (", "Follow Windows", "Windows default", "As in Windows", "Inherited"];

    /// <summary>Every rule broken on <paramref name="rows"/> (a page's sections, a widget's page), each with where.</summary>
    public static IReadOnlyList<RuleOffence> Check(IReadOnlyList<RuleRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var found = new List<RuleOffence>();
        Walk(rows, [], found);
        return found;
    }

    /// <summary>R18: what a value says inside its "Default (…)", else whole.</summary>
    public static string Measured(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var form = Str.Common_DefaultOf.Format(("value", "\u0001")).Split('\u0001');
        return form.Length == 2 && value.Length > form[0].Length + form[1].Length && value.StartsWith(form[0], StringComparison.Ordinal) && value.EndsWith(form[1], StringComparison.Ordinal)
            ? value[form[0].Length..^form[1].Length]
            : value;
    }

    private static string Path(IReadOnlyList<string> path, RuleRow? row = null) => string.Join(" › ", row is null ? path : [.. path, row.Text]);

    private static void Walk(IReadOnlyList<RuleRow> level, IReadOnlyList<string> path, List<RuleOffence> found)
    {
        void Add(string rule, string where, string what) => found.Add(new RuleOffence(rule, where, what));
        var rows = level.Where(r => r.Kind != RuleKind.Separator).ToList();

        // R13: no icon twice on a level, two rows of different words (the options of one choice and the user's own things wear theirs).
        foreach (var twice in rows.Where(r => r.Glyph is { Length: > 0 } && r.Kind is not (RuleKind.Option or RuleKind.Heading or RuleKind.Note) && !r.Thing)
            .GroupBy(r => r.Glyph).Where(g => g.Select(r => r.Text).Distinct(StringComparer.Ordinal).Count() > 1))
        {
            Add("R13", Path(path, twice.First()), "its icon is also " + string.Join(", ", twice.Skip(1).Select(r => r.Text)) + "'s");
        }

        // R12: nothing destructive mid-level.
        var settings = rows.Where(r => r.Kind is not (RuleKind.Heading or RuleKind.Note)).ToList();
        for (var at = 0; at < settings.Count - 1; at++)
        {
            if (settings[at] is { Danger: true, Kind: RuleKind.Action } danger)
            {
                Add("R12", Path(path, danger), "a destructive row mid-level");
            }
        }

        // R11: a reset is called Reset all and stands last.
        var resetAll = Str.SettingsPages_General_FormatsReset.Text();
        foreach (var reset in rows.Where(r => r.Kind == RuleKind.Action && r.Glyph == Glyphs.Reset))
        {
            if (reset.Text != resetAll)
            {
                Add("R11", Path(path, reset), "a reset not called " + resetAll);
            }
            else if (!ReferenceEquals(reset, settings[^1]))
            {
                Add("R11", Path(path, reset), resetAll + " is not last");
            }
        }

        for (var at = 0; at < level.Count; at++)
        {
            var row = level[at];
            if (row.Kind == RuleKind.Separator)
            {
                continue;
            }

            var here = Path(path, row);
            if (row.Kind == RuleKind.Heading)
            {
                // R10: a heading over rows of its own.
                var next = at + 1 < level.Count ? level[at + 1] : null;
                if (next is null or { Kind: RuleKind.Separator or RuleKind.Heading })
                {
                    Add("R10", here, "a heading over no rows of its own");
                }

                continue;
            }

            Words(row, here, Add);
            Values(row, here, Add);

            // R20: a greyed row says why.
            if (row.Disabled && string.IsNullOrWhiteSpace(row.Why) && row.Kind is not (RuleKind.Note or RuleKind.Option))
            {
                Add("R20", here, "greyed, saying no why");
            }

            if (row.Below.Count == 0)
            {
                continue;
            }

            // R3: a choice of two with Off in it is a switch.
            var options = row.Below.Where(r => r.Kind == RuleKind.Option).Select(r => r.Text).ToList();
            if (row.Kind == RuleKind.Choice && options.Count == 2 && options.Contains(Str.Toggle_Off.Text()))
            {
                Add("R3", here, $"a choice of two with Off ({string.Join(", ", options)}): a switch");
            }

            // R4: a thing's card says what it holds where it holds settings.
            if (row.Kind == RuleKind.Level && row.Card && string.IsNullOrEmpty(row.Value)
                && row.Below.Any(r => r.Kind is RuleKind.Toggle or RuleKind.Choice or RuleKind.Number or RuleKind.Value))
            {
                Add("R4", here, "a card saying nothing of what it holds");
            }

            Walk(row.Below, [.. path, row.Text], found);
        }
    }

    /// <summary>R7, R8, R14 on a row's own words.</summary>
    private static void Words(RuleRow row, string here, Action<string, string, string> add)
    {
        if (row.Text.Contains('&'))
        {
            add("R8", here, "\"&\" for \"and\"");
        }

        // The user's own words (a view's name) and an option's words are theirs or the choice's.
        if (row.Thing || row.Kind is RuleKind.Option or RuleKind.Note)
        {
            return;
        }

        foreach (var (word, not) in Glossary)
        {
            if (not.Contains(row.Text, StringComparer.Ordinal))
            {
                add("R7", here, $"\"{row.Text}\" for \"{word}\"");
            }
        }

        if (row.Kind is RuleKind.Level or RuleKind.Choice && Sentence.IsMatch(row.Text))
        {
            add("R7", here, "a sentence for a title");
        }

        if (row.Text.StartsWith("Add ", StringComparison.Ordinal) && !AddA.IsMatch(row.Text))
        {
            add("R8", here, "not \"Add a …\"");
        }

        if (row.Text.Split(' ').Skip(1).FirstOrDefault(w => w.Length > 1 && char.IsUpper(w[0]) && !Proper.Contains(w.Trim('(', ')', ',', '…', '.', ':'))) is { } capital)
        {
            add("R8", here, $"\"{capital}\" in sentence case");
        }

        var control = row.Kind switch
        {
            RuleKind.Toggle => 7,
            RuleKind.Number => 15,
            _ => 0,
        };
        if (row.Text.Length + control > LongestTitle)
        {
            add("R14", here, $"its title is cut ({row.Text.Length} characters)");
        }
    }

    /// <summary>R4, R14, R18, R21 on what a row says at its end.</summary>
    private static void Values(RuleRow row, string here, Action<string, string, string> add)
    {
        if (row.Kind is RuleKind.Choice or RuleKind.Number && string.IsNullOrWhiteSpace(row.Value))
        {
            add("R4", here, "no value at its end");
        }

        foreach (var said in new[] { row.Text, row.Value }.OfType<string>())
        {
            if (OldDefaults.Any(old => said.StartsWith(old, StringComparison.Ordinal)) || said.EndsWith("(default)", StringComparison.OrdinalIgnoreCase))
            {
                add("R18", here, $"\"{said}\", not \"Default (…)\"");
            }
        }

        if (row.Value is not { } value || row.Thing)
        {
            return;
        }

        if (value == Str.Common_Default.Text())
        {
            add("R18", here, "a bare \"Default\", not what it comes to");
        }

        if (row.Kind != RuleKind.Note && Measured(value) is { Length: > LongestValue } measured)
        {
            add("R14", here, $"its value \"{value}\" is {measured.Length} characters");
        }

        if (row.Kind == RuleKind.Number && Digits.IsMatch(value))
        {
            add("R21", here, $"\"{value}\" says no unit");
        }
    }
}
