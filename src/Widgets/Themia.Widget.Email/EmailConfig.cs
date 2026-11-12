using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Accounts;
using Themia.Core.Localization;
using Themia.Core.Mail;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Widget.Email;

namespace Themia.Core.Model;

/// <summary>
/// [S-542] [S-6403] How the list draws a message: the Mail widget's styles, what were its view
/// mode and its "Show preview" line. List: sender and time, subject, then the preview, as many
/// lines as the tile gives a row ([S-7032]). Compact: sender and time, subject. Table: one
/// line of the user's columns (<see cref="EmailConfig.RowColumns"/>).
/// </summary>
[JsonConverter(typeof(StyleJsonConverter<EmailStyle>))]
public enum EmailStyle
{
    List = 0,
    Compact = 1,
    Table = 2,
}

/// <summary>
/// [S-540] Recent mail from Themia's own accounts: which accounts, which folder, and how each
/// message is drawn. How many is not a setting: the widget lists the folder's whole shelf ([S-1286]).
/// <para>
/// Every closed set is read leniently (<see cref="LenientWireConverter{T}"/>): an unknown or
/// mistyped value becomes the default and is logged, because a throw inside one widget's config
/// would refuse the whole state file and blank every desktop.
/// </para>
/// </summary>
public sealed class EmailConfig : WidgetConfig, IStyled, IShownSettings<EmailSetting>, IKitConfig, IInterests, IKitRows
{
    /// <summary>[S-11402] A new Mail widget's rows.</summary>
    public const int DefaultRows = 5;

    /// <summary>[S-11402] How many messages the list shows; more scroll inside them. Its height follows.</summary>
    public int Rows { get; set; } = DefaultRows;

    /// <summary>[S-11402] Lines per message: the sender, then the subject, then the preview (List only).</summary>
    public int Lines { get; set; } = 2;

    /// <summary>[S-11402] The Table's column titles over its rows: the user's choice, never the height's.</summary>
    public bool ColumnTitles { get; set; } = true;

    /// <summary>
    /// [S-11402] The most lines a message can have in this style with these fields: the sender, a second
    /// line where the subject or the account is on, the preview's line in List; the Table one.
    /// </summary>
    [JsonIgnore]
    public int MostLines => Style == EmailStyle.Table
        ? 1
        : 1 + (Shows(MailListField.Subject) || Shows(MailListField.Account) ? 1 : 0) + (ShowsPreview ? 1 : 0);

    /// <summary>[S-11402] The rows as drawn: the chosen count, at most the lines a message has, the Table's titles where on.</summary>
    [JsonIgnore]
    public KitRowsChoice RowsShown => Style == EmailStyle.Table
        ? new(KitSizing.ClampRows(Rows), 1, ColumnTitles: ColumnTitles)
        : new(KitSizing.ClampRows(Rows), KitSizing.ClampLines(Lines, MostLines));

    /// <summary>[S-11402] Each style's own widths: the stacked looks the kit's (220..520), the Table's columns 320..720.</summary>
    [JsonIgnore]
    public KitWidths? Widths => Style == EmailStyle.Table ? TableWidths : null;

    /// <summary>[S-11402] A table's sender, subject and date read whole from 320; past 720 the subject is a sentence across a room.</summary>
    public static KitWidths TableWidths { get; } = new(320, 420, 720);

    /// <summary>[S-1862] What its Set up row says it waits for.</summary>
    string? ISetupRule.SetupSentence => EmailStr.Email_Setup_Sentence.Text();

    /// <summary>[S-2608] What the background sync keeps fresh for it.</summary>
    public void AddInterests(InterestsBuilder interests) => interests.Of<MailInterest>().Add(new MailInterest([.. EffectiveAccountIds], Folder, FetchCount));

    /// <summary>[S-7050] [S-6906] A mail account: it waits for setup until one is signed in.</summary>
    FeatureId? IKitConfig.Needs => HostFeatures.Mail;

    /// <summary>Always something to read: the folder, of every account when none is named.</summary>
    bool IKitConfig.Configured => true;

    IReadOnlyList<string> IKitConfig.AccountIds => EffectiveAccountIds;

    KitShow IKitConfig.Shows => MailSpec.Choices(this).Shows;

    KitChoices IKitConfig.Choices => MailSpec.Choices(this);

    /// <summary>The fields the list and compact styles show when the user has not chosen.</summary>
    public static IReadOnlyList<MailListField> DefaultListFields { get; } = [MailListField.Subject, MailListField.Account];

    /// <summary>The row view's columns when the user has not chosen.</summary>
    public static IReadOnlyList<MailColumn> DefaultColumns { get; } = [MailColumn.From, MailColumn.Subject, MailColumn.Date];

    /// <summary>
    /// [S-541] The accounts to merge into the list, by id. Empty means every mail account Themia is
    /// signed in to, so a widget keeps working when an account is added rather than needing a visit
    /// to its settings first.
    /// </summary>
    [JsonConverter(typeof(LenientStringListConverter))]
    public List<string>? AccountIds { get; set; } = [];

