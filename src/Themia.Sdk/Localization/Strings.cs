using System.Text;
using Themia.Core.Diagnostics;
using Themia.Sdk;

namespace Themia.Core.Localization;

/// <summary>
/// [S-1100..S-1104] The app's text, in the current language.
/// <code>
/// Str.Common_Save.Text()                                        // "Save" / "Speichern"
/// Str.Common_DeleteEmptyConfirm.Format(("name", list.Name))    // named parameters
/// Str.Shortcut_AppCount.Plural(apps.Count)                      // plural-safe, fills {count}
/// </code>
/// <para>
/// Lookup is an array read in the generated tables — no dictionary, no reflection, nothing loaded
/// at run time. A string the current language lacks falls back to English (logged once at debug
/// level); a key nobody has returns the key itself. Never an exception, never an empty string
/// where the table has text.
/// </para>
/// </summary>
public static class Strings
{
    private const string Scope = "i18n";

    private static Dictionary<string, Str>? _byKey;
    private static volatile Locale _current = Locale.En;

    /// <summary>The language every lookup without an explicit locale uses.</summary>
    public static Locale Current => _current;

    /// <summary>
    /// [S-1110] Raised after <see cref="Current"/> changed, on the thread that changed it (the UI
    /// thread, in the app). Settings pages rebuild and cards re-title on it. A static event: every
    /// subscriber must unsubscribe when it goes away (a page's Unloaded, a view's Detach).
    /// </summary>
    public static event Action<Locale>? Changed;

    /// <summary>Switch the language. A no-op (and no event) when it is already the current one.</summary>
    public static void SetLocale(Locale locale)
    {
        if (!Enum.IsDefined(locale))
        {
            Log.Warn(Scope, $"ignored an unknown locale {(int)locale}");
            return;
        }

        if (_current == locale)
        {
            return;
        }

        _current = locale;
        Log.Info(Scope, $"language is now {LanguageResolver.Code(locale)}");
        Changed?.Invoke(locale);
    }

    /// <summary>
    /// [S-3770] The regional formats changed (<see cref="RegionFormat"/>): the language did not, but
    /// every time on screen may read differently, so everything that redraws on a language change
    /// redraws now.
    /// </summary>
    internal static void NotifyFormatsChanged() => Changed?.Invoke(_current);

    // -----------------------------------------------------------------------------------------
    // Lookup
    // -----------------------------------------------------------------------------------------

    /// <summary>The text of <paramref name="id"/> in <paramref name="locale"/>, English where it has none.</summary>
    public static string Get(Locale locale, Str id) => StringTables.Table.Get(locale, (int)id);

    /// <summary>The host's own table (<see cref="Str"/>), as every module's is: a <see cref="StringTable"/>.</summary>
    public static StringTable Host => StringTables.Table;

    /// <summary>
    /// [S-1104] The lookup with its fallback, over explicit tables (so it can be tested with gaps).
    /// <paramref name="table"/> notes a missing text and names an unknown id; the host's when not given.
    /// </summary>
    internal static string Lookup(LocaleTable own, LocaleTable english, int index, StringTable? table = null)
    {
        table ??= StringTables.Table;
        if (own.Text(index) is { } text)
        {
            return text;
        }

        if (english.Text(index) is { } fallback)
        {
            table.NoteMissing(own.Locale, index);
            return fallback;
        }

        return table.Unknown(index);
    }

    /// <summary>The text of a key given as a string (a key built at run time, e.g. per widget kind).</summary>
    public static string Get(Locale locale, string key)
    {
        if (TryParseKey(key, out var id))
        {
            return Get(locale, id);
        }

        Log.Debug(Scope, $"no string has the key '{key}'");
        return key;
    }

    /// <summary>The <see cref="Str"/> for a dotted key, e.g. <c>"widget.clock.label"</c>.</summary>
    public static bool TryParseKey(string? key, out Str id)
    {
        id = default;
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        var map = _byKey ??= BuildKeyMap();
        return map.TryGetValue(key, out id);
    }

