using Themia.Core.Widgets;

namespace Themia.Core.Model;

/// <summary>
/// [S-5520] [S-5600] The config of a desktop icon, the one kind drawn as Explorer's desktop draws an
/// icon. It is one of two sources, told apart here and nowhere else:
/// <list type="bullet">
/// <item>a <b>desktop item</b>: <see cref="Item"/> is the item's key
/// (<see cref="Themia.Sdk.DesktopItemKeys"/>). The host's reconcile reads and rewrites it in every view
/// (a rename, a removal, a new file) without knowing the module that draws it;</item>
/// <item>an <b>own icon</b>: <see cref="Item"/> is empty and the icon holds what it opens itself
/// (<see cref="Target"/> or <see cref="Layout"/>). The reconcile never touches these.</item>
/// </list>
/// The host's one desktop selection and its commands treat both alike through this contract.
/// </summary>
public interface IDesktopItemConfig
{
    /// <summary>A desktop item's key; empty for an own icon.</summary>
    string Item { get; set; }

    /// <summary>[S-1411] An own icon's stable id: its selection key is made from it, and the layout editor finds it by it.</summary>
    string ItemId { get; }

    /// <summary>An own icon's target (a path, a web address, a Windows location, a Settings page); empty for none. Untrusted: classified at every use.</summary>
    string Target { get; }

    /// <summary>[S-1405] An own icon's app layout, or null.</summary>
    LayoutNode? Layout { get; }

    /// <summary>[S-9505] How an own icon opens: a double click as the desktop's, or a single one.</summary>
    ItemClickMode ClickMode { get; }

    /// <summary>[S-9502] An own icon's name as drawn: the user's label, else the target's own.</summary>
    string OwnName { get; }

    /// <summary>[S-9507] Name an own icon <paramref name="typed"/>; the target's own name (or nothing) clears the label. True when it changed.</summary>
    bool RenameOwn(string? typed);
}
