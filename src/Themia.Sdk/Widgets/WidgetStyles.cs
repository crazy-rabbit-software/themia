using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-6001] One look a widget kind offers: the enum value it is stored as, its wire name (the
/// camelCase name, also what <c>--style</c> takes), the name the user reads and its Segoe Fluent
/// Icons code point.
/// </summary>
/// <param name="Value">The style enum's value, as an int.</param>
/// <param name="Name">The camelCase wire name: <c>digital</c>, <c>flip</c>.</param>
/// <param name="Label">What the style dropdown and the settings row call it.</param>
/// <param name="Glyph">The Segoe Fluent Icons code point beside the name (U+E700..U+F8FF).</param>
public readonly record struct StyleOption(int Value, string Name, LocalText Label, int Glyph)
{
    /// <summary>The option for one value of a kind's style enum.</summary>
    public static StyleOption Of<T>(T value, LocalText label, int glyph)
        where T : struct, Enum
        => new(WidgetStyles.ToInt(value), WidgetStyles.WireName(value), label, glyph);
}

/// <summary>
/// [S-6000]..[S-6003] A widget kind with several looks of the same data. Implemented by the kind's
/// config class; <see cref="WidgetStyles"/> finds it through this interface, so a kind gains styles
/// without a line anywhere else.
/// <para>
/// How to give a kind styles: declare an int-backed enum <c>&lt;Kind&gt;Style</c> next to the config,
/// marked <c>[JsonConverter(typeof(StyleJsonConverter&lt;&lt;Kind&gt;Style&gt;))]</c>; give the
/// config a public <c>Style</c> property (it is what is stored, as <c>"style": "camelName"</c>) whose
/// default is the first option; implement this interface explicitly over it (the explicit members
/// are not serialised); and copy <c>Style</c> in <c>Clone</c>. The registry repairs an unknown
/// stored value to the first option on load; the config's own <c>Repair</c> need not check it.
/// </para>
/// <para>
/// The view's side ([S-6005]): a style is a config value like any other. The card hands the
/// changed config to <c>IWidgetView.ApplyConfig</c>; the view reads <c>Style</c> there and
/// re-lays itself (the same responsive ladder, [S-660]..[S-666], applies to every style). It must
/// not keep parts of the old style alive: what one style subscribes or builds is released when
/// another is chosen.
/// </para>
/// </summary>
public interface IStyled
{
    /// <summary>The kind's styles in menu order, the default first. The same list for every instance.</summary>
    IReadOnlyList<StyleOption> Styles { get; }

    /// <summary>The current style, as its enum's int value. May be outside <see cref="Styles"/> until repaired.</summary>
    int StyleValue { get; set; }

    /// <summary>
    /// [S-6075] Whether the styles draw differently now: false where every look draws the same (a
    /// gallery of one picture), so the properties menu has no Style line until they would differ
    /// again. The style is kept for then.
    /// </summary>
    bool StyleApplies => true;
}

/// <summary>
/// [S-6000]..[S-6004] The one registry of widget styles, for every kind: discovered from each
/// kind's default config (<see cref="IStyled"/>), never a list kept beside them.
/// </summary>
public static class WidgetStyles
{
    private static readonly Lazy<Dictionary<WidgetId, IReadOnlyList<StyleOption>>> ByKind = new(Discover);

    /// <summary>[S-6001] The styles of <paramref name="kind"/>; empty for a kind without styles.</summary>
    public static IReadOnlyList<StyleOption> For(WidgetId kind)
        => ByKind.Value.TryGetValue(kind, out var styles) ? styles : [];

    /// <summary>[S-6002] Whether a kind has a style choice at all: at least two styles.</summary>
    public static bool Offers(WidgetId kind) => For(kind).Count >= 2;

    /// <summary>Every kind that offers a style choice.</summary>
    public static IEnumerable<WidgetId> StyledKinds => ModuleCatalog.Widgets.Select(w => w.Id).Where(Offers);

    /// <summary>The current style of <paramref name="config"/>; null for a kind without styles.</summary>
    public static int? Current(WidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return config is IStyled styled ? styled.StyleValue : null;
    }

    /// <summary>[S-6075] Whether <paramref name="config"/>'s style changes what it draws now; false for a kind without styles.</summary>
    public static bool Applies(WidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return config is IStyled styled && styled.StyleApplies;
    }

    /// <summary>
    /// [S-6003] Set the style of <paramref name="config"/>. False (and nothing written) for a kind
    /// without styles, a value that is not one of its styles, or the style it already has.
    /// </summary>
    public static bool Set(WidgetConfig config, int value)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (config is not IStyled styled || styled.StyleValue == value || !styled.Styles.Any(s => s.Value == value))
        {
            return false;
        }

        styled.StyleValue = value;
        return true;
    }

    /// <summary>[S-6006] The option a wire name (<c>--style flip</c>) names for a kind, case-insensitive.</summary>
    public static StyleOption? Find(WidgetId kind, string? name)
    {
        foreach (var option in For(kind))
        {
            if (string.Equals(option.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                return option;
            }
        }

        return null;
    }

    /// <summary>
    /// [S-6004] Load validation: a stored style that is not one of the kind's (an unknown name, a
    /// number, a hand edit) becomes the first style. Returns what was repaired, or null.
    /// </summary>
    public static string? Repair(WidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (config is not IStyled styled || styled.Styles.Count == 0)
        {
            return null;
        }

        var value = styled.StyleValue;
        if (styled.Styles.Any(s => s.Value == value))
        {
            return null;
        }

        var fallback = styled.Styles[0];
        styled.StyleValue = fallback.Value;
        return $"{config.Kind} style {(value == StyleJson.Unknown ? "unknown" : value.ToString(System.Globalization.CultureInfo.InvariantCulture))}; {fallback.Name}";
    }

    internal static int ToInt<T>(T value)
        where T : struct, Enum
    {
        RequireInt<T>();
        return Unsafe.BitCast<T, int>(value);
    }

    internal static T FromInt<T>(int value)
        where T : struct, Enum
    {
        RequireInt<T>();
        return Unsafe.BitCast<int, T>(value);
    }

    /// <summary>The camelCase wire name of a style value: <c>Word</c> is <c>word</c>.</summary>
    internal static string WireName<T>(T value)
        where T : struct, Enum
    {
        var name = value.ToString();
        return name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name[1..];
    }

    private static void RequireInt<T>()
        where T : struct, Enum
    {
        if (Unsafe.SizeOf<T>() != sizeof(int))
        {
            throw new InvalidOperationException($"{typeof(T).Name}: a style enum is int-backed");
        }
    }

    private static Dictionary<WidgetId, IReadOnlyList<StyleOption>> Discover()
    {
        var table = new Dictionary<WidgetId, IReadOnlyList<StyleOption>>();
        foreach (var widget in ModuleCatalog.Widgets)
        {
            if (widget.CreateDefault() is IStyled styled)
            {
                table[widget.Id] = styled.Styles;
            }
        }

        return table;
    }
}

/// <summary>Shared by <see cref="StyleJsonConverter{T}"/> and the repair: the value an unreadable stored style reads as.</summary>
internal static class StyleJson
{
    /// <summary>Never a style's value: the converter's "not a name this build knows", for the repair to report.</summary>
    public const int Unknown = -1;
}

/// <summary>
/// [S-6004] A style enum on the wire: its camelCase name. Reading never throws, because a throw
/// inside one widget's config refuses the whole state file: an unknown name or a token that is not
/// a string reads as <see cref="StyleJson.Unknown"/>, which load validation repairs to the kind's
/// first style and logs ([S-6004]).
/// </summary>
public sealed class StyleJsonConverter<T> : JsonConverter<T>
    where T : struct, Enum
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var wire = reader.GetString();

            // Names only: Enum.TryParse would also take "7", which is not a name this build wrote.
            if (wire is { Length: > 0 } && char.IsAsciiLetter(wire[0])
                && Enum.TryParse<T>(wire, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
            {
                return parsed;
            }

            return WidgetStyles.FromInt<T>(StyleJson.Unknown);
        }

        reader.Skip();
        return WidgetStyles.FromInt<T>(StyleJson.Unknown);
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(WidgetStyles.WireName(value));
    }
}
