using Themia.Sdk.Kit;
using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Accounts;
using Themia.Core.Localization;
using Themia.Core.Office;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Widget.Contacts;

namespace Themia.Core.Model;

/// <summary>[S-6074] The contacts rows that only some layouts draw with.</summary>
public enum ContactsSetting
{
    /// <summary>The details table's columns.</summary>
    Columns,
}

/// <summary>
/// [S-580] The contacts widget's settings: which accounts, the layout, the columns of the details
/// table, how many to list and a search to start with. Data comes from Themia's own accounts.
/// </summary>
public sealed class ContactsConfig : WidgetConfig, IShownSettings<ContactsSetting>, ISetupRule, IInterests, IKitRows, IChoosesAccounts
{
    /// <summary>[S-11406] A new contacts list's rows.</summary>
    public const int DefaultRows = 5;

    /// <summary>[S-11406] The grid's cells a line: 2 to <see cref="MaxGrid"/>.</summary>
    public const int DefaultGridColumns = 3;

    /// <summary>[S-11406] The grid's lines of cells.</summary>
    public const int DefaultGridRows = 2;

    /// <summary>[S-11406] The most cells a grid line, and the most lines.</summary>
    public const int MaxGrid = 6;

    /// <summary>[S-11406] How many people the List, Compact and Details layouts show; more scroll inside them.</summary>
    public int Rows { get; set; } = DefaultRows;

    /// <summary>[S-11406] Lines per person in the List layout: the name; the role or a way to reach them; the address or the number.</summary>
    public int Lines { get; set; } = 2;

    /// <summary>[S-11406] Only the List layout's people have lines to choose.</summary>
    bool IKitRows.OffersLines => EffectiveLayout == ContactsLayout.List;

    /// <summary>[S-11406] The search box over the people (off: the header's search button opens it).</summary>
    public bool SearchBox { get; set; } = true;

    /// <summary>[S-11406] The Grid layout's cells a line.</summary>
    public int GridColumns { get; set; } = DefaultGridColumns;

    /// <summary>[S-11406] The Grid layout's lines of cells; more scroll inside them.</summary>
    public int GridRows { get; set; } = DefaultGridRows;

    /// <summary>[S-1862] What its Set up row says it waits for.</summary>
    string? ISetupRule.SetupSentence => ContactsStr.Contacts_Setup_Sentence.Text();

    /// <summary>[S-2608] What the background sync keeps fresh for it.</summary>
    public void AddInterests(InterestsBuilder interests) => interests.Of<ContactsInterest>().Add(new ContactsInterest([.. AccountIds]));

    /// <summary>[S-6906] It waits for setup while this holds.</summary>
    public bool NeedsSetup(FeatureSet signedIn) => !signedIn.Has(HostFeatures.Contacts);

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

    public ContactsLayout Layout { get; set; } = ContactsLayout.List;

    /// <summary>[S-582] The details table's columns; kept in canonical order with at least one.</summary>
    [JsonConverter(typeof(LenientEnumListConverter<ContactColumn>))]
    public List<ContactColumn> Columns { get; set; } = [.. ContactColumns.All];

    /// <summary>[S-583] How many people to list: at least five, in steps of five, no upper bound.</summary>
    public int Count { get; set; } = 20;

    [JsonIgnore]
    public int EffectiveCount => Math.Max(5, Count);

    /// <summary>[S-582] [S-6074] The columns are the details table's: the page offers them for that layout.</summary>
    [JsonIgnore]
    public IReadOnlyList<ContactsSetting> ShownSettings => EffectiveLayout == ContactsLayout.Details ? [ContactsSetting.Columns] : [];

    [JsonIgnore]
    public ContactsLayout EffectiveLayout => Enum.IsDefined(Layout) ? Layout : ContactsLayout.List;

    /// <summary>[S-582] The columns as shown: canonical order, unknown values dropped, never none.</summary>
    [JsonIgnore]
    public IReadOnlyList<ContactColumn> EffectiveColumns
    {
        get
        {
            var chosen = ContactColumns.All.Where(c => Columns?.Contains(c) == true).ToList();
            return chosen.Count > 0 ? chosen : [ContactColumn.Name, ContactColumn.Email, ContactColumn.Phone];
        }
    }

    /// <summary>[S-563] Bring every value back inside its range.</summary>
    public override IReadOnlyList<string> Repair()
    {
        var repairs = new List<string>();

        var ids = (AccountIds ?? []).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList();
        if (AccountIds is null || ids.Count != AccountIds.Count)
        {
            repairs.Add("contacts widget had blank or repeated account ids");
        }

        AccountIds = ids;
        Columns ??= [.. ContactColumns.All];

        if (Count != EffectiveCount)
        {
            repairs.Add($"contacts widget count {Count} was below five");
            Count = EffectiveCount;
        }

        if (!Enum.IsDefined(Layout))
        {
            repairs.Add("contacts widget had an unknown layout");
            Layout = ContactsLayout.List;
        }

        var (rows, lines, gridColumns, gridRows) = (KitSizing.ClampRows(Rows), KitSizing.ClampLines(Lines, KitSizing.MaxLines), Math.Clamp(GridColumns, 2, MaxGrid), Math.Clamp(GridRows, 1, MaxGrid));
        if ((rows, lines, gridColumns, gridRows) != (Rows, Lines, GridColumns, GridRows))
        {
            (Rows, Lines, GridColumns, GridRows) = (rows, lines, gridColumns, gridRows);
            repairs.Add("contacts widget's rows, lines or grid were out of range");
        }

        return repairs;
    }

    public override WidgetConfig Clone() => new ContactsConfig
    {
        Rows = Rows,
        Lines = Lines,
        SearchBox = SearchBox,
        GridColumns = GridColumns,
        GridRows = GridRows,
        AccountIds = [.. AccountIds],
        Layout = Layout,
        Columns = [.. Columns],
        Count = Count,
    };
}