    /// <summary>
    /// The plural form of <paramref name="id"/> for <paramref name="count"/>, with <c>{count}</c>
    /// filled in (number formatted for the language) as well as <paramref name="args"/>.
    /// Falls back form by form: the language's own category, its "other", then English.
    /// A key that is not a plural just formats.
    /// </summary>
    public static string Plural(Locale locale, Str id, long count, params ReadOnlySpan<StringArg> args)
        => StringTables.Table.Plural(locale, (int)id, count, args);

    /// <summary>The text of <paramref name="id"/> with its <c>{name}</c> placeholders filled in.</summary>
    public static string Format(Locale locale, Str id, params ReadOnlySpan<StringArg> args)
        => Interpolate(Get(locale, id), locale, args, null);

    /// <summary>
    /// [S-1102] Replace each <c>{name}</c> with the argument of that name, in one pass. A placeholder
    /// without an argument stays as written (visible, not silently blank); a value is never re-scanned,
    /// so a name or file that itself contains "{count}" is shown as is.
    /// </summary>
    public static string Interpolate(string template, Locale locale, ReadOnlySpan<StringArg> args, long? count = null)
    {
        ArgumentNullException.ThrowIfNull(template);
        if ((args.IsEmpty && count is null) || template.IndexOf('{') < 0)
        {
            return template;
        }

        var sb = new StringBuilder(template.Length + 16);
        var i = 0;
        while (i < template.Length)
        {
            var open = template.IndexOf('{', i);
            if (open < 0)
            {
                sb.Append(template, i, template.Length - i);
                break;
            }

            sb.Append(template, i, open - i);
            var close = PlaceholderEnd(template, open);
            if (close < 0)
            {
                sb.Append('{');
                i = open + 1;
                continue;
            }

            var name = template.AsSpan(open + 1, close - open - 1);
            if (Resolve(name, locale, args, count) is { } value)
            {
                sb.Append(value);
            }
            else
            {
                sb.Append(template, open, close - open + 1);
            }

            i = close + 1;
        }

        return sb.ToString();
    }

    /// <summary>The names of the <c>{placeholders}</c> in a text, sorted (what the completeness test compares).</summary>
    public static IReadOnlyList<string> Placeholders(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var names = new SortedSet<string>(StringComparer.Ordinal);
        for (var open = text.IndexOf('{'); open >= 0; open = text.IndexOf('{', open + 1))
        {
            var close = PlaceholderEnd(text, open);
            if (close > 0)
            {
                names.Add(text.Substring(open + 1, close - open - 1));
            }
        }

        return [.. names];
    }

    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// [S-1103]/[S-1104] The plural form for a count, over explicit tables: the language's own
    /// category, its "other", then English's form for the count.
    /// </summary>
    internal static string PluralLookup(LocaleTable own, LocaleTable english, int index, long count, StringTable? table = null)
    {
        table ??= StringTables.Table;
        if (own.Forms(index) is { } forms)
        {
            var form = forms[(int)PluralRules.For(own.Locale, count)] ?? forms[(int)PluralCategory.Other];
            if (form is not null)
            {
                return form;
            }
        }
        else if (own.Text(index) is { } text)
        {
            // A key this language wrote as one string (a key that is not a plural at all): the one
            // string serves every count.
            return text;
        }

        table.NoteMissing(own.Locale, index);

        if (english.Forms(index) is { } enForms)
        {
            return enForms[(int)PluralRules.For(english.Locale, count)] ?? enForms[(int)PluralCategory.Other] ?? table.Unknown(index);
        }

        return english.Text(index) ?? table.Unknown(index);
    }

    private static string? Resolve(ReadOnlySpan<char> name, Locale locale, ReadOnlySpan<StringArg> args, long? count)
    {
        foreach (var arg in args)
        {
            if (name.SequenceEqual(arg.Name))
            {
                return arg.Render(locale);
            }
        }

        return count is { } n && name.SequenceEqual("count") ? LocaleFormat.Integer(locale, n) : null;
    }

    /// <summary>The index of the <c>}</c> closing a placeholder that opens at <paramref name="open"/>; -1 when it is not one.</summary>
    private static int PlaceholderEnd(string text, int open)
    {
        var i = open + 1;
        while (i < text.Length && (char.IsAsciiLetterOrDigit(text[i]) || text[i] == '_'))
        {
            i++;
        }

        return i > open + 1 && i < text.Length && text[i] == '}' ? i : -1;
    }

