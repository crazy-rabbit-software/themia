namespace Themia.Core.Localization;

/// <summary>
/// One language's compiled strings, as the generator emits them: a text per <see cref="Str"/> id
/// (null where the language has none — the caller falls back to English) and, for plural keys, the
/// six CLDR forms.
/// </summary>
internal sealed class LocaleTable
{
    private readonly string?[] _texts;
    private readonly string?[]?[] _plurals;

    public LocaleTable(Locale locale, string?[] texts, (int Id, string?[] Forms)[] plurals)
    {
        Locale = locale;
        _texts = texts;

        // Sparse in the generated source, indexed here so a lookup is one array read.
        _plurals = new string?[]?[texts.Length];
        foreach (var (id, forms) in plurals)
        {
            _plurals[id] = forms;
        }
    }

    public Locale Locale { get; }

    /// <summary>The text (for a plural key, its "other" form); null when this language lacks it.</summary>
    public string? Text(int id) => (uint)id < (uint)_texts.Length ? _texts[id] : null;

    /// <summary>The plural forms by <see cref="PluralCategory"/>; null when the key is not a plural here.</summary>
    public string?[]? Forms(int id) => (uint)id < (uint)_plurals.Length ? _plurals[id] : null;
}
