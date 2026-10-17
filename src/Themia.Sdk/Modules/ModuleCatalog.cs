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
        Dictionary<WidgetKind, WidgetDescriptor> ByKind);

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
            list.ToDictionary(w => w.Kind));
        if (Interlocked.CompareExchange(ref _installed, installed, null) is not null)
        {
            throw new InvalidOperationException("the widget registry is installed once per process");
        }
    }

    /// <summary>What is wrong with a set of descriptors: a missing one, an empty id, an id or a kind given twice.</summary>
    public static IReadOnlyList<string> Check(IReadOnlyList<WidgetDescriptor?> widgets)
    {
        ArgumentNullException.ThrowIfNull(widgets);

        var problems = new List<string>();
        var ids = new HashSet<WidgetId>();
        var kinds = new HashSet<WidgetKind>();
        for (var i = 0; i < widgets.Count; i++)
        {
            if (widgets[i] is not { } widget)
            {
                problems.Add($"descriptor {i} is missing");
                continue;
            }

            if (widget.Id.Value is null)
            {
                problems.Add($"descriptor {i} ({widget.Kind}) has no id");
                continue;
            }

            if (!ids.Add(widget.Id))
            {
                problems.Add($"widget id '{widget.Id}' is declared twice");
            }

            if (!kinds.Add(widget.Kind))
            {
                problems.Add($"widget kind {widget.Kind} is declared twice");
            }
        }

        return problems;
    }

    /// <summary>The descriptor of an installed kind. A kind with none is a registration bug, thrown.</summary>
    public static WidgetDescriptor Of(WidgetKind kind)
        => Current.ByKind.TryGetValue(kind, out var widget)
            ? widget
            : throw new KeyNotFoundException($"no widget module declares the kind {kind}");

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

    private static Installed Current
        => Volatile.Read(ref _installed)
            ?? throw new InvalidOperationException("the widget registry is not installed: the host installs it before anything reads a widget ([S-6902])");
}