    public MailFolder Folder { get; set; } = MailFolder.Inbox;

    /// <summary>[S-6403] List, Compact or Table: the widget's style.</summary>
    public EmailStyle Style { get; set; } = EmailStyle.List;

    private static readonly StyleOption[] EmailStyles =
    [
        StyleOption.Of(EmailStyle.List, EmailStr.Styles_Email_List.Local(), 0xE8FD),
        StyleOption.Of(EmailStyle.Compact, EmailStr.Styles_Email_Compact.Local(), 0xEA37),
        StyleOption.Of(EmailStyle.Table, EmailStr.Styles_Email_Table.Local(), 0xF12B),
    ];

    IReadOnlyList<StyleOption> IStyled.Styles => EmailStyles;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (EmailStyle)value;
    }

    /// <summary>The optional lines of the list and compact views; null is <see cref="DefaultListFields"/>.</summary>
    [JsonConverter(typeof(MailListFieldsConverter))]
    public List<MailListField>? ListFields { get; set; }

    /// <summary>The row view's columns in order; null is <see cref="DefaultColumns"/>, and never empty.</summary>
    [JsonConverter(typeof(MailColumnsConverter))]
    public List<MailColumn>? RowColumns { get; set; }

    public MailSortOrder SortOrder { get; set; } = MailSortOrder.NewestFirst;

    /// <summary>
    /// [S-1286] How many messages a widget asks for: the whole shelf the sync keeps per folder
    /// (<see cref="MailLimits.PerFolder"/>), newest first. The list scrolls past what fits, so
    /// the tile's size never changes what is fetched and no count needs choosing.
    /// </summary>
    public const int FetchCount = MailLimits.PerFolder;

    [JsonIgnore]
    public IReadOnlyList<string> EffectiveAccountIds => AccountIds ?? [];

    [JsonIgnore]
    public IReadOnlyList<MailListField> EffectiveListFields => ListFields ?? DefaultListFields;

    [JsonIgnore]
    public IReadOnlyList<MailColumn> EffectiveColumns => RowColumns is { Count: > 0 } columns ? columns : DefaultColumns;

    /// <summary>[S-543] Whether a chosen line of the list and compact styles shows: subject and account whenever chosen.</summary>
    public bool Shows(MailListField field) => EffectiveListFields.Contains(field);

    /// <summary>[S-6403] The preview line is the List style's own: Compact is the list without it, Table has a column for it.</summary>
    [JsonIgnore]
    public bool ShowsPreview => Style == EmailStyle.List;

    /// <summary>
    /// [S-6403] [S-6070] Which of the page's look settings the current style draws with; the page
    /// offers only these, and the rest keep their values for when the style comes back.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<EmailSetting> ShownSettings => Style switch
    {
        EmailStyle.List or EmailStyle.Compact => [EmailSetting.ListFields],
        _ => [EmailSetting.Columns, EmailSetting.ColumnTitles],
    };

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

        if (ListFields is { } fields)
        {
            ListFields = [.. fields.Where(f => Enum.IsDefined(f)).Distinct()];
        }

        if (RowColumns is { } columns)
        {
            var clean = columns.Where(c => Enum.IsDefined(c)).Distinct().ToList();

            // At least one column always stays: a row view with none is a blank widget.
            if (clean.Count == 0)
            {
                repairs.Add("mail widget had no row columns");
                RowColumns = null;
            }
            else
            {
                RowColumns = clean;
            }
        }

        if (Rows != KitSizing.ClampRows(Rows) || Lines != KitSizing.ClampLines(Lines, KitSizing.MaxLines))
        {
            (Rows, Lines) = (KitSizing.ClampRows(Rows), KitSizing.ClampLines(Lines, KitSizing.MaxLines));
            repairs.Add("mail widget's rows or lines were out of range");
        }

        return repairs;
    }

    public override WidgetConfig Clone() => new EmailConfig
    {
        Rows = Rows,
        Lines = Lines,
        ColumnTitles = ColumnTitles,
        AccountIds = AccountIds is null ? null : [.. AccountIds],
        Folder = Folder,
        Style = Style,
        ListFields = ListFields is null ? null : [.. ListFields],
        RowColumns = RowColumns is null ? null : [.. RowColumns],
        SortOrder = SortOrder,
    };
}

/// <summary>[S-6403] A look setting of the Mail widget that only some styles draw with.</summary>
public enum EmailSetting
{
    /// <summary>The subject and account lines (List, Compact).</summary>
    ListFields,

    /// <summary>The table's columns (Table).</summary>
    Columns,

    /// <summary>[S-11402] The table's column titles (Table).</summary>
    ColumnTitles,
}

/// <summary>[S-540] The mail folders the widget offers.</summary>
public static class EmailFolders
{
    /// <summary>The folders the widget lists, in the order the settings box and the menu offer them.</summary>
    public static IReadOnlyList<MailFolder> Listable { get; } =
        [MailFolder.Inbox, MailFolder.Sent, MailFolder.Drafts, MailFolder.Deleted, MailFolder.Junk];
}
