namespace Themia.Core.Widgets;

/// <summary>
/// [S-1980] Type-ahead in a long widget list (files, people, notes, shortcuts): typing a name's first
/// letters while the widget holds the keyboard lease moves the cursor to the first item whose name
/// starts with them, as in Explorer.
/// <para>
/// The rules, all here so they are tested once: letters typed within <see cref="ResetAfterMs"/> of
/// each other build one prefix, a pause starts a new one. The search starts at the cursor itself, so
/// a growing prefix ("jo" → "joh") stays on the item it already matches, and wraps round the end. A
/// prefix of one letter repeated ("ss") steps through the items starting with that letter instead,
/// from the one after the cursor. Case is ignored (ordinal, so it needs no culture data). A control
/// character never types; a space does only inside a prefix, since a lone Space is the widget's own
/// key (tick, select, pause). No match leaves the cursor where it is.
/// </para>
/// <para>
/// The pause is the one clock here, and it is a gesture boundary, not a correctness wait: the caller
/// passes a monotonic time (<c>Environment.TickCount64</c>) so the rule is tested without one.
/// </para>
/// </summary>
public sealed class TypeAhead
{
    /// <summary>A pause longer than this starts a new prefix (Explorer's is about a second).</summary>
    public const long ResetAfterMs = 1000;

    private string _prefix = string.Empty;
    private long _lastAt;

    /// <summary>The prefix typed so far, empty when none is building.</summary>
    public string Prefix => _prefix;

    /// <summary>Forget the prefix: the lease ended, or the list was replaced.</summary>
    public void Reset() => _prefix = string.Empty;

    /// <summary>
    /// Add <paramref name="character"/> typed at <paramref name="nowMs"/> and return the item the
    /// cursor goes to, or null when the character does not type or nothing matches.
    /// </summary>
    /// <param name="names">Each item's name, in the order the list shows them.</param>
    /// <param name="current">The cursor, or negative when there is none.</param>
    public int? Type(char character, long nowMs, IReadOnlyList<string> names, int current)
    {
        ArgumentNullException.ThrowIfNull(names);

        if (nowMs - _lastAt > ResetAfterMs || nowMs < _lastAt)
        {
            _prefix = string.Empty;
        }

        if (char.IsControl(character) || (char.IsWhiteSpace(character) && _prefix.Length == 0))
        {
            return null;
        }

        _lastAt = nowMs;
        _prefix += character;

        return Find(names, current, _prefix);
    }

    /// <summary>The item <paramref name="prefix"/> lands on from <paramref name="current"/>, or null.</summary>
    public static int? Find(IReadOnlyList<string> names, int current, string prefix)
    {
        ArgumentNullException.ThrowIfNull(names);

        if (names.Count == 0 || prefix.Length == 0)
        {
            return null;
        }

        // "sss" means "the next one starting with s", not "a name starting with sss".
        var repeated = prefix.Length > 1 && prefix.All(c => char.ToUpperInvariant(c) == char.ToUpperInvariant(prefix[0]));
        var wanted = repeated ? prefix[..1] : prefix;
        var start = current < 0 || current >= names.Count ? 0 : (wanted.Length == 1 ? current + 1 : current);

        for (var step = 0; step < names.Count; step++)
        {
            var index = (start + step) % names.Count;

            if ((names[index] ?? string.Empty).TrimStart().StartsWith(wanted, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return null;
    }
}
