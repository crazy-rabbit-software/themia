using System.Text.Json.Serialization;
using Microsoft.UI.Xaml;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.App.Widgets;
using Themia.Core.Accounts;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;
using Themia.Widget.Calendar;

[assembly: ThemiaWidget("calendar", typeof(CalendarModule))]

namespace Themia.Widget.Calendar;

/// <summary>Calendar: the synced calendars as a month, a week, a day, an agenda or a year. SPEC.md beside this file.</summary>
public sealed class CalendarModule : IWidgetModule
{
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
            "events", "meetings", "appointments", "schedule", "outlook", "google calendar", "birthdays",
            "month calendar", "week calendar", "day calendar", "year calendar", "agenda", "upcoming", "next", "today",
            "timeline", "holidays", "overview",
        ],
        Providers = [HostProviders.Microsoft, HostProviders.Google, HostProviders.CalDav],
        Label = CalendarStr.Styles_Calendar_Label.Local(),
        Description = CalendarStr.Styles_Calendar_Desc.Local(),
        Glyph = Glyphs.Calendar,
        DefaultWidth = 340,
        DefaultHeight = 260,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.CalendarMail, 0, 0, Popular: 3),
    };

    public void SettingsRows(WidgetSettingsRows rows, WidgetConfig config)
        => CalendarSettings.Rows(rows, (CalendarWidgetConfig)config);

    public FrameworkElement CreateView(IWidgetHost host) => new CalendarView(host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(CalendarWidgetConfig))]
internal sealed partial class CalendarJsonContext : JsonSerializerContext;
