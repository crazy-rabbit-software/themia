using System.Text.Json.Serialization.Metadata;
using Themia.Core.Localization;
using Themia.Core.Model;

namespace Themia.Sdk;

/// <summary>
/// [S-6901] Declares a widget module: its id and the class the host creates. Read at build time by
/// the registry generator, never at run time.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class ThemiaWidgetAttribute(string id, Type module) : Attribute
{
    public string Id { get; } = id;

    public Type Module { get; } = module;
}

/// <summary>[S-6901] Declares a provider module (an account or data service).</summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class ThemiaProviderAttribute(string id, Type module) : Attribute
{
    public string Id { get; } = id;

    public Type Module { get; } = module;
}

/// <summary>
/// [S-6902] What the host knows of a widget without its UI: one record per widget, in place of the
/// per-kind tables the host used to keep (docs/WIDGET-KIT.md section 4.3). A member is added here
/// when a table moves onto it, and the table goes in the same commit.
/// </summary>
public sealed record WidgetDescriptor
{
    /// <summary>The widget's id ([S-6900]): its config's discriminator in state.json.</summary>
    public required WidgetId Id { get; init; }

    /// <summary>The enum key the host still uses while <see cref="WidgetKind"/> exists (M2's interim).</summary>
    public required WidgetKind Kind { get; init; }

    /// <summary>[S-6903] Its config's type: the member of the config union written with <see cref="Id"/> as its discriminator.</summary>
    public required Type ConfigType { get; init; }

    /// <summary>A new config with the widget's defaults; always a <see cref="ConfigType"/>.</summary>
    public required Func<WidgetConfig> CreateDefault { get; init; }

    /// <summary>[S-6903] The source-generated JSON metadata that knows <see cref="ConfigType"/> (AOT: no reflection).</summary>
    public required IJsonTypeInfoResolver Json { get; init; }

    /// <summary>Its name, as a string id: <see cref="Label"/> reads it in the current language ([S-1109]).</summary>
    public required Str LabelId { get; init; }

    /// <summary>The picker's one line under the name.</summary>
    public required Str DescriptionId { get; init; }

    /// <summary>[S-1115] The facts its description's placeholders take ("15-minute steps"), written in the language's digits.</summary>
    public IReadOnlyList<StringArg> DescriptionFacts { get; init; } = [];

    /// <summary>Segoe Fluent Icons code point: the picker, the Set up prompt, the settings search.</summary>
    public required string Glyph { get; init; }

    /// <summary>[S-10] Default size in DIPs, made a fraction of the display when it lands.</summary>
    public required double DefaultWidth { get; init; }

    public required double DefaultHeight { get; init; }

    /// <summary>The name in the current language, read each time so a language switch reaches every caller.</summary>
    public string Label => LabelId.Text();

    /// <summary>The description in the current language.</summary>
    public string Description => Describe(Strings.Current);

    /// <summary>The description in <paramref name="locale"/>, its facts filled.</summary>
    public string Describe(Locale locale) => Strings.Format(locale, DescriptionId, [.. DescriptionFacts]);
}
