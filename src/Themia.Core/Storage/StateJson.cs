using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Themia.Core.Model;
using Themia.Sdk;

namespace Themia.Core.Storage;

/// <summary>
/// [S-6903] The one set of options every persisted state document is read and written with. The
/// host's own metadata (<see cref="AppStateJsonContext"/>) first, then each installed widget's
/// resolver; a contract modifier adds every widget's config to the <see cref="WidgetConfig"/> union
/// with its id as the discriminator, so no list in the host names a widget. Resolver combination,
/// modifiers and <see cref="JsonDerivedType"/> are AOT-safe: nothing here reflects.
/// </summary>
public static class StateJson
{
    private static readonly Lazy<JsonSerializerOptions> Built = new(() => Build(ModuleCatalog.Widgets));

    /// <summary>The options, built once from the installed registry ([S-6902]) on first use.</summary>
    public static JsonSerializerOptions Options => Built.Value;

    public static JsonTypeInfo<AppState> AppState => Info<AppState>();

    public static JsonTypeInfo<WidgetInstance> WidgetInstance => Info<WidgetInstance>();

    public static JsonTypeInfo<WidgetConfig> WidgetConfig => Info<WidgetConfig>();

    public static JsonTypeInfo<DesignState> DesignState => Info<DesignState>();

    public static JsonTypeInfo<AppPreferences> AppPreferences => Info<AppPreferences>();

    /// <summary>The state options over <paramref name="widgets"/>: the union holds exactly their configs.</summary>
    public static JsonSerializerOptions Build(IReadOnlyList<WidgetDescriptor> widgets)
    {
        ArgumentNullException.ThrowIfNull(widgets);

        var resolvers = new List<IJsonTypeInfoResolver> { AppStateJsonContext.Default };
        foreach (var widget in widgets)
        {
            if (!resolvers.Contains(widget.Json))
            {
                resolvers.Add(widget.Json);
            }
        }

        var options = new JsonSerializerOptions(AppStateJsonContext.Default.Options)
        {
            TypeInfoResolver = JsonTypeInfoResolver.Combine([.. resolvers]).WithAddedModifier(info =>
            {
                if (info.Type != typeof(WidgetConfig))
                {
                    return;
                }

                var union = info.PolymorphismOptions
                    ?? throw new InvalidOperationException("WidgetConfig lost its [JsonPolymorphic] discriminator");
                foreach (var widget in widgets)
                {
                    union.DerivedTypes.Add(new JsonDerivedType(widget.ConfigType, widget.Id.Value));
                }
            }),
        };
        options.MakeReadOnly();
        return options;
    }

    private static JsonTypeInfo<T> Info<T>() => (JsonTypeInfo<T>)Options.GetTypeInfo(typeof(T));

    /// <summary>
    /// [S-893] <paramref name="fragment"/>'s properties over <paramref name="defaults"/>, as the state file would hold
    /// them, read back and copied (every id inside new): how host code (the first-run desktop, a debug
    /// scene) gives a widget it names only by id a config other than its default.
    /// </summary>
    public static WidgetConfig Merged(WidgetConfig defaults, string fragment)
    {
        var json = JsonNode.Parse(JsonSerializer.Serialize(defaults, WidgetConfig))?.AsObject()
            ?? throw new InvalidOperationException("a default config serialised to nothing");
        var over = JsonNode.Parse(fragment)?.AsObject()
            ?? throw new InvalidOperationException($"a config fragment is not an object: {fragment}");

        foreach (var (name, value) in over.ToList())
        {
            over.Remove(name);
            json[name] = value;
        }

        var config = JsonSerializer.Deserialize(json.ToJsonString(), WidgetConfig)
            ?? throw new InvalidOperationException("a merged config read back as nothing");
        return config.Clone();
    }
}
