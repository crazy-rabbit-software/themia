using System.Text.Json;
using Themia.Core.Diagnostics;
using Themia.Core.Model;
using Themia.Sdk;

namespace Themia.Core.Storage;

/// <summary>
/// [S-15660] The live state as a write left it, in the parts a change is told by: each widget's rect, own look, config
/// object and config document (and a fence's items), each view's name, icon, colour, desktop icons and widgets' order,
/// which view each display shows, the design's document and the preferences'. <see cref="Since"/> compares two: what
/// the writes between them changed (<see cref="StateChange"/>). Taken on the thread that owns the state, after every
/// write (<see cref="StateWriter"/>); a write made in place between two writes (a widget's page writing into its config)
/// is told by the next.
/// </summary>
public sealed class StateMarks
{
    private const string Scope = "store";

    private readonly Dictionary<string, WidgetMark> _widgets;
    private readonly List<string> _views;
    private readonly Dictionary<string, string> _shown;
    private readonly string? _active;
    private readonly byte[]? _design;
    private readonly byte[]? _preferences;

    private StateMarks(Dictionary<string, WidgetMark> widgets, List<string> views, Dictionary<string, string> shown, string? active, byte[]? design, byte[]? preferences)
    {
        _widgets = widgets;
        _views = views;
        _shown = shown;
        _active = active;
        _design = design;
        _preferences = preferences;
    }

    /// <summary>One widget as a write left it.</summary>
    private sealed record WidgetMark(WidgetInstance Look, WidgetConfig Config, byte[]? Json, string[]? Held);

    /// <summary>The state's marks now: call it on the thread that owns <paramref name="state"/>.</summary>
    public static StateMarks Of(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var widgets = new Dictionary<string, WidgetMark>(StringComparer.Ordinal);
        var views = new List<string>(state.Views.Count);
        foreach (var view in state.Views)
        {
            var ids = new List<string>(view.Widgets.Count);
            foreach (var widget in view.Widgets)
            {
                ids.Add(widget.Id);
                if (widgets.ContainsKey(widget.Id))
                {
                    continue;
                }

                var look = new WidgetInstance { Id = widget.Id, Config = widget.Config };
                look.CopyArrangementFrom(widget);
                widgets[widget.Id] = new WidgetMark(look, widget.Config, Json(widget.Config, StateJson.WidgetConfig), widget.Config is IHoldsDesktopItems fence ? [.. fence.Items] : null);
            }

            views.Add($"{view.Id}\u001f{view.Name}\u001f{view.Icon}\u001f{view.Color}\u001f{view.DesktopIcons}\u001f{string.Join('\u001f', ids)}");
        }

        return new StateMarks(
            widgets,
            views,
            new Dictionary<string, string>(state.MonitorViews, StringComparer.Ordinal),
            state.ActiveViewId,
            Json(state.Design, StateJson.DesignState),
            Json(state.Preferences, StateJson.AppPreferences));
    }

    /// <summary>
    /// [S-15660] What changed from <paramref name="before"/> (null: nothing known, so everything) to these marks. A part
    /// that could not be written as a document counts as changed.
    /// </summary>
    public StateChange Since(StateMarks? before)
    {
        if (before is null)
        {
            return StateChange.All;
        }

        var configs = new HashSet<string>(StringComparer.Ordinal);
        var moved = new HashSet<string>(StringComparer.Ordinal);
        var arranged = false;
        foreach (var (id, now) in _widgets)
        {
            if (!before._widgets.TryGetValue(id, out var was))
            {
                configs.Add(id);
                arranged = true;
                continue;
            }

            if (now.Look.Rect != was.Look.Rect)
            {
                moved.Add(id);
                arranged = true;
            }

            // [S-886] A config replaced by another object is arrangement too: the history holds configs by reference.
            if (!now.Look.SameLook(was.Look) || !ReferenceEquals(now.Config, was.Config))
            {
                configs.Add(id);
                arranged = true;
            }
            else if (!Same(now.Json, was.Json))
            {
                configs.Add(id);
            }

            // [S-13010] A fence's items are the arrangement.
            if (!SameItems(now.Held, was.Held))
            {
                arranged = true;
            }
        }

        // A widget gone (removed, cut).
        arranged |= Gone(before);

        arranged |= !_views.SequenceEqual(before._views, StringComparer.Ordinal);

        // Another view shown on a display (or the active one): the surfaces draw what they show whole.
        var whole = !string.Equals(_active, before._active, StringComparison.Ordinal)
            || _shown.Count != before._shown.Count
            || _shown.Any(kv => !before._shown.TryGetValue(kv.Key, out var was) || !string.Equals(was, kv.Value, StringComparison.Ordinal));

        return new StateChange(
            configs,
            moved,
            Design: !Same(_design, before._design),
            Preferences: !Same(_preferences, before._preferences),
            Whole: whole,
            Arranged: arranged);
    }

    /// <summary>Whether a widget of <paramref name="before"/> is gone from these marks.</summary>
    private bool Gone(StateMarks before)
    {
        foreach (var id in before._widgets.Keys)
        {
            if (!_widgets.ContainsKey(id))
            {
                return true;
            }
        }

        return false;
    }

    private static bool SameItems(string[]? a, string[]? b)
        => a is null ? b is null : b is not null && a.AsSpan().SequenceEqual(b, StringComparer.Ordinal);

    private static bool Same(byte[]? a, byte[]? b) => a is not null && b is not null && a.AsSpan().SequenceEqual(b);

    /// <summary>The document of <paramref name="value"/>, or null when it cannot be written (counted as changed, said in the log).</summary>
    private static byte[]? Json<T>(T value, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> info)
    {
        try
        {
            return JsonSerializer.SerializeToUtf8Bytes(value, info);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException or ArgumentException)
        {
            Log.Warn(Scope, $"a {typeof(T).Name} could not be written to tell what changed; it counts as changed: {ex.Message}");
            return null;
        }
    }
}
