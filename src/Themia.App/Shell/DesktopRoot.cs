using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;

namespace Themia.App.Shell;

/// <summary>
/// [S-5571] A surface's root: under Themia's items, outside edit mode, it is the desktop itself to a
/// screen reader, as Explorer's desktop is a list named "Desktop": its items are the list's items in
/// the desktop's order (<c>DesktopNav.Order</c>), the selection is the list's (several at once, none
/// required), and the focused item is what has the keyboard focus. Otherwise it is a plain pane.
/// </summary>
internal sealed partial class DesktopRoot(Func<bool> isDesktop) : Grid
{
    /// <summary>Whether the root stands for the desktop now: Themia's items show and edit mode is off.</summary>
    internal bool IsDesktop => isDesktop();

    protected override AutomationPeer OnCreateAutomationPeer() => new DesktopRootAutomationPeer(this);
}

/// <summary>[S-5571] The desktop's list to UI Automation; each call reads the item peers as they are, and none throws back.</summary>
internal sealed partial class DesktopRootAutomationPeer(DesktopRoot owner) : FrameworkElementAutomationPeer(owner), ISelectionProvider
{
    private const string Scope = "desktop-automation";

    protected override AutomationControlType GetAutomationControlTypeCore()
        => owner.IsDesktop ? AutomationControlType.List : AutomationControlType.Pane;

    protected override string GetClassNameCore() => owner.IsDesktop ? "Desktop" : nameof(DesktopRoot);

    protected override string GetNameCore() => owner.IsDesktop ? Str.Desktop_Automation_List.Text() : base.GetNameCore();

    protected override object? GetPatternCore(PatternInterface patternInterface)
        => patternInterface == PatternInterface.Selection && owner.IsDesktop ? this : base.GetPatternCore(patternInterface);

    /// <summary>The items in the desktop's order (each says its place), the rest after them as they were.</summary>
    protected override IList<AutomationPeer>? GetChildrenCore()
    {
        var children = base.GetChildrenCore();
        if (children is null || !owner.IsDesktop)
        {
            return children;
        }

        try
        {
            return [.. children
                .Select((peer, i) => (peer, i, at: peer.GetPositionInSet()))
                .OrderBy(c => c.at > 0 ? 0 : 1)
                .ThenBy(c => c.at > 0 ? c.at : c.i)
                .Select(c => c.peer)];
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, "ordering the desktop's items for UI Automation failed; their tree order stands", ex);
            return children;
        }
    }

    /// <summary>The desktop's focused item has the focus while the desktop holds the keys; else what XAML says.</summary>
    protected override object? GetFocusedElementCore()
    {
        if (owner.IsDesktop && Items().FirstOrDefault(peer => peer.HasKeyboardFocus()) is { } focused)
        {
            return focused;
        }

        return base.GetFocusedElementCore();
    }

    public bool CanSelectMultiple => true;

    public bool IsSelectionRequired => false;

    public IRawElementProviderSimple[] GetSelection()
    {
        try
        {
            return [.. Items()
                .Where(peer => peer.GetPattern(PatternInterface.SelectionItem) is ISelectionItemProvider { IsSelected: true })
                .Select(ProviderFromPeer)];
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, "reading the desktop's selection for UI Automation failed", ex);
            return [];
        }
    }

    /// <summary>The item peers among the children: the ones that say their place in the desktop's order.</summary>
    private IEnumerable<AutomationPeer> Items()
        => (base.GetChildrenCore() ?? []).Where(peer => peer.GetPositionInSet() > 0);
}
