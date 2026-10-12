using Themia.Core.Model;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-1458] Whether the app keeps a clipboard history, and how long: while any view on any display
/// holds a Clipboard widget, whether or not it is on screen, so the history is there when a view
/// with one is switched to (user requirement). The longest capacity any of them asks for, so each
/// shows as many as its own setting says. None at all, no history and no listener: the clipboard is
/// not read for a widget nobody placed.
/// </summary>
public static class ClipboardInterest
{
    /// <summary>The entries to keep, or null when no view holds a Clipboard widget.</summary>
    public static int? Capacity(AppState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        int? capacity = null;
        foreach (var widget in state.Views.SelectMany(view => view.Widgets))
        {
            if (widget.Config is ClipboardConfig config)
            {
                capacity = Math.Max(capacity ?? 0, config.EffectiveCapacity);
            }
        }

        return capacity;
    }
}
