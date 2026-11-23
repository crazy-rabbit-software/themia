using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Services.Accounts;
using Themia.App.Services.Office;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Accounts;
using Themia.Core.Links;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Office;
using Themia.Core.Settings;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Items;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Items;
using Themia.Widget.Meetings;

[assembly: ThemiaWidgetFamily]
[assembly: ThemiaWidget("meeting", typeof(MeetingModule))]
[assembly: ThemiaWidget("meetings", typeof(MeetingsModule))]

namespace Themia.Widget.Meetings;

/// <summary>
/// [S-16960] The Meeting: one meeting on the item base's face, the next one or one picked, with Join. A placed Next meeting
/// becomes it in place ([S-16964]). SPEC.md beside this file.
/// </summary>
public sealed class MeetingModule : IWidgetModule
{
    /// <summary>[S-2041] A click on the card opens the calendar of the account it reads, on the web ([S-2005]).</summary>
    internal static CardOpen? Opens(IReadOnlyList<string> accountIds, IReadOnlyList<AccountInfo> accounts)
        => AccountPages.Home(HostFeatures.Calendar, accountIds, accounts);

    /// <summary>[S-16960] The end's widest: Join, or a start on another day.</summary>
    internal const double EndWidth = 96;

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("meeting"),
        ConfigType = typeof(MeetingConfig),
        CreateDefault = () => new MeetingConfig(),
        Json = MeetingsJsonContext.Default,
        Keywords = ["meeting", "next meeting", "join", "teams", "zoom", "google meet", "webex", "event", "calendar", "upcoming"],
        Providers = [HostProviders.Microsoft, HostProviders.Google, HostProviders.CalDav],
        Label = MeetingsStr.Meetings_Single_Label.Local(),
        Description = MeetingsStr.Meetings_Single_Desc.Local(),
        Glyph = Glyphs.Meeting,
        Sizing = c => ItemLayout.Face(ItemShape.Of(MeetingItems.Parts, ((MeetingConfig)c).HiddenParts, action: true), ItemLayout.WidestLine(["in 18 min · 11:00 AM – 11:45 AM", "Tomorrow · 9:30 AM – 10:00 AM"]), EndWidth),
        Traits = WidgetTraits.HasItems,
        Links = LinkUse.Both(LinkTopicId.HoveredEvent),
        Add = new(AddFolderId.CalendarMail, 0, 1),
        Merges = [new MergeFrom("nextMeeting") { Writes = """{ "pick": "Next" }""" }],
    };

    /// <summary>[S-16960] Which meeting, what it shows, then the calendars: all on its page.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config) => MeetingsMenu.Lines(menu, (MeetingConfig)config, SettingsHost.Office);

    public FrameworkElement CreateView(IWidgetHost host) => new MeetingView(host);
}

/// <summary>
/// [S-16961] The Meetings: the meetings of the chosen calendars on the item base, coming up, past or both, with Join. A placed
/// After this becomes it in place ([S-16964]). SPEC.md beside this file.
/// </summary>
public sealed class MeetingsModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("meetings"),
        ConfigType = typeof(MeetingsConfig),
        CreateDefault = () => new MeetingsConfig(),
        Json = MeetingsJsonContext.Default,
        Keywords = ["meetings", "up next", "after", "later", "agenda", "schedule", "calendar", "today", "past", "join"],
        Providers = [HostProviders.Microsoft, HostProviders.Google, HostProviders.CalDav],
        Label = MeetingsStr.Meetings_List_Label.Local(),
        Description = MeetingsStr.Meetings_List_Desc.Local(),
        Glyph = Glyphs.Calendar,
        Sizing = c => ItemLayout.Sizing((MeetingsConfig)c, MeetingItems.Parts, action: true),
        Traits = WidgetTraits.HasItems,
        Links = LinkUse.Both(LinkTopicId.HoveredEvent),
        Add = new(AddFolderId.CalendarMail, 0, 2),
        Merges = [new MergeFrom("upNext") { Writes = """{ "afterNext": true, "style": "columnList" }""" }],
    };

    /// <summary>[S-16962] Which meetings, their order and reach, the item base's lines, then the calendars.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config) => MeetingsMenu.Lines(menu, (MeetingsConfig)config, SettingsHost.Office);

    public FrameworkElement CreateView(IWidgetHost host) => new MeetingsView(host);
}

/// <summary>
/// [S-16960] The meetings the meeting views read last (none in a render): what a Meeting's page offers to pick from, as the
/// drive list is the drives' page's.
/// </summary>
internal static class MeetingsSeen
{
    private static IReadOnlyList<CalendarEvent> _last = [];

    public static IReadOnlyList<CalendarEvent> Last
    {
        get => Volatile.Read(ref _last);
        set => Volatile.Write(ref _last, value ?? []);
    }
}

