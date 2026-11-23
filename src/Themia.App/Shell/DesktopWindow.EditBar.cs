using Microsoft.UI.Xaml;
using Themia.Core.Geometry;
using Themia.Core.Shell;
using Themia.Sdk;

namespace Themia.App.Shell;

/// <summary>[S-2320] The edit bar along the top centre of the surface in edit mode.</summary>
internal sealed partial class DesktopWindow
{
    private EditBar BuildEditBar() => new(BarCommands()) { Visibility = Visibility.Collapsed };

    /// <summary>What this display's edit bar does: everything goes through the host, for this display.</summary>
    private EditBarCommands BarCommands() => new(
        Add: ToggleWidgetGallery,
        Page: TogglePage,
        Selection: ToggleSelectionPage,
        IntoFlyout: FocusFlyout,
        Done: () => _host.SetEditMode(false));

    /// <summary>
    /// [S-2320] What this display's bar shows now: the selection's entry, the problems (Themia keeps no
    /// standing list of them yet: the room is kept, empty), the open entry, the room the work area gives.
    /// </summary>
    private EditBarState BarState()
    {
        // [S-2341] The selection's entry: the kind's name and glyph, or the count.
        BarSelection? selection = null;
        if (_selectionModel is { } model && SelectionPage.Words(model, id => ModuleCatalog.Of(id).Name) is { } words)
        {
            selection = new BarSelection(words, SelectionPage.Glyph(model));
        }

        var metrics = Metrics;
        var widest = metrics.IsUsable ? WorkArea.Width - (2 * Sdk.Kit.ShellMetrics.Bar.Top) : double.PositiveInfinity;
        return new EditBarState(selection, 0, _open.PageId, _open.IsSelection, widest)
        {
            AddOpen = _open.IsGallery,
        };
    }

    /// <summary>
    /// [S-2320] The bar follows a change made elsewhere (an undo, an import, another display), the
    /// selection's entry the selection as it is now, and the open flyout the state.
    /// </summary>
    private void SyncEditBar() => Step(() =>
    {
        SyncProperties();
        ShowEditBar();
        SyncBarMenu();

        // [S-13706] [S-16727] The gallery's Not on view tab follows the state too.
        _widgetGallery?.SyncUnplaced();
    });

    /// <summary>[S-2320] The bar's entries as they are now; nothing while it is not built.</summary>
    private void ShowEditBar()
    {
        // [S-2320] Within a step the bar shows once, as the step ends.
        if (_barHeld > 0)
        {
            _barAsked = true;
            return;
        }

        if (!_closed && _editBar.Built)
        {
            _editBar.Show(BarState());
        }
    }
}
