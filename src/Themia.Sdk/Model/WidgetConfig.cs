using System.Text.Json.Serialization;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>
/// Per-instance widget settings. A discriminated union rather than a property bag: the
/// discriminator is written into the JSON so a config can never be silently read as the wrong
/// widget's shape.
/// <para>
/// [S-6903] The union's members are not listed here: each widget's descriptor names its config type
/// and id, and <c>StateJson</c> adds them to the union when it builds the options.
/// </para>
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
public abstract class WidgetConfig
{
    /// <summary>
    /// [S-6903] The widget this config belongs to: its id, the discriminator it is written with.
    /// Sealed here, never declared by a config, so no config can repeat the <c>kind</c> metadata
    /// property or claim another widget's id.
    /// </summary>
    [JsonIgnore]
    public WidgetId Kind => ModuleCatalog.IdOf(this);

    /// <summary>
    /// Default config for a kind: its descriptor's ([S-6902]). Never returns null, so a widget always
    /// has settings; a kind without a descriptor is a registration bug, thrown, never another kind's config.
    /// </summary>
    public static WidgetConfig CreateDefault(WidgetId kind) => ModuleCatalog.Of(kind).CreateDefault();

    public abstract WidgetConfig Clone();

    /// <summary>
    /// Bring a config read from disk back inside its own invariants — clamp ranges, drop values a
    /// hand edit made unusable — and say what had to change. Run once at load by
    /// <c>AppStateStore.Validate</c>, so the repairs are logged with the rest. Most configs have
    /// nothing to repair.
    /// </summary>
    public virtual IReadOnlyList<string> Repair() => [];
}
