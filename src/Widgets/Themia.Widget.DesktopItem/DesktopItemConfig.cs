using System.Text.Json.Serialization;
using Themia.Core.Accounts;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Widget.DesktopItem;

/// <summary>
/// [S-5600] [S-9501] What one desktop icon remembers. It is one of two sources, told apart here alone:
/// <list type="bullet">
/// <item>a <b>desktop item</b>: <see cref="Item"/> is the item's key ([S-5510]). The host's reconcile
/// creates these, rewrites the key on a rename and removes the widget when the item is gone
/// ([S-5520]), through <see cref="IDesktopItemConfig"/>;</item>
/// <item>an <b>own icon</b> (no key): the one thing it opens (a web address, a file or folder, a Windows
/// location, a Windows Settings page, or an app layout), the user's name and icon for it, and how it
/// opens. The reconcile never touches it.</item>
/// </list>
/// <para>
/// The <see cref="Target"/> is untrusted. It reaches here from a text box, a drop and a JSON file in
/// the user's profile that anything running as the user can rewrite, and it ends at a shell call, so it
/// is checked by <see cref="LaunchTargets"/> every time it is used, never once on the way in.
/// </para>
/// <para>
/// [S-1405] An own icon with a <see cref="Layout"/> is an <b>app layout</b>: opening it opens every app
/// of its pane tree and puts each window in its pane. Its <see cref="Target"/> is empty and never
/// launched. The host's one layout editor finds it by its <see cref="ItemId"/> (<see cref="IHoldsAppLayouts"/>).
/// </para>
/// </summary>
public sealed class DesktopItemConfig : WidgetConfig, IDesktopItemConfig, ISetupRule, IHoldsAppLayouts, IAppLayoutItem
{
    /// <inheritdoc />
    public string Item { get; set; } = string.Empty;

    /// <summary>
    /// [S-1411] [S-5540] An own icon's stable id: its selection key is made from it
    /// (<see cref="DesktopItemKeys.Own"/>) and the layout editor holds it while its window is open. A
    /// copy gets one of its own (<see cref="Clone"/>), so an edit never lands on two icons.
    /// </summary>
    public string ItemId { get; set; } = NewId();

    /// <summary>[S-9502] The user's own name for an own icon, or empty to use the target's own.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Where an own icon points. Never launched without being classified first.</summary>
    public string Target { get; set; } = string.Empty;

    /// <summary>
    /// [S-9508] The icon a dropped <c>.url</c> named (<c>IconFile=</c>, a Steam game's icon), drawn
    /// instead of the target's own. A local path, gated like every probe; null for none.
    /// </summary>
    public string? IconPath { get; set; }

    /// <summary>
    /// [S-9503] The user's own picture for an own icon (png, jpg, bmp, gif, ico or svg), drawn instead of
    /// any other icon while the file is there; null for none. A local path, checked by
    /// <see cref="OwnIcons.IsPicture"/> as it is chosen and on load.
    /// </summary>
    public string? IconPicture { get; set; }

    /// <summary>[S-9503] The user's own emoji for an own icon (one grapheme, <see cref="OwnIcons.Emoji"/>); null for none. Never with a picture.</summary>
    public string? IconEmoji { get; set; }

    /// <summary>[S-1405] The app layout's pane tree, or null.</summary>
    public LayoutNode? Layout { get; set; }

    /// <summary>[S-11204] The name's lines under the icon, 1 or 2; the cell's height follows. Both sources.</summary>
    public int LabelLines { get; set; } = 2;

    /// <summary>
    /// [S-9505] Single or double click to open an own icon. Double by default, as Windows' desktop: the
    /// icon sits in the pointer's path across the desktop, and a stray click opening something is a hazard.
    /// </summary>
    public ItemClickMode ClickMode { get; set; } = ItemClickMode.Double;

    /// <summary>Whether this is a desktop item (it has a key), not an own icon.</summary>
    [JsonIgnore]
    public bool IsDesktopItem => Item.Length > 0;

    /// <summary>Whether this own icon is an app layout rather than a target.</summary>
    [JsonIgnore]
    public bool IsLayout => !IsDesktopItem && Layout is not null;

    /// <summary>Whether an own icon opens nothing yet: neither a target nor a layout.</summary>
    [JsonIgnore]
    public bool OpensNothing => !IsDesktopItem && Layout is null && string.IsNullOrWhiteSpace(Target);

    /// <inheritdoc />
    [JsonIgnore]
    public string OwnName => OwnIconRules.Name(this);

    /// <inheritdoc />
    public bool RenameOwn(string? typed) => !IsDesktopItem && OwnIconRules.Rename(this, typed);

