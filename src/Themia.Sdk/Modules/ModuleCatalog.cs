using Themia.Core.Model;

namespace Themia.Sdk;

/// <summary>
/// [S-6902] The widgets compiled into this build, installed once by the host from the generated
/// registry before anything reads a widget (the app in its constructor, the tests in their module
/// initializer). Every per-widget question the host asks goes through it; there is no second list.
/// </summary>
public static class ModuleCatalog
{
    private sealed record Installed(
        IReadOnlyList<WidgetDescriptor> Widgets,
        Dictionary<WidgetId, WidgetDescriptor> ById,
        Dictionary<Type, WidgetDescriptor> ByConfig);

    private static Installed? _installed;

    public static bool IsInstalled => Volatile.Read(ref _installed) is not null;

    /// <summary>Every widget, in the registry's order (by id).</summary>
    public static IReadOnlyList<WidgetDescriptor> Widgets => Current.Widgets;

    /// <summary>
    /// Install the registry. Once per process: a second install is a host bug, refused loudly, as is
    /// a set that <see cref="Check"/> finds wrong.
    /// </summary>
    public static void Install(IEnumerable<WidgetDescriptor> widgets)
    {
        ArgumentNullException.ThrowIfNull(widgets);

        var list = widgets.ToList();
        if (Check(list) is { Count: > 0 } problems)
        {
            throw new InvalidOperationException("the widget registry is inconsistent: " + string.Join("; ", problems));
        }

        var installed = new Installed(
            list.AsReadOnly(),
            list.ToDictionary(w => w.Id),
            list.ToDictionary(w => w.ConfigType));
        if (Interlocked.CompareExchange(ref _installed, installed, null) is not null)
        {
            throw new InvalidOperationException("the widget registry is installed once per process");
        }
    }

    /// <summary>What is wrong with a set of descriptors: a missing one, an empty id, an id or a config type given twice.</summary>
    public static IReadOnlyList<string> Check(IReadOnlyList<WidgetDescriptor?> widgets)
    {
        ArgumentNullException.ThrowIfNull(widgets);

        var problems = new List<string>();
        var ids = new HashSet<WidgetId>();
        var configs = new HashSet<Type>();
        for (var i = 0; i < widgets.Count; i++)
        {
            if (widgets[i] is not { } widget)
            {
                problems.Add($"descriptor {i} is missing");
                continue;
            }

            if (widget.Id.Value is null)
            {
                problems.Add($"descriptor {i} ({widget.ConfigType.Name}) has no id");
                continue;
            }

            if (!ids.Add(widget.Id))
            {
                problems.Add($"widget id '{widget.Id}' is declared twice");
            }

            // The config's type is how a config knows its id: two widgets cannot share one.
            if (!configs.Add(widget.ConfigType))
            {
                problems.Add($"config type {widget.ConfigType.Name} is declared by two widgets");
            }
        }

        return problems;
    }

    /// <summary>The descriptor of an installed id. An id with none is a registration bug, thrown.</summary>
    public static WidgetDescriptor Of(WidgetId id)
        => TryGet(id, out var widget)
            ? widget
            : throw new KeyNotFoundException($"no widget module declares the id '{id}'");

    /// <summary>The descriptor of an id read from outside (a file, a command line), or false.</summary>
    public static bool TryGet(WidgetId id, out WidgetDescriptor widget)
    {
        if (id.Value is not null && Current.ById.TryGetValue(id, out var found))
        {
            widget = found;
            return true;
        }

        widget = null!;
        return false;
    }

    /// <summary>
    /// A widget named on a command line (a debug render's <c>--widget-png Clock</c>): its id in any
    /// case, so the names the flags always took keep working. Only an installed id is found.
    /// </summary>
    public static bool TryFind(string? name, out WidgetDescriptor widget)
    {
        widget = Current.Widgets.FirstOrDefault(w => string.Equals(w.Id.Value, name, StringComparison.OrdinalIgnoreCase))!;
        return widget is not null;
    }

    /// <summary>
    /// [S-6903] The id of a config: the widget whose descriptor names its type. The discriminator
    /// written into state.json is this id, so a config always says which widget it is.
    /// </summary>
    public static WidgetId IdOf(WidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return Current.ByConfig.TryGetValue(config.GetType(), out var widget)
            ? widget.Id
            : throw new KeyNotFoundException($"no widget module declares the config {config.GetType().Name}");
    }

    private static Installed Current
        => Volatile.Read(ref _installed)
            ?? throw new InvalidOperationException("the widget registry is not installed: the host installs it before anything reads a widget ([S-6902])");
}
