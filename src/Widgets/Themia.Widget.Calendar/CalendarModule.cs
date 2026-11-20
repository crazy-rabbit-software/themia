using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Services.Accounts;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Accounts;
using Themia.Core.Links;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.Calendar;

// [S-14900] One family: the calendar (its body: a month, a week, a day, a year or an agenda) and its header, the
// period's title and arrows, paging every calendar of the view through the view's links ([S-3626]).
[assembly: ThemiaWidgetFamily]
[assembly: ThemiaWidget("calendar", typeof(CalendarModule))]
[assembly: ThemiaWidget("calendarHeader", typeof(CalendarHeaderModule))]

namespace Themia.Widget.Calendar;

/// <summary>Calendar: the synced calendars as a month, a week, a day, an agenda or a year. SPEC.md beside this file.</summary>
public sealed class CalendarModule : IWidgetModule
{
    /// <summary>[S-2040] A click on the card opens the calendar of the account it reads, on the web ([S-2005]).</summary>
    internal static CardOpen? Opens(IReadOnlyList<string> accountIds, IReadOnlyList<AccountInfo> accounts)
        => AccountPages.Home(HostFeatures.Calendar, accountIds, accounts);

    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("calendar"),
        ConfigType = typeof(CalendarWidgetConfig),
        CreateDefault = () => new CalendarWidgetConfig(),
        Json = CalendarJsonContext.Default,
        // [S-3640] The five former calendar widgets' names and words find the one calendar.
        Keywords =
        [
            "events", "meetings", "appointments", "schedule", "outlook", "birthdays",
            "month calendar", "week calendar", "day calendar", "year calendar", "agenda", "upcoming", "next", "today",
            "timeline", "holidays", "overview",
        ],
        Providers = [HostProviders.Microsoft, HostProviders.Google, HostProviders.CalDav],
        Label = CalendarStr.Styles_Calendar_Label.Local(),
        Description = CalendarStr.Styles_Calendar_Desc.Local(),
        Glyph = Glyphs.Calendar,
        Sizing = c => CalendarSizing.Sizing((CalendarWidgetConfig)c),
        Traits = WidgetTraits.HasItems,
        Links = LinkUse.Of([LinkTopicId.SelectedDate, LinkTopicId.VisibleRange, LinkTopicId.HoveredEvent, LinkTopicId.HoveredDate], [LinkTopicId.SelectedDate, LinkTopicId.VisibleRange, LinkTopicId.HoveredEvent, LinkTopicId.HoveredDate]),
        Add = new(AddFolderId.CalendarMail, 0, 0, Popular: 3),
    };

    /// <summary>[S-3719] [S-7513] Every option is on the selection's page: the widget has no settings page.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
        => CalendarMenu.Lines(menu, (CalendarWidgetConfig)config, SettingsHost.Office);

    public FrameworkElement CreateView(IWidgetHost host) => new CalendarView(host);
}

/// <summary>
/// [S-14900] The calendar header: the period the view's calendars show, its title and back and forward, one tool line
/// ([S-14901]). It pages every calendar of the view and follows them ([S-3626]); a calendar body keeps the kind id
/// <c>calendar</c>, so a placed calendar stays one (without the line it had, [S-14903]).
/// </summary>
public sealed class CalendarHeaderModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("calendarHeader"),
        ConfigType = typeof(CalendarHeaderConfig),
        CreateDefault = () => new CalendarHeaderConfig(),
        Json = CalendarJsonContext.Default,
        Keywords = ["calendar header", "month", "arrows", "previous", "next", "today", "navigation", "period", "week", "year"],
        Label = CalendarStr.Styles_CalendarHeader_Label.Local(),
        Description = CalendarStr.Styles_CalendarHeader_Desc.Local(),
        Glyph = Glyphs.Calendar,
        Sizing = _ => CalendarSizing.HeaderSizing(),
        Traits = WidgetTraits.None,
        Links = LinkUse.Of([LinkTopicId.VisibleRange], [LinkTopicId.SelectedDate, LinkTopicId.VisibleRange]),
        Add = new(AddFolderId.CalendarMail, 0, 4),
    };

    /// <summary>[S-14902] Its one option is its style, the period it pages by: no line of its own.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
    {
    }

    public FrameworkElement CreateView(IWidgetHost host) => new CalendarHeaderView();
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(CalendarWidgetConfig))]
[JsonSerializable(typeof(CalendarHeaderConfig))]
internal sealed partial class CalendarJsonContext : JsonSerializerContext;
