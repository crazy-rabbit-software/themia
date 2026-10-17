using System.Text.Json;
using System.Text.Json.Serialization;

namespace Themia.Sdk;

/// <summary>
/// [S-6900] The one shape every module id has (widgets, providers, features, cadences): camelCase,
/// ASCII letters and digits, a lowercase letter first, at most <see cref="MaxLength"/> characters.
/// The same rule the registry generator applies at build time (THEMIAMOD001), so an id read from a
/// file, the clipboard, a drag or a command line is checked exactly as a declared one is.
/// </summary>
public static class ModuleIds
{
    public const int MaxLength = 64;

    public static bool IsValid(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > MaxLength || value[0] is < 'a' or > 'z')
        {
            return false;
        }

        foreach (var c in value)
        {
            if (c is not ((>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9')))
            {
                return false;
            }
        }

        return true;
    }

    internal static string Checked(string value, string what)
        => IsValid(value) ? value : throw new ArgumentException($"'{value}' is not a valid {what} id (camelCase letters and digits)", nameof(value));
}

/// <summary>A widget's id: its config's discriminator in state.json, its module's key ([S-6900]).</summary>
[JsonConverter(typeof(WidgetIdJsonConverter))]
public readonly record struct WidgetId : IComparable<WidgetId>
{
    public WidgetId(string value) => Value = ModuleIds.Checked(value, "widget");

    /// <summary>The id; null only for <c>default</c>, which no declared module has.</summary>
    public string Value { get; }

    public static bool TryParse(string? text, out WidgetId id)
    {
        id = ModuleIds.IsValid(text) ? new WidgetId(text!) : default;
        return id.Value is not null;
    }

    /// <summary>Ids sort by their text, ordinally: the registry's order.</summary>
    public int CompareTo(WidgetId other) => string.CompareOrdinal(Value, other.Value);

    public override string ToString() => Value ?? string.Empty;
}

/// <summary>A provider's id: the service a module reads (<c>microsoft</c>, <c>google</c>, <c>github</c>).</summary>
[JsonConverter(typeof(ProviderIdJsonConverter))]
public readonly record struct ProviderId
{
    public ProviderId(string value) => Value = ModuleIds.Checked(value, "provider");

    public string Value { get; }

    public static bool TryParse(string? text, out ProviderId id)
    {
        id = ModuleIds.IsValid(text) ? new ProviderId(text!) : default;
        return id.Value is not null;
    }

    public override string ToString() => Value ?? string.Empty;
}

/// <summary>A feature an account may offer a widget (<c>mail</c>, <c>calendar</c>, a kit service's provider id).</summary>
[JsonConverter(typeof(FeatureIdJsonConverter))]
public readonly record struct FeatureId
{
    public FeatureId(string value) => Value = ModuleIds.Checked(value, "feature");

    public string Value { get; }

    public static bool TryParse(string? text, out FeatureId id)
    {
        id = ModuleIds.IsValid(text) ? new FeatureId(text!) : default;
        return id.Value is not null;
    }

    public override string ToString() => Value ?? string.Empty;
}

/// <summary>A row of Settings › Widgets › Updates ([S-3780]): one shared refresh cadence.</summary>
[JsonConverter(typeof(CadenceIdJsonConverter))]
public readonly record struct CadenceId
{
    public CadenceId(string value) => Value = ModuleIds.Checked(value, "cadence");

    public string Value { get; }

    public static bool TryParse(string? text, out CadenceId id)
    {
        id = ModuleIds.IsValid(text) ? new CadenceId(text!) : default;
        return id.Value is not null;
    }

    public override string ToString() => Value ?? string.Empty;
}

/// <summary>Reads and writes an id as its string; a malformed one is a corrupt document, not a value.</summary>
public abstract class ModuleIdJsonConverter<T> : JsonConverter<T>
    where T : struct
{
    protected abstract bool TryParse(string? text, out T id);

    protected abstract string? ValueOf(T id);

    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"expected an id string, found {reader.TokenType}");
        }

        var text = reader.GetString();
        return TryParse(text, out var id) ? id : throw new JsonException($"'{text}' is not a valid id");
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(ValueOf(value) ?? throw new JsonException("an empty id cannot be written"));
    }
}

public sealed class WidgetIdJsonConverter : ModuleIdJsonConverter<WidgetId>
{
    protected override bool TryParse(string? text, out WidgetId id) => WidgetId.TryParse(text, out id);

    protected override string? ValueOf(WidgetId id) => id.Value;
}

public sealed class ProviderIdJsonConverter : ModuleIdJsonConverter<ProviderId>
{
    protected override bool TryParse(string? text, out ProviderId id) => ProviderId.TryParse(text, out id);

    protected override string? ValueOf(ProviderId id) => id.Value;
}

public sealed class FeatureIdJsonConverter : ModuleIdJsonConverter<FeatureId>
{
    protected override bool TryParse(string? text, out FeatureId id) => FeatureId.TryParse(text, out id);

    protected override string? ValueOf(FeatureId id) => id.Value;
}

public sealed class CadenceIdJsonConverter : ModuleIdJsonConverter<CadenceId>
{
    protected override bool TryParse(string? text, out CadenceId id) => CadenceId.TryParse(text, out id);

    protected override string? ValueOf(CadenceId id) => id.Value;
}
