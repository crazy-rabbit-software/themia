using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Accounts;
using Themia.Core.Localization;
using Themia.Core.Office;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Items;
using Themia.Sdk.Kit;
using Themia.Widget.Contacts;

namespace Themia.Core.Model;

/// <summary>
/// [S-580] [S-16972] What both contacts widgets share: which accounts they read (empty: every contacts account), their setup
/// rule and the item base's parts ([S-16950]). Data comes from Themia's own accounts.
/// </summary>
public abstract class ContactsBaseConfig : WidgetConfig, ISetupRule, IInterests, IChoosesAccounts, IItemParts
{
    /// <summary>[S-1862] What its Set up row says it waits for.</summary>
    string? ISetupRule.SetupSentence => ContactsStr.Contacts_Setup_Sentence.Text();

    /// <summary>[S-2608] What the background sync keeps fresh for it.</summary>
    public void AddInterests(InterestsBuilder interests) => interests.Of<ContactsInterest>().Add(new ContactsInterest([.. AccountIds]));

    /// <summary>[S-6906] It waits for setup while this holds.</summary>
    public bool NeedsSetup(FeatureSet signedIn) => !signedIn.Has(HostFeatures.Contacts);

    /// <summary>[S-15785] It says so in its own state's line over its items kept empty, as the calendar does: no card line over it.</summary>
    bool ISetupRule.SetsUpInPlace => true;

    /// <summary>The accounts to show; empty means every contacts account.</summary>
    [JsonConverter(typeof(LenientIdListConverter))]
    public List<string> AccountIds { get; set; } = [];

    /// <summary>[S-11752] The contacts accounts it reads, under the shape every account widget shares.</summary>
    FeatureId IChoosesAccounts.AccountFeature => HostFeatures.Contacts;

    IReadOnlyList<string> IChoosesAccounts.ChosenAccounts
    {
        get => AccountIds;
        set => AccountIds = [.. value];
    }

    /// <summary>[S-16946] The parts left out: the number (it is on the menu) by default.</summary>
    public List<ItemPart> HiddenParts { get; set; } = [ItemPart.End];

    /// <summary>[S-16950] The parts' order: a table's columns.</summary>
    public List<ItemPart> PartOrder { get; set; } = [];

    /// <summary>[S-563] The shared keys brought back inside their range; a note each.</summary>
    protected List<string> RepairShared(string kind)
    {
        var repairs = new List<string>();
        var ids = (AccountIds ?? []).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList();
        if (AccountIds is null || ids.Count != AccountIds.Count)
        {
            repairs.Add($"{kind} widget had blank or repeated account ids");
        }

        AccountIds = ids;
        HiddenParts = ItemOrder.Repaired(HiddenParts, kind, repairs);
        PartOrder = ItemOrder.Repaired(PartOrder, kind, repairs);
        return repairs;
    }

    /// <summary>The shared keys copied onto <paramref name="copy"/>.</summary>
    protected T CopySharedTo<T>(T copy)
        where T : ContactsBaseConfig
    {
        copy.AccountIds = [.. AccountIds];
        copy.HiddenParts = [.. HiddenParts];
        copy.PartOrder = [.. PartOrder];
        return copy;
    }
}

/// <summary>
/// [S-580] [S-16972] The Contacts list on the item base: which accounts, how many people, the search box over them (owner,
/// 2026-11-23: "contacts has the search box that should be kept"), the item base's arrangement, gap, rows and parts.
/// </summary>
public sealed class ContactsConfig : ContactsBaseConfig, IItemList, IStyled
{
    /// <summary>[S-11406] A new contacts list's rows.</summary>
    public const int DefaultRows = 5;

    /// <summary>[S-11406] How many people a new card stands; more scroll inside them.</summary>
    public int Rows { get; set; } = DefaultRows;

    /// <summary>[S-11406] The search box over the people (off: the card menu's Search opens it).</summary>
    public bool SearchBox { get; set; } = true;

    /// <summary>[S-583] How many people to list: at least five, in steps of five, no upper bound.</summary>
    public int Count { get; set; } = 20;

    /// <summary>[S-16940] The item base's arrangement, the list first.</summary>
    public ItemStyle Style { get; set; } = ItemStyle.List;

    /// <summary>[S-16673] The grid blocks between two people.</summary>
    public int Gap { get; set; }

    /// <summary>[S-16952] A table's column titles.</summary>
    public bool ColumnTitles { get; set; } = true;

    [JsonIgnore]
    public int EffectiveCount => Math.Max(5, Count);

    [JsonIgnore]
    ItemStyle IItemList.Arrangement => Style;

    IReadOnlyList<StyleOption> IStyled.Styles => ItemStyles.All;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (ItemStyle)value;
    }

    /// <summary>[S-16972] One face a person: no lines to choose.</summary>
    [JsonIgnore]
    bool IKitRows.OffersLines => false;

    [JsonIgnore]
    public int Lines
    {
        get => 1;
        set
        {
            // ignore: an item has its one face; the menu offers no lines.
        }
    }

    /// <summary>[S-563] Bring every value back inside its range.</summary>
    public override IReadOnlyList<string> Repair()
    {
        var repairs = RepairShared("contacts");
        if (Count != EffectiveCount)
        {
            repairs.Add($"contacts widget count {Count} was below five");
            Count = EffectiveCount;
        }

        repairs.AddRange(ItemOrder.Repair(this, "contacts"));
        return repairs;
    }

    public override WidgetConfig Clone()
    {
        var copy = CopySharedTo(new ContactsConfig());
        copy.Rows = Rows;
        copy.SearchBox = SearchBox;
        copy.Count = Count;
        copy.Style = Style;
        copy.Gap = Gap;
        copy.ColumnTitles = ColumnTitles;
        return copy;
    }
}

/// <summary>
/// [S-16973] The Contact widget: one person the user picked (owner, 2026-11-23: "contact should get a single too"), on the item
/// base's face. Empty: none picked yet (the face says so).
/// </summary>
public sealed class ContactConfig : ContactsBaseConfig
{
    /// <summary>[S-16973] The person's id in their address book; empty for none.</summary>
    public string Person { get; set; } = string.Empty;

    public override IReadOnlyList<string> Repair() => RepairShared("contact");

    public override WidgetConfig Clone()
    {
        var copy = CopySharedTo(new ContactConfig());
        copy.Person = Person;
        return copy;
    }
}
