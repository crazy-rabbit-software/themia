using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Accounts;
using Themia.Core.Localization;
using Themia.Core.Mail;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Items;
using Themia.Sdk.Kit;
using Themia.Widget.Email;

namespace Themia.Core.Model;

/// <summary>
/// [S-540] [S-16971] Recent mail from Themia's own accounts on the item base: which accounts, which folder, its order, and how
/// each message is drawn (the item base's arrangement, gap, rows and parts, the preview after the subject). How many is not a
/// setting: the widget lists the folder's whole shelf ([S-1286]).
/// <para>
/// Every closed set is read leniently (<see cref="LenientWireConverter{T}"/>): an unknown or mistyped value becomes the
/// default and is logged, because a throw inside one widget's config would refuse the whole state file and blank every
/// desktop.
/// </para>
/// </summary>
public sealed class EmailConfig : WidgetConfig, IStyled, IKitConfig, IInterests, IItemList, IChoosesAccounts
{
    /// <summary>[S-11402] A new Mail widget's rows.</summary>
    public const int DefaultRows = 5;

    /// <summary>[S-11402] How many messages a new card stands; more scroll inside them.</summary>
    public int Rows { get; set; } = DefaultRows;

    /// <summary>[S-16952] A table's column titles over its rows: the user's choice, never the height's.</summary>
    public bool ColumnTitles { get; set; } = true;

    /// <summary>[S-16971] The preview after the subject, on the subject's line.</summary>
    public bool ShowPreview { get; set; }

    /// <summary>[S-16940] The item base's arrangement, the list first.</summary>
    public ItemStyle Style { get; set; } = ItemStyle.List;

    /// <summary>[S-16673] The grid blocks between two messages.</summary>
    public int Gap { get; set; }

    /// <summary>
    /// [S-16946] [S-12801] The parts left out: the account (a choice for an inbox of several accounts; on every message of one
    /// it only repeats the context) by default.
    /// </summary>
    public List<ItemPart> HiddenParts { get; set; } = [ItemPart.Note];

    /// <summary>[S-16950] The parts' order: a table's columns.</summary>
    public List<ItemPart> PartOrder { get; set; } = [];

    [JsonIgnore]
    ItemStyle IItemList.Arrangement => Style;

    IReadOnlyList<StyleOption> IStyled.Styles => ItemStyles.All;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (ItemStyle)value;
    }

    /// <summary>[S-16971] One face a message: no lines to choose.</summary>
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

    /// <summary>[S-1862] What its Set up row says it waits for.</summary>
    string? ISetupRule.SetupSentence => EmailStr.Email_Setup_Sentence.Text();

    /// <summary>[S-2608] What the background sync keeps fresh for it.</summary>
    public void AddInterests(InterestsBuilder interests) => interests.Of<MailInterest>().Add(new MailInterest([.. EffectiveAccountIds], Folder, FetchCount));

    /// <summary>[S-7050] [S-6906] A mail account: it waits for setup until one is signed in.</summary>
    FeatureId? IKitConfig.Needs => HostFeatures.Mail;

    /// <summary>Always something to read: the folder, of every account when none is named.</summary>
    bool IKitConfig.Configured => true;

    IReadOnlyList<string> IKitConfig.AccountIds => EffectiveAccountIds;

    KitShow IKitConfig.Shows => KitShow.When | KitShow.Subtitle;

    KitChoices IKitConfig.Choices => new(KitShow.When | KitShow.Subtitle, []) { Rows = new KitRowsChoice(KitSizing.ClampRows(Rows), 1) };

    /// <summary>
    /// [S-541] The accounts to merge into the list, by id. Empty means every mail account Themia is
    /// signed in to, so a widget keeps working when an account is added rather than needing a visit
    /// to its settings first.
    /// </summary>
    [JsonConverter(typeof(LenientStringListConverter))]
    public List<string>? AccountIds { get; set; } = [];

    /// <summary>[S-11752] The mail accounts it reads, under the shape every account widget shares.</summary>
    FeatureId IChoosesAccounts.AccountFeature => HostFeatures.Mail;

    IReadOnlyList<string> IChoosesAccounts.ChosenAccounts
    {
        get => EffectiveAccountIds;
        set => AccountIds = [.. value];
    }

    public MailFolder Folder { get; set; } = MailFolder.Inbox;

    public MailSortOrder SortOrder { get; set; } = MailSortOrder.NewestFirst;

    /// <summary>
    /// [S-1286] How many messages a widget asks for: the whole shelf the sync keeps per folder
    /// (<see cref="MailLimits.PerFolder"/>), newest first. The list scrolls past what fits, so
    /// the tile's size never changes what is fetched and no count needs choosing.
    /// </summary>
    public const int FetchCount = MailLimits.PerFolder;

    [JsonIgnore]
    public IReadOnlyList<string> EffectiveAccountIds => AccountIds ?? [];

    /// <summary>[S-540] Bring every value back inside its range.</summary>
    public override IReadOnlyList<string> Repair()
    {
        var repairs = new List<string>();

        var ids = (AccountIds ?? []).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList();
        if (AccountIds is null || ids.Count != AccountIds.Count)
        {
            repairs.Add("mail widget had blank or repeated account ids");
        }

        AccountIds = ids;

        if (!Enum.IsDefined(Folder) || Folder == MailFolder.Archive)
        {
            repairs.Add("mail widget had a folder it cannot list");
            Folder = MailFolder.Inbox;
        }

        if (!Enum.IsDefined(SortOrder))
        {
            repairs.Add("mail widget had an unknown sort order");
            SortOrder = MailSortOrder.NewestFirst;
        }

        repairs.AddRange(ItemOrder.Repair(this, "mail widget"));
        return repairs;
    }

    public override WidgetConfig Clone() => new EmailConfig
    {
        Rows = Rows,
        ColumnTitles = ColumnTitles,
        ShowPreview = ShowPreview,
        Style = Style,
        Gap = Gap,
        HiddenParts = [.. HiddenParts],
        PartOrder = [.. PartOrder],
        AccountIds = AccountIds is null ? null : [.. AccountIds],
        Folder = Folder,
        SortOrder = SortOrder,
    };
}

/// <summary>[S-540] The mail folders the widget offers.</summary>
public static class EmailFolders
{
    /// <summary>The folders the widget lists, in the order the settings box and the menu offer them.</summary>
    public static IReadOnlyList<MailFolder> Listable { get; } =
        [MailFolder.Inbox, MailFolder.Sent, MailFolder.Drafts, MailFolder.Deleted, MailFolder.Junk];
}
