using Microsoft.UI.Xaml.Media;

namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-5510]..[S-5512] [S-5530] The desktop's items as an item widget reads them
/// (<see cref="IWidgetHost.DesktopItems"/>): the host's one listing of the shell's desktop namespace,
/// kept current by the shell's change notifications, the items' icons as Explorer's desktop draws them,
/// and the one item selection every item widget on every display shares. The namespace, the
/// notifications and the image lists stay host code. UI thread throughout.
/// </summary>
public interface IDesktopItems
{
    /// <summary>The items as last read: empty until the first read, and while the desktop shows no item of Themia's.</summary>
    IReadOnlyList<DesktopItem> Items { get; }

    /// <summary>The listed item with <paramref name="key"/>, or null.</summary>
    DesktopItem? Find(string? key);

    /// <summary>Raised on the UI thread when <see cref="Items"/> or <see cref="Selected"/> changed.</summary>
    event Action? Changed;

    /// <summary>[S-5530] The selected item's key, or null for none.</summary>
    string? Selected { get; }

    /// <summary>[S-5530] Select one item alone (null clears): every item widget shows it.</summary>
    void Select(string? key);

    /// <summary>[S-5512] The cached icon for <paramref name="item"/> at <paramref name="pixels"/> (physical), if there is one (no extraction).</summary>
    bool TryIcon(DesktopItem item, int pixels, out ImageSource? image);

    /// <summary>[S-5512] The shell's icon with its overlay (the shortcut arrow among them), or null.</summary>
    Task<ImageSource?> IconAsync(DesktopItem item, int pixels);
}
