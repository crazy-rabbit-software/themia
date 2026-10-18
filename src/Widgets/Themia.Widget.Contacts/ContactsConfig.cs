using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Accounts;
using Themia.Core.Localization;
using Themia.Core.Office;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk;

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
public sealed class ContactsConfig : WidgetConfig, IShownSettings<ContactsSetting>, ISetupRule, IInterests
{
    /// <summary>[S-2608] What the background sync keeps fresh for it.</summary>
    public void AddInterests(InterestsBuilder interests) => interests.Of<ContactsInterest>().Add(new ContactsInterest([.. AccountIds]));

    /// <summary>[S-6906] It waits for setup while this holds.</summary>
    public bool NeedsSetup(FeatureSet signedIn) => !signedIn.Has(HostFeatures.Contacts);

    /// <summary>The accounts to show; empty means every contacts account.</summary>
    [JsonConverter(typeof(LenientIdListConverter))]
    public List<string> AccountIds { get; set; } = [];

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

        return repairs;
    }

    public override WidgetConfig Clone() => new ContactsConfig
    {
        AccountIds = [.. AccountIds],
        Layout = Layout,
        Columns = [.. Columns],
        Count = Count,
    };
}
