using Microsoft.UI.Xaml;
using Themia.Core.Geometry;
using Themia.Core.Shell;
using Themia.Sdk;

namespace Themia.App.Shell;

/// <summary>[S-2320] The edit bar along the top centre of the surface in edit mode.</summary>
internal sealed partial class DesktopWindow
{
    private EditBar BuildToolbar() => new(ToolbarCommands()) { Visibility = Visibility.Collapsed };

    /// <summary>What this display's edit bar does: everything goes through the host, for this display.</summary>
    private EditBarCommands ToolbarCommands() => new(
        Add: ToggleAddPage,
        Page: TogglePage,
        Selection: ToggleSelectionPage,
        IntoFlyout: FocusFlyout,
        Done: () => _host.SetEditMode(false));

    /// <summary>
    /// [S-2320] What this display's bar shows now: the selection's entry, the problems (Themia keeps no
    /// standing list of them yet: the room is kept, empty), the open entry, the room the work area gives.
    /// </summary>
    private EditBarState ToolbarState()
    {
        // [S-2341] [S-11754] The selection's entry: a group's name and glyph, else the kind's or the count.
        BarSelection? selection = null;
        if (_selectionModel is { } model && SelectionPage.Words(model, id => ModuleCatalog.Of(id).Name) is { } words)
        {
            selection = new BarSelection(words, SelectionPage.Glyph(model));
        }

        var metrics = Metrics;
        var widest = metrics.IsUsable ? GridGeometry.AllowedPixels(metrics).Width - (2 * Sdk.Kit.ShellMetrics.Bar.Top) : double.PositiveInfinity;
        return new EditBarState(selection, 0, _openPage, _selectionOpen, widest) { AddOpen = _addPage is not null };
    }

    /// <summary>
    /// [S-2320] The bar follows a change made elsewhere (an undo, an import, another display), the
    /// selection's entry the selection as it is now, and the open flyout the state.
    /// </summary>
    private void SyncToolbar()
    {
        SyncProperties();
        ShowToolbar();
        SyncBarMenu();
    }

    /// <summary>[S-2320] The bar's entries as they are now; nothing while it is not built.</summary>
    private void ShowToolbar()
    {
        if (!_closed && _toolbar.Built)
        {
            _toolbar.Show(ToolbarState());
        }
    }

    /// <summary>[S-885] What Undo and Redo can step changed: the bar has no buttons for them (the keys do it).</summary>
    private void OnHistoryChanged()
    {
        // ignore: the bar shows no history; Ctrl+Z, Ctrl+Y and Ctrl+Shift+Z step it.
    }
}
