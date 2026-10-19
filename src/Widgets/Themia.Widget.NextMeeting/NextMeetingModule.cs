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
using Themia.Widget.NextMeeting;

[assembly: ThemiaWidget("nextMeeting", typeof(NextMeetingModule))]

namespace Themia.Widget.NextMeeting;

/// <summary>Next meeting: the next event with a meeting link, and a Join button. SPEC.md beside this file.</summary>
public sealed class NextMeetingModule : IWidgetModule
{
    /// <summary>[S-6902] What the host knows of the widget without its UI.</summary>
    public WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("nextMeeting"),
        ConfigType = typeof(NextMeetingConfig),
        CreateDefault = () => new NextMeetingConfig(),
        Json = NextMeetingJsonContext.Default,
        Keywords = ["meeting", "join", "teams", "zoom", "google meet", "webex", "event", "calendar", "upcoming"],
        Providers = [HostProviders.Microsoft, HostProviders.Google, HostProviders.CalDav],
        Label = NextMeetingStr.Meeting_Label.Local(),
        Description = NextMeetingStr.Meeting_Desc.Local(),
        Glyph = Glyphs.Meeting,
        DefaultWidth = 320,
        DefaultHeight = 200,
        Traits = WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.CalendarMail, 0, 1),
    };

    /// <summary>[S-3719] [S-4826] Every option is on the properties menu: the widget has no settings page.</summary>
    public void MenuLines(WidgetMenuLines menu, WidgetConfig config)
        => NextMeetingMenu.Lines(menu, (NextMeetingConfig)config, SettingsHost.Office);

    public FrameworkElement CreateView(IWidgetHost host) => new NextMeetingView(host);
}

/// <summary>[S-6903] The module's own JSON metadata, with the host's options (camelCase, indented, nulls left out).</summary>
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(NextMeetingConfig))]
internal sealed partial class NextMeetingJsonContext : JsonSerializerContext;
