namespace Themia.App.Shell;

/// <summary>[S-3500] The edit bar along the top centre of the surface in edit mode.</summary>
internal sealed partial class DesktopWindow
{
    private EditBar BuildToolbar() => new(ToolbarCommands()) { Visibility = Microsoft.UI.Xaml.Visibility.Collapsed };

    /// <summary>What this display's edit bar does: everything goes through the host, for this display.</summary>
    private EditBarCommands ToolbarCommands() => new(
        AddFlyout: BuildAddFlyout,
        SnapOn: _host.State.Design.GridSnap,
        SetSnap: on => _host.Mutate(state => state.Design.GridSnap = on),
        Menu: ToggleBarMenu,
        MenuOpening: () => CloseBarMenu(focusButton: false),
        Done: () => _host.SetEditMode(false));

    /// <summary>
    /// [S-905] The bar's switches follow a change made elsewhere (the settings menu, an import),
    /// [S-3701] the properties strip the selection's widgets as they are now (an undo, another display),
    /// and [S-3531] the bar's open menu the state.
    /// </summary>
    private void SyncToolbar()
    {
        _toolbar.SyncSnap(Design.GridSnap);
        SyncProperties();
        SyncBarMenu();
    }
}
