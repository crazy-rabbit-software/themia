using System.Text.Json.Serialization;
using Themia.Core.Localization;
using Themia.Core.Mail;
using Themia.Core.Sync;
using Themia.Core.Widgets;

namespace Themia.Core.Model;

/// <summary>
/// [S-542] [S-6403] How the list draws a message: the Mail widget's styles, what were its view
/// mode and its "Show preview" line. List: sender and time, subject, then the preview
/// (<see cref="EmailConfig.PreviewLines"/> lines). Compact: sender and time, subject. Table: one
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
public sealed class EmailConfig : WidgetConfig, IStyled, IShownSettings<EmailSetting>
{
    /// <summary>The fields the list and compact styles show when the user has not chosen.</summary>
    public static IReadOnlyList<MailListField> DefaultListFields { get; } = [MailListField.Subject, MailListField.Account];

    /// <summary>The row view's columns when the user has not chosen.</summary>
    public static IReadOnlyList<MailColumn> DefaultColumns { get; } = [MailColumn.From, MailColumn.Subject, MailColumn.Date];

    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.Email;

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
        StyleOption.Of(EmailStyle.List, Str.Styles_Email_List, 0xE8FD),
        StyleOption.Of(EmailStyle.Compact, Str.Styles_Email_Compact, 0xEA37),
        StyleOption.Of(EmailStyle.Table, Str.Styles_Email_Table, 0xF12B),
    ];

    IReadOnlyList<StyleOption> IStyled.Styles => EmailStyles;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (EmailStyle)value;
    }

    /// <summary>Lines of preview per message in the List style, 1 to 5.</summary>
    public int PreviewLines { get; set; } = 2;

    /// <summary>The optional lines of the list and compact views; null is <see cref="DefaultListFields"/>.</summary>
    [JsonConverter(typeof(MailListFieldsConverter))]
    public List<MailListField>? ListFields { get; set; }

    /// <summary>The row view's columns in order; null is <see cref="DefaultColumns"/>, and never empty.</summary>
    [JsonConverter(typeof(MailColumnsConverter))]
    public List<MailColumn>? RowColumns { get; set; }

    public MailSortOrder SortOrder { get; set; } = MailSortOrder.NewestFirst;

    /// <summary>
    /// [S-1286] How many messages a widget asks for: the whole shelf the sync keeps per folder
    /// (<see cref="MailShelves.MaxPerFolder"/>), newest first. The list scrolls past what fits, so
    /// the tile's size never changes what is fetched and no count needs choosing.
    /// </summary>
    public const int FetchCount = MailShelves.MaxPerFolder;

    [JsonIgnore]
    public int EffectivePreviewLines => Math.Clamp(PreviewLines, 1, 5);

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
        EmailStyle.List => [EmailSetting.ListFields, EmailSetting.PreviewLines],
        EmailStyle.Compact => [EmailSetting.ListFields],
        _ => [EmailSetting.Columns],
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

        if (PreviewLines != EffectivePreviewLines)
        {
            repairs.Add($"mail widget preview lines {PreviewLines} were out of range");
            PreviewLines = EffectivePreviewLines;
        }

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

        return repairs;
    }

    public override WidgetConfig Clone() => new EmailConfig
    {
        AccountIds = AccountIds is null ? null : [.. AccountIds],
        Folder = Folder,
        Style = Style,
        PreviewLines = PreviewLines,
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

    /// <summary>How many preview lines (List).</summary>
    PreviewLines,

    /// <summary>The table's columns (Table).</summary>
    Columns,
}
