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
    /// <summary>The kind this config belongs to. Used to detect a config/kind mismatch on load.</summary>
    [JsonIgnore]
    public abstract WidgetKind Kind { get; }

    /// <summary>
    /// Default config for a kind: its descriptor's ([S-6902]). Never returns null, so a widget always
    /// has settings; a kind without a descriptor is a registration bug, thrown, never another kind's config.
    /// </summary>
    public static WidgetConfig CreateDefault(WidgetKind kind) => ModuleCatalog.Of(kind).CreateDefault();

    public abstract WidgetConfig Clone();

    /// <summary>
    /// Bring a config read from disk back inside its own invariants — clamp ranges, drop values a
    /// hand edit made unusable — and say what had to change. Run once at load by
    /// <c>AppStateStore.Validate</c>, so the repairs are logged with the rest. Most configs have
    /// nothing to repair.
    /// </summary>
    public virtual IReadOnlyList<string> Repair() => [];
}
