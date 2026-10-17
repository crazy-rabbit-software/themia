using System.Text.Json;
using System.Text.Json.Serialization;
using Themia.Core.Diagnostics;

namespace Themia.Core.Office;

/// <summary>
/// [S-563] Converters that read the calendar and contacts configs leniently: a value of the wrong
/// type or an unknown name becomes the default and is logged, because a throw inside one widget's
/// config refuses the whole state file and blanks every desktop. Written exactly as the strict
/// converters would, so a file this build wrote reads the same anywhere.
/// </summary>
internal static class LenientJson
{
    public const string Scope = "office-config";

    public static void Unknown(string what, string? value) => Log.Warn(Scope, $"unknown {what} '{value}' in a widget's settings; using the default");
}

/// <summary>An enum by name, case-insensitive; anything else is the enum's zero value.</summary>
public sealed class LenientEnumConverter<T> : JsonConverter<T>
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

            LenientJson.Unknown(typeof(T).Name, wire);
            return default;
        }

        var token = reader.TokenType;
        reader.Skip();
        LenientJson.Unknown(typeof(T).Name, token.ToString());
        return default;
    }

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        var name = value.ToString();
        writer.WriteStringValue(char.ToLowerInvariant(name[0]) + name[1..]);
    }
}

/// <summary>A list of ids: a non-array is empty, a non-string item is dropped.</summary>
public sealed class LenientIdListConverter : JsonConverter<List<string>>
{
    public override List<string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => ReadList(ref reader, "id list");

    public override void Write(Utf8JsonWriter writer, List<string> value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStartArray();
        foreach (var item in value ?? [])
        {
            writer.WriteStringValue(item);
        }

        writer.WriteEndArray();
    }

    internal static List<string> ReadList(ref Utf8JsonReader reader, string what)
    {
        var list = new List<string>();

        if (reader.TokenType != JsonTokenType.StartArray)
        {
            var token = reader.TokenType;
            reader.Skip();
            if (token != JsonTokenType.Null)
            {
                LenientJson.Unknown(what, token.ToString());
            }

            return list;
        }

        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (reader.TokenType == JsonTokenType.String && reader.GetString() is { } value)
            {
                list.Add(value);
                continue;
            }

            var token = reader.TokenType;
            reader.Skip();
            LenientJson.Unknown(what + " item", token.ToString());
        }

        return list;
    }
}

/// <summary>A list of enum names: a non-array is empty, an unknown name is dropped.</summary>
public sealed class LenientEnumListConverter<T> : JsonConverter<List<T>>
    where T : struct, Enum
{
    public override List<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var list = new List<T>();
        foreach (var name in LenientIdListConverter.ReadList(ref reader, typeof(T).Name + " list"))
        {
            if (name.Length > 0 && char.IsAsciiLetter(name[0]) && Enum.TryParse<T>(name, ignoreCase: true, out var value) && Enum.IsDefined(value))
            {
                list.Add(value);
            }
            else
            {
                LenientJson.Unknown(typeof(T).Name, name);
            }
        }

        return list;
    }

    public override void Write(Utf8JsonWriter writer, List<T> value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStartArray();
        foreach (var item in value ?? [])
        {
            var name = item.ToString();
            writer.WriteStringValue(char.ToLowerInvariant(name[0]) + name[1..]);
        }

        writer.WriteEndArray();
    }
}

/// <summary>Account id → list of calendar ids; a wrong shape anywhere drops that entry.</summary>
public sealed class LenientCalendarMapConverter : JsonConverter<Dictionary<string, List<string>>>
{
    public override Dictionary<string, List<string>> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var map = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            var token = reader.TokenType;
            reader.Skip();
            LenientJson.Unknown("calendar selection", token.ToString());
            return map;
        }

        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            var key = reader.GetString() ?? string.Empty;
            reader.Read();
            map[key] = LenientIdListConverter.ReadList(ref reader, "calendar list");
        }

        return map;
    }

    public override void Write(Utf8JsonWriter writer, Dictionary<string, List<string>> value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStartObject();
        foreach (var (key, list) in value ?? [])
        {
            writer.WritePropertyName(key);
            writer.WriteStartArray();
            foreach (var id in list)
            {
                writer.WriteStringValue(id);
            }

            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }
}

/// <summary>Key → string; a non-string value drops that entry.</summary>
public sealed class LenientStringMapConverter : JsonConverter<Dictionary<string, string>>
{
    public override Dictionary<string, string> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            var token = reader.TokenType;
            reader.Skip();
            LenientJson.Unknown("colour map", token.ToString());
            return map;
        }

        while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
        {
            var key = reader.GetString() ?? string.Empty;
            reader.Read();

            if (reader.TokenType == JsonTokenType.String && reader.GetString() is { } value)
            {
                map[key] = value;
                continue;
            }

            var token = reader.TokenType;
            reader.Skip();
            LenientJson.Unknown("colour", token.ToString());
        }

        return map;
    }

    public override void Write(Utf8JsonWriter writer, Dictionary<string, string> value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStartObject();
        foreach (var (key, color) in value ?? [])
        {
            writer.WriteString(key, color);
        }

        writer.WriteEndObject();
    }
}
