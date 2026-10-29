using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Themia.Core.Diagnostics;
using Themia.Core.Desktop;
using Themia.Sdk;

namespace Themia.Widget.DesktopItem;

/// <summary>
/// [S-5570] What a screen reader is told of one desktop item, as Explorer's desktop tells it of an icon:
/// a list item named as the icon's name, "3 of 12" in the desktop's order, selectable (alone, added,
/// removed: the one selection, [S-5540]), invoked to open ([S-5530]) and renamed through its value
/// (F2's rules, [S-5543]); it has the keyboard focus while it is the desktop's focused item. Every call
/// goes to the host's desktop; none throws back into UI Automation (a refusal is logged and ignored).
/// </summary>
internal sealed partial class DesktopItemAutomationPeer(DesktopItemView owner)
    : FrameworkElementAutomationPeer(owner), IInvokeProvider, ISelectionItemProvider, IValueProvider
{
    private const string Scope = "desktop-item";

    private Themia.Sdk.DesktopItem? Item => owner.ShownItem;

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

    protected override string GetClassNameCore() => nameof(DesktopItem);

    protected override string GetNameCore() => Item?.Name ?? string.Empty;

    protected override bool IsKeyboardFocusableCore() => owner.Acts;

    protected override bool HasKeyboardFocusCore() => Item is { } item && owner.Acts && owner.Host.DesktopItems.IsFocused(item.Key);

    protected override int GetPositionInSetCore() => Item is { } item && owner.Host.DesktopItems.PositionOf(item.Key) is { } at ? at.Position : -1;

    protected override int GetSizeOfSetCore() => Item is { } item && owner.Host.DesktopItems.PositionOf(item.Key) is { } at ? at.Count : -1;

    protected override object? GetPatternCore(PatternInterface patternInterface)
        => patternInterface is PatternInterface.Invoke or PatternInterface.SelectionItem or PatternInterface.Value && owner.Acts
            ? this
            : base.GetPatternCore(patternInterface);

    // ---- Invoke: open ([S-5530]) --------------------------------------------------------------------

    public void Invoke()
    {
        if (Item is { } item && owner.Acts)
        {
            owner.Host.DesktopItems.Open(item.Key);
        }
    }

    // ---- SelectionItem: the one selection ([S-5540]) ------------------------------------------------

    public bool IsSelected => Item is { } item && owner.Host.DesktopItems.IsSelected(item.Key);

    public IRawElementProviderSimple? SelectionContainer
        => owner.XamlRoot?.Content is UIElement root && FrameworkElementAutomationPeer.FromElement(root) is { } peer
            ? ProviderFromPeer(peer)
            : null;

    public void Select() => Press(ctrl: false, only: null);

    public void AddToSelection() => Press(ctrl: true, only: false);

    public void RemoveFromSelection() => Press(ctrl: true, only: true);

    /// <summary>A click's worth of selection: alone, or Ctrl toggling it when its state is <paramref name="only"/>.</summary>
    private void Press(bool ctrl, bool? only)
    {
        if (Item is not { } item || !owner.Acts || (only is { } wanted && IsSelected != wanted))
        {
            return;
        }

        owner.Host.DesktopItems.Press(item.Key, ctrl, shift: false);
        owner.Host.DesktopItems.Release(item.Key);
    }

    // ---- Value: the name, set by F2's rules ([S-5543]) ----------------------------------------------

    public string Value => Item?.Name ?? string.Empty;

    public bool IsReadOnly => Item is not { } item || !DesktopRename.CanRename(item);

    public void SetValue(string value)
    {
        if (Item is not { } item || IsReadOnly || !owner.Acts)
        {
            return;
        }

        if (owner.Host.DesktopItems.RenameTo(item.Key, value ?? string.Empty) is { } why)
        {
            // Said, not thrown: UI Automation's caller gets no exception from here.
            Log.Info(Scope, $"a screen reader's rename was refused: {why}");
        }
    }

    // ---- what changed, told ------------------------------------------------------------------------

    /// <summary>[S-5570] The view painted: a selection that changed and a focus that arrived are announced.</summary>
    internal void Changed(bool wasSelected, bool selected, bool wasFocused, bool focused)
    {
        if (wasSelected != selected && ListenerExists(AutomationEvents.PropertyChanged))
        {
            RaisePropertyChangedEvent(SelectionItemPatternIdentifiers.IsSelectedProperty, wasSelected, selected);
        }

        if (focused && !wasFocused && ListenerExists(AutomationEvents.AutomationFocusChanged))
        {
            RaiseAutomationEvent(AutomationEvents.AutomationFocusChanged);
        }
    }
}
