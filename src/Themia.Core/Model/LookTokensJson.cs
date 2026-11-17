using System.Text.Json;
using System.Text.Json.Serialization;
using Themia.Core.Diagnostics;
using Themia.Sdk.Kit;

namespace Themia.Core.Model;

/// <summary>
/// [S-2130] The look tokens in the state file: only the tokens that differ from a fresh set's, each by its own key
/// (<see cref="LookTokens.Numbers"/>' keys, the enums by their names), so a file names what its look changed and a
/// token left out is its default. Hand-written for Native AOT (no reflection). A value of the wrong kind or an enum
/// name that is not one is its default, logged; a number out of range is read as written and held to it by load validation
/// (<see cref="LookTokens.Repair"/>). A key it does not know is skipped: an older or newer token, never a reason to
/// lose the rest.
/// </summary>
public sealed class LookTokensJson : JsonConverter<LookTokens>
{
    private static readonly LookTokens Fresh = new();

    public override LookTokens Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("the look tokens are an object");
        }

        var tokens = new LookTokens();
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                throw new JsonException("a look token is a name and a value");
            }

            var key = reader.GetString() ?? string.Empty;
            reader.Read();
            if (LookTokens.Number(key) is { } number)
            {
                if (reader.TokenType == JsonTokenType.Number)
                {
                    number.Set(tokens, reader.GetDouble());
                }
                else
                {
                    Log.Warn(Scope, $"look token '{key}' is not a number; using its default");
                    reader.Skip();
                }

                continue;
            }

            switch (key)
            {
                case "rendering": tokens.Rendering = Named(ref reader, key, tokens.Rendering); break;
                case "headerWeight": tokens.HeaderWeight = Named(ref reader, key, tokens.HeaderWeight); break;
                case "readoutWeight": tokens.ReadoutWeight = Named(ref reader, key, tokens.ReadoutWeight); break;
                case "bodyWeight": tokens.BodyWeight = Named(ref reader, key, tokens.BodyWeight); break;
                case "innerSurface": tokens.InnerSurface = Named(ref reader, key, tokens.InnerSurface); break;
                case "rowDividers": tokens.RowDividers = Named(ref reader, key, tokens.RowDividers); break;
                case "tabularNumerals":
                    if (reader.TokenType is JsonTokenType.True or JsonTokenType.False)
                    {
                        tokens.TabularNumerals = reader.GetBoolean();
                    }
                    else
                    {
                        Log.Warn(Scope, "look token 'tabularNumerals' is not true or false; using its default");
                        reader.Skip();
                    }

                    break;
                default:
                    reader.Skip(); // ignore: a token this build does not know
                    break;
            }
        }

        return tokens;
    }

    public override void Write(Utf8JsonWriter writer, LookTokens value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);
        writer.WriteStartObject();
        foreach (var number in LookTokens.Numbers)
        {
            if (Math.Abs(number.Get(value) - number.Default) >= 0.0005)
            {
                writer.WriteNumber(number.Key, number.Get(value));
            }
        }

        void Enum<T>(string key, T now, T fresh)
            where T : struct, System.Enum
        {
            if (!EqualityComparer<T>.Default.Equals(now, fresh))
            {
                writer.WriteString(key, now.ToString());
            }
        }

        Enum("rendering", value.Rendering, Fresh.Rendering);
        Enum("headerWeight", value.HeaderWeight, Fresh.HeaderWeight);
        Enum("readoutWeight", value.ReadoutWeight, Fresh.ReadoutWeight);
        Enum("bodyWeight", value.BodyWeight, Fresh.BodyWeight);
        Enum("innerSurface", value.InnerSurface, Fresh.InnerSurface);
        Enum("rowDividers", value.RowDividers, Fresh.RowDividers);
        if (value.TabularNumerals != Fresh.TabularNumerals)
        {
            writer.WriteBoolean("tabularNumerals", value.TabularNumerals);
        }

        writer.WriteEndObject();
    }

    private const string Scope = "design";

    /// <summary>An enum token by its name; anything else keeps <paramref name="fallback"/> (the default), loudly.</summary>
    private static T Named<T>(ref Utf8JsonReader reader, string key, T fallback)
        where T : struct, Enum
    {
        if (reader.TokenType == JsonTokenType.String && reader.GetString() is { } name
            && !int.TryParse(name, out _) && Enum.TryParse<T>(name, ignoreCase: false, out var value) && Enum.IsDefined(value))
        {
            return value;
        }

        Log.Warn(Scope, $"look token '{key}' is not one of its names; using its default");
        reader.Skip();
        return fallback;
    }
}
