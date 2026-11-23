using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Items;
using Themia.Sdk.Kit;
using Themia.Widget.Meetings;

namespace Themia.Core.Model;

/// <summary>[S-16960] Which meeting the Meeting widget shows: the next one, or one the user picked.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<MeetingPick>))]
public enum MeetingPick
{
    /// <summary>[S-4821] The one that matters now (under way, or the next; the next one 5 minutes before it starts).</summary>
    Next = 0,

    /// <summary>One upcoming meeting by its fingerprint; once it is over, the next one again.</summary>
    Chosen = 1,
}

/// <summary>[S-16962] Which meetings the Meetings list shows: those coming up, those past, or both.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<MeetingWhen>))]
public enum MeetingWhen
{
    /// <summary>Those not over yet (one under way too), from now.</summary>
    Future = 0,

    /// <summary>Those over.</summary>
    Past = 1,

    /// <summary>Both.</summary>
    Both = 2,
}

/// <summary>[S-16962] The list's order by start.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<MeetingOrder>))]
public enum MeetingOrder
{
    /// <summary>The earliest start first.</summary>
    Soonest = 0,

    /// <summary>The latest start first.</summary>
    Latest = 1,
}

/// <summary>
/// [S-16960] What both meeting widgets share: the calendar accounts and calendars every calendar widget chooses ([S-562],
/// [S-567]; never birthdays: all-day, never a meeting), the item base's parts ([S-16950]) and the meeting's own: its place and
/// its organiser in the note ([S-11407]). Every property is stored; copy each in <see cref="CopyMeetingTo"/>.
/// </summary>
public abstract class MeetingsBaseConfig : CalendarConfig, IItemParts
{
    protected MeetingsBaseConfig()
    {
        ShowBirthdays = false;
    }

    /// <summary>[S-1862] What its Set up row says it waits for.</summary>
    [JsonIgnore]
    public override string? SetupSentence => MeetingsStr.UpNext_Setup_Sentence.Text();

    /// <summary>What is coming up: the agenda's shape.</summary>
    [JsonIgnore]
    public override CalendarStyle Layout => CalendarStyle.Agenda;

    /// <summary>[S-11407] The meeting's place in its note (where it has one).</summary>
    public bool ShowPlace { get; set; } = true;

    /// <summary>[S-11407] Who organised it, in its note (where it has one).</summary>
    public bool ShowOrganizer { get; set; } = true;

    /// <summary>[S-16946] The parts left out: the lead and the bar by default (a meeting needs no icon, owner 2026-11-23).</summary>
    public List<ItemPart> HiddenParts { get; set; } = [ItemPart.Lead, ItemPart.Bar];

    /// <summary>[S-16950] The parts' order: a table's columns.</summary>
    public List<ItemPart> PartOrder { get; set; } = [];

    protected override void RepairOwn(List<string> repairs)
    {
        if (ShowBirthdays)
        {
            repairs.Add($"{Kind}: a meeting never shows birthdays");
            ShowBirthdays = false;
        }

        HiddenParts = ItemOrder.Repaired(HiddenParts, Kind.Value, repairs);
        PartOrder = ItemOrder.Repaired(PartOrder, Kind.Value, repairs);
        RepairMeeting(repairs);
    }

    /// <summary>The kind's own keys repaired.</summary>
    protected abstract void RepairMeeting(List<string> repairs);

    /// <summary>A copy of the shared keys onto <paramref name="copy"/>.</summary>
    protected T CopyMeetingTo<T>(T copy)
        where T : MeetingsBaseConfig
    {
        CopySharedTo(copy);
        copy.ShowPlace = ShowPlace;
        copy.ShowOrganizer = ShowOrganizer;
        copy.HiddenParts = [.. HiddenParts];
        copy.PartOrder = [.. PartOrder];
        return copy;
    }
}

/// <summary>
/// [S-16960] The Meeting widget: one meeting on the item base's face, the next one or one the user picked (owner, 2026-11-23:
/// "there should also be a single meeting that you can place (you can choose it to show your next meeting for example)").
/// A placed Next meeting becomes it, showing the next meeting ([S-16964]).
/// </summary>
public sealed class MeetingConfig : MeetingsBaseConfig
{
    /// <summary>[S-16960] The next meeting, or the one picked.</summary>
    public MeetingPick Pick { get; set; } = MeetingPick.Next;

    /// <summary>[S-16960] The picked meeting's fingerprint (<see cref="MeetingItems.Id"/>); empty with the next one.</summary>
    public string Chosen { get; set; } = string.Empty;

