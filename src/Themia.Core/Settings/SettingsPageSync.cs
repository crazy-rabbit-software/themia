using System.Text;
using System.Text.Json;
using Themia.Core.Model;
using Themia.Core.Storage;

namespace Themia.Core.Settings;

/// <summary>
/// What a settings page edits: the app's own options, every view (the Views page), every connected
/// display (the Displays page) or one widget.
/// </summary>
public enum SettingsScope
{
    App,
    Views,
    Displays,
    Widget,
}

/// <summary>
/// [S-1720] Keeping the open settings page in step with changes made somewhere else — the edit
/// toolbar's snap switch, a keyboard shortcut, the desktop's own menus.
/// <para>
/// A page's controls are built once from the state and then only write. Rather than teach every
/// control to follow the state, the window compares what the page was built from (its
/// <see cref="Signature"/>) with the state now, and builds the page again when they differ. The
/// page is never rebuilt while its window is the active one: then the change is the user's own,
/// made with the page's controls, which already show it — and rebuilding would tear the control
/// down under the pointer or take the focus out of a box being typed in.
/// </para>
/// </summary>
public static class SettingsPageSync
{
    /// <summary>
    /// [S-1720] Whether the page on screen has to be built again. <paramref name="shown"/> is the
    /// signature it was built from (or caught up to when its window last lost the focus); null while
    /// there is no page yet.
    /// </summary>
    public static bool NeedsRebuild(bool windowActive, string? shown, string current)
        => !windowActive && shown is not null && !string.Equals(shown, current, StringComparison.Ordinal);

    /// <summary>
    /// [S-1721] Everything a page of <paramref name="scope"/> shows, as one comparable string — and
    /// nothing else, so a widget dragged on the desktop or a feed refreshing does not rebuild a page
    /// that shows neither. <paramref name="id"/> is the widget id (<see cref="SettingsScope.Widget"/>
    /// only); <paramref name="displays"/> the connected displays' ids, in the order the Displays page
    /// lists them (<see cref="SettingsScope.Displays"/> only). A widget that no longer exists has a
    /// signature of its own.
    /// </summary>
    public static string Signature(AppState state, SettingsScope scope, string? id = null, IReadOnlyList<string>? displays = null)
    {
        ArgumentNullException.ThrowIfNull(state);

        return scope switch
        {
            SettingsScope.App => AppSignature(state),
            SettingsScope.Views => string.Join("\n--\n", state.Views.Select(v => v.Id + "\n" + ViewSignature(state, v.Id))),
            SettingsScope.Displays => string.Join("\n--\n", (displays ?? []).Select(d => d + "\n" + DisplaySignature(state, d))),
            SettingsScope.Widget => WidgetSignature(state, id),
            _ => string.Empty,
        };
    }

    /// <summary>
    /// [S-1834] The shape of a list page (Displays, Views): which groups it has, in which order, and
    /// what decides the state of one group's controls from another's. When it changes the page is
    /// built again at once, even while its window is active — the change came from the page itself
    /// (Add view, Duplicate, Delete, Move up, a drag, switching a display off), and the page has to
    /// show the group it added or drop the one it deleted. A rename or a new icon keeps the shape:
    /// those update their own group in place, so the box being typed in is never torn down.
    /// Empty for every other scope, which never rebuilds this way.
    /// </summary>
    public static string Shape(AppState state, SettingsScope scope, IReadOnlyList<string>? displays = null)
    {
        ArgumentNullException.ThrowIfNull(state);

        return scope switch
        {
            SettingsScope.Views => string.Join(",", state.Views.Select(v => v.Id)),

            // Whether each display is on: the last display on cannot be switched off ([S-845]), so
            // switching one changes whether the others' switches can move.
            SettingsScope.Displays => string.Join(",", (displays ?? []).Select(d => $"{d}={state.DisabledMonitors.Contains(d)}")),
            _ => string.Empty,
        };
    }

    /// <summary>The design, the preferences, and the views by name (General offers them for new displays).</summary>
    private static string AppSignature(AppState state)
    {
        var text = new StringBuilder()
            .Append(JsonSerializer.Serialize(state.Design, AppStateJsonContext.Default.DesignState))
            .Append('\n')
            .Append(JsonSerializer.Serialize(state.Preferences, AppStateJsonContext.Default.AppPreferences))
            .Append('\n')
            .Append(state.DefaultViewForNewDisplays);

        foreach (var view in state.Views)
        {
            text.Append('\n').Append(view.Id).Append('=').Append(view.Name);
        }

        return text.ToString();
    }

    /// <summary>The view's name and icon, its widgets by kind and label (not where they sit), and the displays showing it.</summary>
    private static string ViewSignature(AppState state, string? id)
    {
        if (state.FindView(id) is not { } view)
        {
            return "-";
        }

        var text = new StringBuilder().Append(view.Name).Append('\n').Append(view.Icon).Append('\n').Append(view.Color);
        foreach (var widget in view.Widgets)
        {
            text.Append('\n').Append(widget.Id).Append('=').Append(widget.Kind).Append('=').Append(widget.Label);
        }

        foreach (var (monitor, shown) in state.MonitorViews.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            if (shown == view.Id)
            {
                text.Append("\non ").Append(monitor);
            }
        }

        return text.Append("\nnew ").Append(state.DefaultViewForNewDisplays == view.Id).ToString();
    }

    /// <summary>The display's view, whether Themia is on there, its scale and view bar, and the views it can pick from.</summary>
    private static string DisplaySignature(AppState state, string? id)
    {
        if (id is null)
        {
            return "-";
        }

        var text = new StringBuilder()
            .Append(state.MonitorViews.GetValueOrDefault(id)).Append('\n')
            .Append(state.DisabledMonitors.Contains(id)).Append('\n')
            .Append(state.MonitorScales.TryGetValue(id, out var scale) ? scale.ToString("R", System.Globalization.CultureInfo.InvariantCulture) : "1").Append('\n');

        if (state.ViewBars.TryGetValue(id, out var bar))
        {
            text.Append(bar.Anchor).Append('|').Append(bar.Size).Append('|').Append(bar.IconOnly).Append('|').Append(bar.Hidden)
                .Append('|').Append(string.Join(",", bar.VisibleViews));
        }

        // [S-3812] The Shows drop-down draws each view's icon in its colour; [S-3832] the page also
        // offers the view new displays start on.
        foreach (var view in state.Views)
        {
            text.Append('\n').Append(view.Id).Append('=').Append(view.Name).Append('=').Append(view.Icon).Append('=').Append(view.Color);
        }

        return text.Append("\nnew ").Append(state.DefaultViewForNewDisplays).ToString();
    }

    /// <summary>The widget's config, label, surface and header — not its position or size.</summary>
    private static string WidgetSignature(AppState state, string? id)
    {
        foreach (var view in state.Views)
        {
            foreach (var widget in view.Widgets)
            {
                if (widget.Id == id)
                {
                    return new StringBuilder()
                        .Append(widget.Label).Append('\n')
                        .Append(widget.Surface).Append('\n')
                        .Append(widget.Header).Append('\n')
                        .Append(JsonSerializer.Serialize(widget.Config, AppStateJsonContext.Default.WidgetConfig))
                        .ToString();
                }
            }
        }

        return "-";
    }
}
