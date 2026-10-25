using Themia.Core.Diagnostics;
using Themia.Core.Localization;

namespace Themia.Sdk;

/// <summary>
/// [S-6909] One compiled set of strings: the host's <c>Str</c> table or a module's own
/// (docs/WIDGET-KIT.md section 4.7). The generator builds one per project from its
/// <c>Localization/Locales</c> folder; every table looks up the same way ([S-1104]): the language's
/// own text, else English (noted once at debug level), else the key. Ids are the generated enum's
/// members as numbers; a table never mixes with another, so keys need no prefix rule.
/// </summary>
public sealed class StringTable
{
    private const string Scope = "i18n";

    private readonly string[] _keys;
    private readonly int[] _englishOnly;
    private readonly Func<Locale, LocaleTable> _tables;
    private readonly Lock _gate = new();
    private readonly HashSet<int> _missingLogged = [];

    /// <param name="name">The generated enum's name (<c>Str</c>, <c>ClockStr</c>): what logs and tests call the table.</param>
    /// <param name="keys">The dotted key of each id, in id order.</param>
    /// <param name="englishOnly">Ids its en.json lists under <c>$englishOnly</c>.</param>
    /// <param name="tables">One language's compiled table; built on first use by the generated code.</param>
    public StringTable(string name, string[] keys, int[] englishOnly, Func<Locale, LocaleTable> tables)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        _keys = keys ?? throw new ArgumentNullException(nameof(keys));
        _englishOnly = englishOnly ?? throw new ArgumentNullException(nameof(englishOnly));
        _tables = tables ?? throw new ArgumentNullException(nameof(tables));
    }

    public string Name { get; }

    public int Count => _keys.Length;

    /// <summary>The dotted key of <paramref name="id"/>, or <c>#id</c> for a number the table lacks.</summary>
    public string Key(int id) => (uint)id < (uint)_keys.Length ? _keys[id] : $"#{id}";

    /// <summary>The id of a dotted key, or -1.</summary>
    public int IdOf(string? key) => key is null ? -1 : Array.IndexOf(_keys, key);

    /// <summary>Listed under <c>$englishOnly</c> in en.json: not translated yet, on purpose.</summary>
    public bool IsEnglishOnly(int id) => Array.IndexOf(_englishOnly, id) >= 0;

    /// <summary>[S-1105] The language's own text (a plural's "other"), without fallback; null when it has none.</summary>
    public string? Raw(Locale locale, int id) => _tables(locale).Text(id);

    /// <summary>The language's plural forms by <see cref="PluralCategory"/>; null when the key is not a plural there.</summary>
    public IReadOnlyList<string?>? RawForms(Locale locale, int id) => _tables(locale).Forms(id);

    /// <summary>The text of <paramref name="id"/> in <paramref name="locale"/>, English where it has none.</summary>
    public string Get(Locale locale, int id) => Strings.Lookup(_tables(locale), _tables(Locale.En), id, this);

    /// <summary>The text with its <c>{name}</c> placeholders filled.</summary>
    public string Format(Locale locale, int id, params ReadOnlySpan<StringArg> args)
        => Strings.Interpolate(Get(locale, id), locale, args);

    /// <summary>The plural form for <paramref name="count"/>, with <c>{count}</c> filled in as well as <paramref name="args"/>.</summary>
    public string Plural(Locale locale, int id, long count, params ReadOnlySpan<StringArg> args)
        => Strings.Interpolate(Strings.PluralLookup(_tables(locale), _tables(Locale.En), id, count, this), locale, args, count);

    /// <summary>A language lacks a text English has: logged once per language and key.</summary>
    internal void NoteMissing(Locale locale, int id)
    {
        if ((uint)id >= (uint)_keys.Length || locale == Locale.En)
        {
            // A bad id is reported by Unknown; English is the fallback itself.
            return;
        }

        lock (_gate)
        {
            if (!_missingLogged.Add(((int)locale * _keys.Length) + id))
            {
                return;
            }
        }

        Log.Debug(Scope, $"{LanguageResolver.Code(locale)} has no '{_keys[id]}' in {Name}, showing English");
    }

    /// <summary>An id nobody has: only reachable through a cast from a number the enum does not have.</summary>
    internal string Unknown(int id)
    {
        Log.Warn(Scope, $"{Name} has no string with id {id}");
        return Key(id);
    }

    public override string ToString() => $"{Name} ({Count} keys)";
}

/// <summary>
/// [S-1109] A text a module hands the host without the host knowing its enum: a table and an id
/// (a descriptor's label and description, a provider's name), or a fixed name that is never
/// translated (a brand: "Microsoft"). Read each time, so a language switch reaches every caller.
/// </summary>
public readonly struct LocalText : IEquatable<LocalText>
{
    private readonly StringTable? _table;
    private readonly int _id;
    private readonly string? _fixed;

    public LocalText(StringTable table, int id)
    {
        _table = table ?? throw new ArgumentNullException(nameof(table));
        if ((uint)id >= (uint)table.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(id), id, $"{table.Name} has no id {id}");
        }

        _id = id;
        _fixed = null;
    }

    private LocalText(string text)
    {
        _table = null;
        _id = -1;
        _fixed = text;
    }

    /// <summary>A name every language writes the same way (a company or product name).</summary>
    public static LocalText Fixed(string text)
        => string.IsNullOrWhiteSpace(text) ? throw new ArgumentException("a fixed text is not empty", nameof(text)) : new LocalText(text);

    /// <summary>The table it reads from; null for a fixed text (and for <c>default</c>).</summary>
    public StringTable? Table => _table;

    /// <summary>Its id in <see cref="Table"/>; -1 for a fixed text.</summary>
    public int Id => _table is null ? -1 : _id;

    /// <summary>Neither a table entry nor a fixed text: a <c>default</c> nobody set.</summary>
    public bool IsEmpty => _table is null && _fixed is null;

    /// <summary>The dotted key, or the fixed text itself.</summary>
    public string Key => _table?.Key(_id) ?? _fixed ?? string.Empty;

    /// <summary>The text in the current language.</summary>
    public string Text() => Text(Strings.Current);

    public string Text(Locale locale) => _table?.Get(locale, _id) ?? _fixed ?? string.Empty;

    /// <summary>The text in <paramref name="locale"/> with its placeholders filled.</summary>
    public string Format(Locale locale, params ReadOnlySpan<StringArg> args)
        => _table is not null ? _table.Format(locale, _id, args) : Strings.Interpolate(_fixed ?? string.Empty, locale, args);

    public bool Equals(LocalText other) => ReferenceEquals(_table, other._table) && _id == other._id && _fixed == other._fixed;

    public override bool Equals(object? obj) => obj is LocalText other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(_table, _id, _fixed);

    public static bool operator ==(LocalText left, LocalText right) => left.Equals(right);

    public static bool operator !=(LocalText left, LocalText right) => !left.Equals(right);

    /// <summary>The text in the current language, so an interpolation reads like the text.</summary>
    public override string ToString() => Text();
}
