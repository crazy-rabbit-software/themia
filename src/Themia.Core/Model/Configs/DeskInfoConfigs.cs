using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Accounts;
using Themia.Core.Localization;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>
/// [S-4820] Next meeting: which accounts and calendars it reads, as every calendar widget chooses
/// them ([S-562], [S-567]). It shares their settings builder and data seam; birthdays are all-day
/// and never a meeting, so it never asks for them.
/// </summary>
public sealed class NextMeetingConfig : CalendarConfig
{
    public NextMeetingConfig()
    {
        ShowBirthdays = false;
    }


    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("nextMeeting"),
        ConfigType = typeof(NextMeetingConfig),
        CreateDefault = () => new NextMeetingConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["meeting", "join", "teams", "zoom", "google meet", "webex", "event", "calendar", "upcoming"],
        Providers = [HostProviders.Microsoft, HostProviders.Google, HostProviders.CalDav],
        Label = Str.Meeting_Label.Local(),
        Description = Str.Meeting_Desc.Local(),
        Glyph = Glyphs.Meeting,
        DefaultWidth = 320,
        DefaultHeight = 200,
        Traits = WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.CalendarMail, 0, 1),
    };

    /// <summary>What is coming up, reduced to its first entry: the agenda's shape.</summary>
    [JsonIgnore]
    public override CalendarStyle Layout => CalendarStyle.Agenda;

    protected override void RepairOwn(List<string> repairs)
    {
        if (ShowBirthdays)
        {
            repairs.Add("next meeting never shows birthdays");
            ShowBirthdays = false;
        }
    }

    public override WidgetConfig Clone() => CopySharedTo(new NextMeetingConfig());
}
