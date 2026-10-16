namespace Themia.Core.Widgets;

/// <summary>
/// One entry of the font picker: the stored value (a preset or a family name), what it is called, and
/// whether it is a symbol font, whose own glyphs cannot spell its name ([S-6440]).
/// </summary>
public sealed record FontOption(string Value, string Label, bool Symbol = false);

/// <summary>
/// [S-827] [S-6440] The installed families as the picker lists them, and which of them are symbol
/// fonts (Wingdings, Segoe Fluent Icons) that cannot spell their own name: every GDI report of such
/// a family says so (the symbol charset, or no Basic Latin in its Unicode signature).
/// </summary>
public sealed record InstalledFonts(IReadOnlyList<string> Families, IReadOnlySet<string> Symbol)
{
    public static InstalledFonts None { get; } = new([], new HashSet<string>());

    /// <summary>
    /// From GDI's reports, one per family and script: the family list ([S-827]), and the families
    /// every report of which is a symbol face.
    /// </summary>
    public static InstalledFonts From(IEnumerable<(string Name, bool Symbol)> reports)
    {
        ArgumentNullException.ThrowIfNull(reports);

        var symbolOnly = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var names = new List<string>();
        foreach (var (name, symbol) in reports)
        {
            var key = name?.Trim() ?? string.Empty;
            names.Add(key);
            symbolOnly[key] = symbolOnly.TryGetValue(key, out var all) ? all && symbol : symbol;
        }

        var families = DesignLook.FontList(names);
        var symbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var family in families)
        {
            if (symbolOnly.TryGetValue(family, out var only) && only)
            {
                symbols.Add(family);
            }
        }

        return new InstalledFonts(families, symbols);
    }
}

/// <summary>
/// [S-6440]..[S-6443] What the font picker lists and how its search box narrows it. Kept apart from
/// the control so the rules a few hundred installed families have to obey are tested, not eyeballed.
/// </summary>
public static class FontSearch
{
    /// <summary>
    /// [S-6440] The picker's list: the presets in their order, then the installed families in theirs
    /// ([S-827]). A family the setting names that is not installed (uninstalled since, or the list not
    /// read yet) is listed right after the presets, so the picker still shows what is set. A name
    /// that is a preset or already listed is not listed twice. A symbol family is marked, so its
    /// name is drawn in the UI font rather than as icons.
    /// </summary>
    public static IReadOnlyList<FontOption> Options(IReadOnlyList<FontOption> presets, InstalledFonts fonts, string? current)
    {
        ArgumentNullException.ThrowIfNull(presets);
        ArgumentNullException.ThrowIfNull(fonts);

        var installed = fonts.Families;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<FontOption>(presets.Count + installed.Count + 1);

        foreach (var preset in presets)
        {
            if (seen.Add(preset.Value))
            {
                list.Add(preset);
            }
        }

        var own = current?.Trim();
        if (DesignLook.IsFamilyName(own) && !seen.Contains(own!) && !installed.Contains(own!, StringComparer.OrdinalIgnoreCase))
        {
            seen.Add(own!);
            list.Add(new FontOption(own!, own!));
        }

        foreach (var family in installed)
        {
            if (DesignLook.IsFamilyName(family) && seen.Add(family))
            {
                list.Add(new FontOption(family, family, fonts.Symbol.Contains(family)));
            }
        }

        return list;
    }

    /// <summary>
    /// [S-6441] The value of the entry the picker ticks: the listed entry <paramref name="current"/>
    /// names (case-insensitively), or the system font when it names none, as the card falls back ([S-826]).
    /// </summary>
    public static string Chosen(IReadOnlyList<FontOption> options, string? current)
    {
        ArgumentNullException.ThrowIfNull(options);

        var wanted = current?.Trim();
        foreach (var option in options)
        {
            if (string.Equals(option.Value, wanted, StringComparison.OrdinalIgnoreCase))
            {
                return option.Value;
            }
        }

        return DesignLook.SystemFont;
    }

    /// <summary>
    /// [S-6442] The entries a query keeps, best first. Case- and accent-insensitive
    /// (<see cref="WidgetSearch.Normalize"/>); a name matches when the query is anywhere in it, also
    /// with the spaces left out on both sides ("segoeui"). Names starting with the query come first,
    /// then names with a word starting with it, then the rest; within each, the list's own order.
    /// The system entry stays at the top whatever is typed, so going back to the default is one
    /// click away. A blank query keeps everything as it was.
    /// </summary>
    public static IReadOnlyList<FontOption> Filter(IReadOnlyList<FontOption> options, string? query)
    {
        ArgumentNullException.ThrowIfNull(options);

        var q = WidgetSearch.Normalize(query);
        if (q.Length == 0)
        {
            return options;
        }

        var squashedQuery = Squash(q);
        var pinned = new List<FontOption>(1);
        var ranked = new List<(int Rank, int Order, FontOption Option)>();

        for (var i = 0; i < options.Count; i++)
        {
            var option = options[i];
            if (string.Equals(option.Value, DesignLook.SystemFont, StringComparison.OrdinalIgnoreCase))
            {
                pinned.Add(option);
                continue;
            }

            if (Rank(WidgetSearch.Normalize(option.Label), q, squashedQuery) is { } rank)
            {
                ranked.Add((rank, i, option));
            }
        }

        ranked.Sort((a, b) => a.Rank != b.Rank ? a.Rank.CompareTo(b.Rank) : a.Order.CompareTo(b.Order));
        pinned.AddRange(ranked.Select(r => r.Option));
        return pinned;
    }

    /// <summary>
    /// [S-6442] What Enter in the search box picks: the best entry that matched what was typed. The
    /// pinned system entry only when its own name matched; null for a blank query or no match, so
    /// Enter then picks nothing.
    /// </summary>
    public static FontOption? Best(IReadOnlyList<FontOption> options, string? query)
    {
        ArgumentNullException.ThrowIfNull(options);

        var q = WidgetSearch.Normalize(query);
        if (q.Length == 0)
        {
            return null;
        }

        foreach (var option in Filter(options, query))
        {
            if (!string.Equals(option.Value, DesignLook.SystemFont, StringComparison.OrdinalIgnoreCase)
                || Rank(WidgetSearch.Normalize(option.Label), q, Squash(q)) is not null)
            {
                return option;
            }
        }

        return null;
    }

    /// <summary>0 = the name starts with the query, 1 = a word of it does, 2 = anywhere; null = no match.</summary>
    private static int? Rank(string name, string query, string squashedQuery)
    {
        if (name.StartsWith(query, StringComparison.Ordinal))
        {
            return 0;
        }

        var at = name.IndexOf(query, StringComparison.Ordinal);
        if (at > 0)
        {
            // Any later occurrence may be the one at a word start ("Noto Sans Mono" for "mo").
            for (; at > 0; at = name.IndexOf(query, at + 1, StringComparison.Ordinal))
            {
                if (!char.IsLetterOrDigit(name[at - 1]))
                {
                    return 1;
                }
            }

            return 2;
        }

        var squashed = Squash(name);
        if (squashed.StartsWith(squashedQuery, StringComparison.Ordinal))
        {
            return 0;
        }

        return squashed.Contains(squashedQuery, StringComparison.Ordinal) ? 2 : null;
    }

    private static string Squash(string text) => text.Replace(" ", string.Empty, StringComparison.Ordinal);
}