    /// <summary>[S-9511] An own icon waits for setup while it opens nothing; a desktop item never does.</summary>
    public bool NeedsSetup(FeatureSet signedIn) => OpensNothing;

    /// <summary>[S-1881] [S-9508] Its setup can be a drop onto the widget itself, so it runs while it waits.</summary>
    bool ISetupRule.SetsUpInPlace => true;

    /// <summary>[S-1862] What the Set up row says while it opens nothing.</summary>
    string? ISetupRule.SetupSentence => DesktopItemStr.Shortcut_Setup.Text();

    /// <summary>[S-1411] This icon, for the host's one layout editor, while it is the layout with this id.</summary>
    public IAppLayoutItem? FindLayout(string itemId) => IsLayout && string.Equals(ItemId, itemId, StringComparison.Ordinal) ? this : null;

    /// <summary>[S-9509] Point an own icon at <paramref name="target"/>: no layout any more, and no icon a dropped <c>.url</c> named for the old target. The name and the user's own icon stay.</summary>
    public void SetTarget(string target)
    {
        Target = target;
        Layout = null;
        IconPath = null;
    }

    /// <summary>[S-1405] [S-9509] Make an own icon a new app layout: one empty pane, no target.</summary>
    public void MakeLayout()
    {
        Layout = LayoutNode.NewPane();
        Target = string.Empty;
        IconPath = null;
    }

    /// <summary>
    /// [S-5600] [S-9501] Load repair, loud: a key that is no key is cleared; a desktop item holds nothing
    /// of an own icon (never both a key and a target); an own icon's id, click mode, layout and icons are
    /// made honest; the name lines are 1 or 2.
    /// </summary>
    public override IReadOnlyList<string> Repair()
    {
        var repairs = new List<string>();

        // An own icon's selection key is no desktop item's: one stored as an item is no key at all.
        if (Item is null || (Item.Length > 0 && (!DesktopItemKeys.IsValid(Item) || DesktopItemKeys.IsOwn(Item))))
        {
            Item = string.Empty;
            repairs.Add("desktop item key unusable; cleared");
        }

        if (Target is null || Label is null)
        {
            Target ??= string.Empty;
            Label ??= string.Empty;
            repairs.Add("desktop icon had no target or name; emptied");
        }

        // The id makes the selection key and is what the layout editor keys on.
        if (!DesktopItemKeys.IsOwnId(ItemId))
        {
            ItemId = NewId();
            repairs.Add("desktop icon had no usable id");
        }

        if (!Enum.IsDefined(ClickMode))
        {
            ClickMode = ItemClickMode.Double;
            repairs.Add("desktop icon had an unknown click mode");
        }

        if (LabelLines != DesktopCell.ClampLines(LabelLines))
        {
            LabelLines = DesktopCell.ClampLines(LabelLines);
            repairs.Add("desktop icon's name lines out of range");
        }

        // One source: a desktop item is the shell's own, with nothing of an own icon beside its key.
        if (IsDesktopItem)
        {
            if (Target.Length > 0 || Label.Length > 0 || Layout is not null || IconPath is not null || IconPicture is not null || IconEmoji is not null)
            {
                Target = string.Empty;
                Label = string.Empty;
                Layout = null;
                IconPath = null;
                IconPicture = null;
                IconEmoji = null;
                repairs.Add("desktop item also held an own icon's target, name or icon; dropped");
            }

            return repairs;
        }

        // [S-1406] A layout's tree is made honest; its target is never launched, so it is blank.
        if (Layout is not null)
        {
            Layout = LayoutTree.Repair(Layout, repairs);

            if (Target.Length > 0)
            {
                Target = string.Empty;
                repairs.Add("desktop icon is an app layout and had a target");
            }
        }

        if (IconPath is { } icon && LaunchTargets.Classify(icon).Kind != LaunchKind.LocalPath)
        {
            IconPath = null;
            repairs.Add("desktop icon had an icon that is not a local file");
        }

        OwnIcons.Repair(this, repairs);
        return repairs;
    }

    /// <summary>A copy with an id of its own, so a duplicated icon is selected, and edited as a layout, on its own.</summary>
    public override WidgetConfig Clone() => new DesktopItemConfig
    {
        Item = Item,
        ItemId = NewId(),
        Label = Label,
        Target = Target,
        IconPath = IconPath,
        IconPicture = IconPicture,
        IconEmoji = IconEmoji,
        Layout = Layout?.Clone(),
        LabelLines = LabelLines,
        ClickMode = ClickMode,
    };

    private static string NewId() => Guid.NewGuid().ToString("n");
}
