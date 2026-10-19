using Themia.Core.Model;

namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-1403] [S-1411] App layouts as a widget offers them (<see cref="IWidgetHost.AppLayouts"/>, and
/// <see cref="SettingsHost.AppLayouts"/> for a menu line): open the host's one layout editor on an
/// item, and launch a layout (the apps started and their windows put in their panes). The editor
/// window and the window placement stay host code; the host finds the item through
/// <see cref="IHoldsAppLayouts"/> on the configs.
/// </summary>
public interface IAppLayouts
{
    /// <summary>Whether the editor can be opened here (false in a debug render, which has none).</summary>
    bool CanEdit { get; }

    /// <summary>Open the editor on the layout item <paramref name="itemId"/>, or bring it forward on that item.</summary>
    void Edit(string itemId);

    /// <summary>[S-1407] Launch <paramref name="layout"/> on the display <paramref name="anchor"/> (the widget's window) is on: every pane's app opened and put in its pane. An empty layout does nothing.</summary>
    void Launch(LayoutNode? layout, nint anchor);
}
