using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Accounts;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Widget.NextMeeting;

namespace Themia.Core.Model;

/// <summary>
/// [S-4820] Next meeting: which accounts and calendars it reads, as every calendar widget chooses
/// them ([S-562], [S-567]). It shares their settings builder and data seam; birthdays are all-day
/// and never a meeting, so it never asks for them.
/// </summary>
public sealed class NextMeetingConfig : CalendarConfig
{
    /// <summary>[S-1862] What its Set up row says it waits for.</summary>
    [JsonIgnore]
    public override string? SetupSentence => NextMeetingStr.NextMeeting_Setup_Sentence.Text();

    public NextMeetingConfig()
    {
        ShowBirthdays = false;
    }

    /// <summary>[S-11407] The meeting's place under its time (where it has one).</summary>
    public bool ShowPlace { get; set; } = true;

    /// <summary>[S-11407] Who organised it, under its place (where it has one).</summary>
    public bool ShowOrganizer { get; set; } = true;

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

    public override WidgetConfig Clone()
    {
        var copy = (NextMeetingConfig)CopySharedTo(new NextMeetingConfig());
        copy.ShowPlace = ShowPlace;
        copy.ShowOrganizer = ShowOrganizer;
        return copy;
    }
}
