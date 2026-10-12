using System.Text.Json.Serialization;

namespace Themia.Core.Model;

/// <summary>
/// [S-4800] Year progress. Nothing to set: the week start and the clock are every widget's regional
/// settings ([S-3770]), so the widget has no settings page ([S-1750]).
/// </summary>
public sealed class YearProgressConfig : WidgetConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.YearProgress;

    /// <summary>[S-4804] A minute: the day bar moves by a fraction of a percent per minute, nothing faster.</summary>
    [JsonIgnore]
    public TimeSpan TickInterval => TimeSpan.FromMinutes(1);

    public override WidgetConfig Clone() => new YearProgressConfig();
}

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

    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.NextMeeting;

    /// <summary>What is coming up, reduced to its first entry: the agenda's shape.</summary>
    [JsonIgnore]
    public override CalendarLayout Layout => CalendarLayout.Agenda;

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

/// <summary>[S-4840] Recent files. Nothing to set: the list is Windows' own Recent Items.</summary>
public sealed class RecentFilesConfig : WidgetConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.RecentFiles;

    /// <summary>[S-4843] Keeps "2h" honest; the list itself redraws from the folder watch.</summary>
    [JsonIgnore]
    public TimeSpan TickInterval => TimeSpan.FromMinutes(1);

    public override WidgetConfig Clone() => new RecentFilesConfig();
}

/// <summary>[S-4860] Recycle Bin. Nothing to set.</summary>
public sealed class RecycleBinConfig : WidgetConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.RecycleBin;

    public override WidgetConfig Clone() => new RecycleBinConfig();
}