    protected override void RepairMeeting(List<string> repairs)
    {
        if (Pick == MeetingPick.Chosen && string.IsNullOrWhiteSpace(Chosen))
        {
            repairs.Add("meeting: a picked meeting without its id; the next meeting");
            Pick = MeetingPick.Next;
        }
    }

    public override WidgetConfig Clone()
    {
        var copy = CopyMeetingTo(new MeetingConfig());
        copy.Pick = Pick;
        copy.Chosen = Chosen;
        return copy;
    }
}

/// <summary>
/// [S-16961] [S-16962] The Meetings list: the meetings of the chosen accounts and calendars on the item base (owner, 2026-11-23: "a
/// list of meetings that you can also filter by account, order, have them show past meetings, have them show future meetings,
/// etc. same component as the bluetooth one as well. also with spacing capability"): those coming up, past or both, how far
/// (today to two weeks), soonest or latest first, the next one left out where it stands under a Meeting; the item base's
/// style, gap, rows and parts. A placed After this becomes it, coming up, the next one left out ([S-16964]).
/// </summary>
public sealed class MeetingsConfig : MeetingsBaseConfig, IKitConfig, IItemList, IStyled
{
    /// <summary>[S-16962] The days it reaches, today counting as one: the choices.</summary>
    public static IReadOnlyList<int> Spans { get; } = [1, 2, 7, 14];

    /// <summary>[S-16962] A new list's reach: two weeks, as far as the calendars' window goes ([S-4820]).</summary>
    public const int DefaultDays = 14;

    /// <summary>[S-16962] Coming up, past or both.</summary>
    public MeetingWhen When { get; set; } = MeetingWhen.Future;

    /// <summary>[S-16962] Soonest or latest first.</summary>
    public MeetingOrder Order { get; set; } = MeetingOrder.Soonest;

    /// <summary>[S-16962] How far it reaches, in days from today (back for the past ones): one of <see cref="Spans"/>.</summary>
    public int Days { get; set; } = DefaultDays;

    /// <summary>[S-16962] The next meeting left out (it stands in a Meeting over the list): After this's own rule ([S-11408]).</summary>
    public bool AfterNext { get; set; }

    /// <summary>[S-16634] How many meetings a new card stands.</summary>
    public int Rows { get; set; } = ItemLayout.DefaultRows;

    /// <summary>[S-16673] The grid blocks between two meetings.</summary>
    public int Gap { get; set; }

    /// <summary>[S-16940] The item base's arrangement, the list first.</summary>
    public ItemStyle Style { get; set; } = ItemStyle.List;

    /// <summary>[S-16952] A table's column titles.</summary>
    public bool ColumnTitles { get; set; } = true;

    [JsonIgnore]
    ItemStyle IItemList.Arrangement => Style;

    IReadOnlyList<StyleOption> IStyled.Styles => ItemStyles.All;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (ItemStyle)value;
    }

    /// <summary>[S-16946] One face a meeting: no lines to choose.</summary>
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

    /// <summary>[S-7050] It reads the calendar accounts; the calendar's own setup rule stands ([S-1862]).</summary>
    [JsonIgnore]
    FeatureId? IKitConfig.Needs => HostFeatures.Calendar;

    /// <summary>[S-7050] Nothing to type in: the accounts decide what it reads.</summary>
    [JsonIgnore]
    bool IKitConfig.Configured => true;

    [JsonIgnore]
    IReadOnlyList<string> IKitConfig.AccountIds => [.. AccountIds];

    [JsonIgnore]
    KitShow IKitConfig.Shows => KitShow.When;

    [JsonIgnore]
    KitChoices IKitConfig.Choices => new(KitShow.When, []) { Rows = new KitRowsChoice(KitSizing.ClampRows(Rows), 1) };

    /// <summary>[S-16962] The reach held to the choices.</summary>
    [JsonIgnore]
    public int EffectiveDays => Spans.Contains(Days) ? Days : DefaultDays;

    protected override void RepairMeeting(List<string> repairs)
    {
        repairs.AddRange(ItemOrder.Repair(this, "meetings"));
        if (!Spans.Contains(Days))
        {
            repairs.Add($"meetings: {Days} days is not offered; {DefaultDays}");
            Days = DefaultDays;
        }
    }

    public override WidgetConfig Clone()
    {
        var copy = CopyMeetingTo(new MeetingsConfig());
        copy.When = When;
        copy.Order = Order;
        copy.Days = Days;
        copy.AfterNext = AfterNext;
        copy.Rows = Rows;
        copy.Gap = Gap;
        copy.Style = Style;
        copy.ColumnTitles = ColumnTitles;
        return copy;
    }
}
