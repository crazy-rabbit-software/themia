using Microsoft.UI.Xaml.Controls;

namespace Themia.App.Shell.Config;

/// <summary>
/// The navigation pane: the app pages under "Themia" — General, Appearance, Accounts, Displays,
/// Views — and License and About at the foot. [S-1830] One entry per page, never one per view,
/// display or widget: those are groups and rows on the Displays and Views pages.
/// </summary>
internal sealed partial class ConfigWindow
{
    private void RebuildNavigation()
    {
        _nav.MenuItems.Clear();
        _nav.MenuItems.Add(new NavigationViewItemHeader { Content = "Themia" });
        AddAppPages(_nav.MenuItems, AppPageSlot.Main);

        // [S-493] About is the very last entry, pinned to the foot of the menu under everything
        // else, as in karpo and Windows Settings. Anything else that wants the footer goes
        // ABOVE it in ConfigPages.AppPages, never after it.
        _nav.FooterMenuItems.Clear();
        AddAppPages(_nav.FooterMenuItems, AppPageSlot.Footer);

        SelectForTarget();
    }

    /// <summary>The registered app pages of one slot, in their registered order.</summary>
    private static void AddAppPages(IList<object> items, AppPageSlot slot)
    {
        foreach (var page in ConfigPages.AppPages)
        {
            if (page.Slot == slot)
            {
                // [S-1602] Every entry carries a full-colour icon, as the categories of Windows Settings do.
                var item = new NavigationViewItem { Content = page.Title, Icon = NavIcons.Create(page.Icon), Tag = new ConfigTarget.App(page.Page) };
                NavIcons.SizeIconBox(item);
                items.Add(item);
            }
        }
    }

    /// <summary>
    /// [S-1831] Select the entry of the page on screen: the page itself, or Views while a widget's
    /// page is open. Selecting raises nothing that navigates: only <see cref="OnItemInvoked"/> does.
    /// </summary>
    private void SelectForTarget()
    {
        var entry = _target.NavEntry();
        foreach (var candidate in Entries())
        {
            if (candidate.Tag is ConfigTarget tag && tag == entry)
            {
                _nav.SelectedItem = candidate;
                return;
            }
        }
    }

    private IEnumerable<NavigationViewItem> Entries()
        => _nav.MenuItems.OfType<NavigationViewItem>().Concat(_nav.FooterMenuItems.OfType<NavigationViewItem>());

    /// <summary>
    /// A click (or Enter) on an entry. Invoked, not selection-changed, so Views opens its page even
    /// while it is already the selected entry under a widget's page. The page already on screen is
    /// left as it is — its scroll position, its open groups.
    /// </summary>
    private void OnItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer is not NavigationViewItem { Tag: ConfigTarget.App target }
            || (_target is ConfigTarget.App shown && shown.Page == target.Page))
        {
            return;
        }

        Show(target, record: true);
    }
}
