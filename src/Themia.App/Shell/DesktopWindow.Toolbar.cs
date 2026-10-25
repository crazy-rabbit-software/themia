using Microsoft.UI.Xaml.Controls;
using Themia.Core.Localization;

namespace Themia.App.Shell;

/// <summary>[S-3500] The edit bar along the top centre of the surface in edit mode, and its View menu.</summary>
internal sealed partial class DesktopWindow
{
    private EditBar BuildToolbar() => new(ToolbarCommands()) { Visibility = Microsoft.UI.Xaml.Visibility.Collapsed };

    /// <summary>What this display's edit bar does: everything goes through the host, for this display.</summary>
    private EditBarCommands ToolbarCommands() => new(
        AddFlyout: BuildAddFlyout,
        FillViews: FillViewFlyout,
        SnapOn: _host.State.Design.GridSnap,
        SetSnap: on => _host.Mutate(state => state.Design.GridSnap = on),
        Settings: ToggleSettingsMenu,
        MenuOpening: () => CloseSettingsMenu(focusButton: false),
        Done: () => _host.SetEditMode(false));

    /// <summary>
    /// [S-905] The bar's switches follow a change made elsewhere (the settings menu, an import),
    /// [S-3701] the properties strip the selection's widgets as they are now (an undo, another display),
    /// and [S-3531] the settings menu the state.
    /// </summary>
    private void SyncToolbar()
    {
        _toolbar.SyncSnap(Design.GridSnap);
        SyncProperties();
        SyncSettingsMenu();
    }

    private void FillViewFlyout(MenuFlyout flyout)
    {
        flyout.Items.Clear();
        // The view THIS display shows, not the app-wide active one: each display has its own.
        var shownId = CurrentViewId;

        foreach (var view in _host.State.Views)
        {
            var item = new ToggleMenuFlyoutItem
            {
                Text = view.Name,
                IsChecked = view.Id == shownId,

                // [S-903] The view's own icon where it has one; [S-3812] in the view's colour, which
                // a view without an icon shows on the shared view glyph.
                Icon = string.IsNullOrEmpty(view.Icon) && view.Color is null ? null : ViewMark.Icon(view),
            };

            var id = view.Id;
            item.Click += (_, _) => _host.ShowView(_monitor.Id, id);
            flyout.Items.Add(item);
        }

        flyout.Items.Add(new MenuFlyoutSeparator());

        var add = new MenuFlyoutItem { Text = Str.Shell_View_New.Text(), Icon = MenuArtIcons.IconOrGlyph(Glyphs.Add) };
        // [S-1065] The free-tier view limit; a refusal says why in a notice ([S-881]).
        add.Click += (_, _) => _host.AddView(_monitor.Id);

        flyout.Items.Add(add);

        // [S-900] A copy of the view this display shows, shown here in its place.
        var duplicate = new MenuFlyoutItem { Text = Str.Shell_View_DuplicateThis.Text(), Icon = MenuArtIcons.IconOrGlyph(Glyphs.Copy) };
        duplicate.Click += (_, _) => _host.DuplicateView(shownId, _monitor.Id);
        flyout.Items.Add(duplicate);
    }
}
