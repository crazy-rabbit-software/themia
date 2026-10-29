using Themia.Core.Model;
using Themia.Sdk;

namespace Themia.Widget.DesktopItem;

/// <summary>
/// [S-5600] What one desktop item widget remembers: the item it stands for, by its key ([S-5510]).
/// The host's reconcile rewrites it on a rename and removes the widget when the item is gone
/// ([S-5520]), through <see cref="IDesktopItemConfig"/>.
/// </summary>
public sealed class DesktopItemConfig : WidgetConfig, IDesktopItemConfig
{
    /// <inheritdoc />
    public string Item { get; set; } = string.Empty;

    public override WidgetConfig Clone() => new DesktopItemConfig { Item = Item };

    /// <summary>[S-5600] A key that is no key (null, too long, a control character) is cleared, loudly: the reconcile then removes the widget.</summary>
    public override IReadOnlyList<string> Repair()
    {
        if (Item is null || (Item.Length > 0 && !DesktopItemKeys.IsValid(Item)))
        {
            Item = string.Empty;
            return ["desktop item key unusable; cleared"];
        }

        return [];
    }
}
