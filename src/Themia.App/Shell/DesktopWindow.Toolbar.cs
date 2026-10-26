using Microsoft.UI.Xaml;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Settings;
using Themia.Core.Shell;
using Themia.Sdk;

namespace Themia.App.Shell;

/// <summary>[S-3500] The edit bar along the top centre of the surface in edit mode.</summary>
internal sealed partial class DesktopWindow
{
    /// <summary>
    /// [S-3508] The selection's properties menu put away by the widget crumb: it stays away (the
    /// selection kept) until the crumb brings it back or the selection changes.
    /// </summary>
    private bool _propertiesDismissed;

    private EditBar BuildToolbar() => new(ToolbarCommands()) { Visibility = Visibility.Collapsed };

    /// <summary>What this display's edit bar does: everything goes through the host, for this display.</summary>
    private EditBarCommands ToolbarCommands() => new(
        AddFlyout: BuildAddFlyout,
        Menu: ToggleBarMenu,
        Widget: ToggleProperties,
        MenuOpening: () => CloseBarMenu(focusButton: false),
        Undo: _host.Undo,
        Redo: _host.Redo,
        SetSnap: on => _host.Mutate(state => EditBarSnap.Set(state, on)),
        Done: () => _host.SetEditMode(false));

    /// <summary>
    /// [S-3507] [S-3509] [S-3516] What this display's bar shows now: this display and the view it
    /// shows, the selection's widget (or how many), whether its menu stands beside it, what Undo and
    /// Redo can step, and snap.
    /// </summary>
    private EditBarState ToolbarState()
    {
        var state = _host.State;
        var view = state.FindView(CurrentViewId);
        var offer = _properties.Offer;
        BarCrumb? widget = null;
        if (offer.Shows)
        {
            widget = offer is { TitleKind: { } kind }
                ? new BarCrumb(string.IsNullOrWhiteSpace(offer.Label) ? ModuleCatalog.Of(kind).Name : offer.Label!, ModuleCatalog.Of(kind).Glyph)
                : new BarCrumb(Str.Shell_Views_WidgetCount.Plural(offer.Count), Glyphs.Apps);
        }

        return new EditBarState(
            Display: new BarCrumb(_monitor.Name, Glyphs.Display, Detail: _monitor.Describe()),
            View: new BarCrumb(view?.Name ?? string.Empty, string.IsNullOrEmpty(view?.Icon) ? Glyphs.View : view!.Icon!, view?.Color),
            Widget: widget,
            WidgetOpen: PropertiesShown,
            CanUndo: _host.CanUndo,
            CanRedo: _host.CanRedo,
            SnapOn: Design.GridSnap);
    }

    /// <summary>
    /// [S-3516] The bar follows a change made elsewhere (an undo, an import, another display),
    /// [S-3701] the properties menu the selection's widgets as they are now, and [S-3531] the bar's
    /// open menu the state.
    /// </summary>
    private void SyncToolbar()
    {
        SyncProperties();
        ShowToolbar();
        SyncBarMenu();
    }

    /// <summary>[S-3507] [S-3509] The bar's trail, Undo, Redo and Snap as they are now; nothing while it is not built.</summary>
    private void ShowToolbar()
    {
        if (!_closed && _toolbar.Built)
        {
            _toolbar.Show(ToolbarState());
        }
    }

    /// <summary>[S-3509] What Undo and Redo can step changed: only the bar follows.</summary>
    private void OnHistoryChanged() => ShowToolbar();

    /// <summary>
    /// [S-3508] The widget crumb: with one of the bar's menus open it closes, and the selection's
    /// properties menu stands beside the selection again; otherwise the menu is put away (the
    /// selection kept) or brought back.
    /// </summary>
    private void ToggleProperties()
    {
        if (BarMenuOpen)
        {
            _propertiesDismissed = false;
            CloseBarMenu(focusButton: false);
        }
        else
        {
            _propertiesDismissed = !_propertiesDismissed;
            PlaceProperties();
        }

        ShowToolbar();
        if (PropertiesShown && !_properties.FocusFirst())
        {
            Core.Diagnostics.Log.Debug(Scope, "the properties menu came back with nothing to focus");
        }
    }

    /// <summary>[S-3508] Whether the properties menu stands beside the selection now.</summary>
    private bool PropertiesShown => _properties.Visibility == Visibility.Visible;
}
