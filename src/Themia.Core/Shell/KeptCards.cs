namespace Themia.Core.Shell;

/// <summary>
/// [S-2361] What a settings page's own drawing state is, apart from its rows: the card opened in it, the pickers unfolded
/// inside that card, what is typed in a lookup's box, why a text was refused, how many answers each lookup has had.
/// Every map is by the row's key.
/// </summary>
public sealed record PageMarks(
    string? Open,
    IReadOnlySet<string> Unfolded,
    IReadOnlyDictionary<string, string> Typed,
    IReadOnlyDictionary<string, string> Refused,
    IReadOnlyDictionary<string, int> Answered);

/// <summary>
/// [S-2361] A page drawn again over the same rows (a card opened or closed, a picker unfolded, a tab, a sub-page and back,
/// a lookup's typing or answer) keeps every card that would be drawn the same: a card is drawn from its rows and from the
/// page's marks of its own rows only (<see cref="PageMarks"/>), so a card is kept while those marks are what it was drawn
/// with (<see cref="Of"/>, compared by value); any other is drawn anew. Over new rows (the state changed) nothing is kept.
/// Pure.
/// </summary>
public static class KeptCards
{
    /// <summary>
    /// The marks a card holding the rows keyed <paramref name="keys"/> (its rows at every depth) is drawn with:
    /// <paramref name="inside"/> another card (a sub-page's card is not), and each mark of <paramref name="page"/> that
    /// names one of its rows.
    /// </summary>
    public static CardMarks Of(IEnumerable<string> keys, bool inside, PageMarks page)
    {
        ArgumentNullException.ThrowIfNull(keys);
        ArgumentNullException.ThrowIfNull(page);

        var marks = new List<(int Mark, string Key, string Value)>();
        foreach (var key in keys.Distinct(StringComparer.Ordinal))
        {
            if (string.Equals(page.Open, key, StringComparison.Ordinal))
            {
                marks.Add((0, key, string.Empty));
            }

            if (page.Unfolded.Contains(key))
            {
                marks.Add((1, key, string.Empty));
            }

            if (page.Typed.TryGetValue(key, out var typed))
            {
                marks.Add((2, key, typed));
            }

            if (page.Refused.TryGetValue(key, out var refused))
            {
                marks.Add((3, key, refused));
            }

            if (page.Answered.TryGetValue(key, out var answered))
            {
                marks.Add((4, key, answered.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            }
        }

        marks.Sort((a, b) => a.Mark != b.Mark ? a.Mark.CompareTo(b.Mark) : string.CompareOrdinal(a.Key, b.Key));
        return new CardMarks(inside, marks);
    }
}

/// <summary>[S-2361] The marks one card was drawn with (<see cref="KeptCards.Of"/>): equal marks over the same rows draw the same card.</summary>
public sealed class CardMarks : IEquatable<CardMarks>
{
    private readonly IReadOnlyList<(int Mark, string Key, string Value)> _marks;

    internal CardMarks(bool inside, IReadOnlyList<(int Mark, string Key, string Value)> marks)
    {
        Inside = inside;
        _marks = marks;
    }

    /// <summary>Drawn inside another card.</summary>
    public bool Inside { get; }

    /// <summary>How many of the page's marks name the card's rows.</summary>
    public int Count => _marks.Count;

    public bool Equals(CardMarks? other)
        => other is not null && Inside == other.Inside && _marks.SequenceEqual(other._marks);

    public override bool Equals(object? obj) => Equals(obj as CardMarks);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Inside);
        foreach (var mark in _marks)
        {
            hash.Add(mark.Mark);
            hash.Add(mark.Key, StringComparer.Ordinal);
            hash.Add(mark.Value, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }
}
