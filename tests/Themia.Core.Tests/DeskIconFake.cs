using Themia.Core.Model;

namespace Themia.Core.Tests;

/// <summary>
/// A desktop icon's config as Core sees it, no module needed: a desktop item while <see cref="Item"/>
/// holds a key, an own icon (its target, its layout, its label) while it is empty.
/// </summary>
internal sealed class DeskIconFake : WidgetConfig, IDesktopItemConfig
{
    public string Item { get; set; } = string.Empty;

    public string ItemId { get; set; } = Guid.NewGuid().ToString("n");

    public string Target { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public LayoutNode? Layout { get; set; }

    public ItemClickMode ClickMode { get; set; }

    public string OwnName => Label.Length > 0 ? Label : Target;

    public bool RenameOwn(string? typed)
    {
        if (Item.Length > 0)
        {
            return false;
        }

        var label = typed?.Trim() ?? string.Empty;
        if (label == Label)
        {
            return false;
        }

        Label = label;
        return true;
    }

    public override WidgetConfig Clone() => new DeskIconFake
    {
        Item = Item,
        Target = Target,
        Label = Label,
        Layout = Layout?.Clone(),
        ClickMode = ClickMode,
    };
}
