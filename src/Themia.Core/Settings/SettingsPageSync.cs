using System.Text;
using System.Text.Json;
using Themia.Core.Model;
using Themia.Core.Storage;

namespace Themia.Core.Settings;

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
    /// [S-1721] Everything the pages show (the design and the preferences), as one comparable string,
    /// and nothing else, so a widget dragged on the desktop or a feed refreshing does not rebuild a
    /// page that shows neither.
    /// </summary>
    public static string Signature(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return new StringBuilder()
            .Append(JsonSerializer.Serialize(state.Design, StateJson.DesignState))
            .Append('\n')
            .Append(JsonSerializer.Serialize(state.Preferences, StateJson.AppPreferences))
            .ToString();
    }
}
