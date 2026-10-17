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
}