    private static Dictionary<string, Str> BuildKeyMap()
    {
        var map = new Dictionary<string, Str>(StringTables.Count, StringComparer.Ordinal);
        for (var i = 0; i < StringTables.Keys.Length; i++)
        {
            map[StringTables.Keys[i]] = (Str)i;
        }

        return map;
    }
}

/// <summary>The short forms the extraction uses: <c>Str.X.Text()</c>, <c>.Format(...)</c>, <c>.Plural(n)</c>.</summary>
public static class StrExtensions
{
    /// <summary>The text in the current language.</summary>
    public static string Text(this Str id) => Strings.Get(Strings.Current, id);

    /// <summary>The text with its <c>{name}</c> placeholders filled: <c>Str.X.Format(("name", value))</c>.</summary>
    public static string Format(this Str id, params ReadOnlySpan<StringArg> args) => Strings.Format(Strings.Current, id, args);

    /// <summary>The plural form for <paramref name="count"/>, with <c>{count}</c> filled in.</summary>
    public static string Plural(this Str id, long count, params ReadOnlySpan<StringArg> args) => Strings.Plural(Strings.Current, id, count, args);

    /// <summary>The dotted key, e.g. <c>common.save</c>.</summary>
    public static string Key(this Str id) => StringCatalog.Key(id);

    /// <summary>The text as a <see cref="LocalText"/>: how a descriptor hands its name to the host ([S-1109]).</summary>
    public static LocalText Local(this Str id) => new(StringTables.Table, (int)id);
}

/// <summary>
/// A named value for a placeholder. Written as a tuple — <c>("name", "Work")</c> or
/// <c>("count", 3)</c> — so a call site reads like the text it fills. Numbers are formatted for the
/// language at the moment the text is built.
/// </summary>
public readonly struct StringArg
{
    private readonly string? _text;
    private readonly long _number;
    private readonly bool _isNumber;

    private StringArg(string name, string? text, long number, bool isNumber)
    {
        Name = name ?? string.Empty;
        _text = text;
        _number = number;
        _isNumber = isNumber;
    }

    public string Name { get; }

    public static StringArg Of(string name, string? value) => new(name, value, 0, false);

    public static StringArg Of(string name, long value) => new(name, null, value, true);

    public static implicit operator StringArg((string Name, string? Value) arg) => Of(arg.Name, arg.Value);

    public static implicit operator StringArg((string Name, long Value) arg) => Of(arg.Name, arg.Value);

    public static implicit operator StringArg((string Name, int Value) arg) => Of(arg.Name, arg.Value);

    internal string Render(Locale locale) => _isNumber ? LocaleFormat.Integer(locale, _number) : _text ?? string.Empty;
}

/// <summary>
/// [S-1105] Read access to the compiled tables as they are, without fallback — for the completeness
/// test and for tooling. The UI reads through <see cref="Strings"/>.
/// </summary>
public static class StringCatalog
{
    public static int Count => StringTables.Count;

    public static IEnumerable<Str> All
    {
        get
        {
            for (var i = 0; i < StringTables.Count; i++)
            {
                yield return (Str)i;
            }
        }
    }

    public static string Key(Str id)
        => (uint)id < (uint)StringTables.Count ? StringTables.Keys[(int)id] : $"#{(int)id}";

    /// <summary>Listed under <c>$englishOnly</c> in en.json: not translated yet, on purpose.</summary>
    public static bool IsEnglishOnly(Str id) => Array.IndexOf(StringTables.EnglishOnly, (int)id) >= 0;

    /// <summary>The language's own text (a plural's "other"); null when it has none.</summary>
    public static string? Raw(Locale locale, Str id) => StringTables.For(locale).Text((int)id);

    /// <summary>The language's plural forms by <see cref="PluralCategory"/>; null when the key is not a plural there.</summary>
    public static IReadOnlyList<string?>? RawForms(Locale locale, Str id) => StringTables.For(locale).Forms((int)id);
}