/// <summary>
/// [S-16960]..[S-16962] The meeting widgets' lines: a Meeting's which meeting, a list's show, order, how far and the next one
/// left out; the item base's parts with the place and the organiser; then the calendar accounts and calendars every
/// calendar widget chooses, without their colours ([S-4826]).
/// </summary>
internal static class MeetingsMenu
{
    public static void Lines(WidgetMenuLines menu, MeetingConfig config, IOfficeSource? office)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);
        var locale = Strings.Current;
        var now = DateTimeOffset.Now;
        var zone = TimeZoneInfo.Local;
        var options = new List<(string Value, string Text)> { (string.Empty, MeetingsStr.Meetings_Which_Next.Text()) };
        options.AddRange(MeetingItems.Upcoming(MeetingsSeen.Last, now).Take(20)
            .Select(m => (MeetingItems.Id(m), $"{(string.IsNullOrWhiteSpace(m.Subject) ? Str.Calendar_NoTitle.Text() : m.Subject)} · {NextMeetingRules.Start(locale, m, now, zone)}")));
        var held = config.Pick == MeetingPick.Chosen ? config.Chosen : string.Empty;
        if (!options.Any(o => o.Value == held))
        {
            options.Add((held, MeetingsStr.Meetings_Which_Gone.Text()));
        }

        menu.Lines.Add(menu.Choice(Glyphs.Meeting, MeetingsStr.Meetings_Which.Text(), options, held, value =>
        {
            config.Pick = value.Length > 0 ? MeetingPick.Chosen : MeetingPick.Next;
            config.Chosen = value;
        }));
        menu.Lines.Add(ItemMenuLines.Parts(menu, config, MeetingItems.Parts, Own(menu, config), p => MeetingItems.PartName(p, locale)));
        menu.Lines.Add(SettingsLine.Separator);
        menu.Lines.AddRange(OfficeMenuLines.CalendarAccounts(menu, config, office, colours: false));
    }

    public static void Lines(WidgetMenuLines menu, MeetingsConfig config, IOfficeSource? office)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);
        var locale = Strings.Current;
        menu.Lines.Add(menu.Choice(Glyphs.Calendar, MeetingsStr.Meetings_When.Text(),
        [
            (MeetingWhen.Future, MeetingsStr.Meetings_When_Future.Text()),
            (MeetingWhen.Past, MeetingsStr.Meetings_When_Past.Text()),
            (MeetingWhen.Both, MeetingsStr.Meetings_When_Both.Text()),
        ], config.When, value => config.When = value));
        menu.Lines.Add(menu.Choice(Glyphs.Sort, MeetingsStr.Meetings_Order.Text(),
        [
            (MeetingOrder.Soonest, MeetingsStr.Meetings_Order_Soonest.Text()),
            (MeetingOrder.Latest, MeetingsStr.Meetings_Order_Latest.Text()),
        ], config.Order, value => config.Order = value));
        menu.Lines.Add(menu.Choice(Glyphs.Clock, MeetingsStr.Meetings_Span.Text(),
            [.. MeetingsConfig.Spans.Select(d => (d, d == 1 ? MeetingsStr.Meetings_Span_Today.Text() : MeetingsStr.Meetings_Span_Days.Format(("count", LocaleFormat.Integer(locale, d)))))],
            config.EffectiveDays, value => config.Days = value));
        if (config.When != MeetingWhen.Past)
        {
            menu.Lines.Add(menu.Toggle(null, MeetingsStr.Meetings_AfterNext.Text(), config.AfterNext, on => config.AfterNext = on));
        }

        menu.Lines.Add(SettingsLine.Separator);
        ItemMenuLines.List(menu, config, MeetingItems.Parts, own: Own(menu, config), name: p => MeetingItems.PartName(p, locale));
        menu.Lines.Add(SettingsLine.Separator);
        menu.Lines.AddRange(OfficeMenuLines.CalendarAccounts(menu, config, office, colours: false));
    }

    /// <summary>[S-11407] A meeting's own parts in the parts' section: its place and its organiser in the note.</summary>
    private static List<SettingsLine> Own(WidgetMenuLines menu, MeetingsBaseConfig config) =>
    [
        menu.Toggle(null, MeetingsStr.NextMeeting_ShowPlace.Text(), config.ShowPlace, on => config.ShowPlace = on),
        menu.Toggle(null, MeetingsStr.NextMeeting_ShowOrganizer.Text(), config.ShowOrganizer, on => config.ShowOrganizer = on),
    ];
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(MeetingConfig))]
[JsonSerializable(typeof(MeetingsConfig))]
internal sealed partial class MeetingsJsonContext : JsonSerializerContext;
