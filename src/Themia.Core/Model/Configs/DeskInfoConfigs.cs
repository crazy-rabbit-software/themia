using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>[S-6040] How Year progress draws its spans: bars, rings, or the year as a dot per day.</summary>
[JsonConverter(typeof(Themia.Core.Widgets.StyleJsonConverter<YearProgressStyle>))]
public enum YearProgressStyle
{
    Bars = 0,
    Rings = 1,
    Dots = 2,
}

/// <summary>
/// [S-4800] Year progress. Its one setting is its style ([S-6040]): the week start and the clock
/// are every widget's regional settings ([S-3770]).
/// </summary>
public sealed class YearProgressConfig : WidgetConfig, Themia.Core.Widgets.IStyled
{
    private static readonly Themia.Core.Widgets.StyleOption[] YearStyles =
    [
        Themia.Core.Widgets.StyleOption.Of(YearProgressStyle.Bars, Themia.Core.Localization.Str.Styles_YearProgress_Bars, 0xE7A5),
        Themia.Core.Widgets.StyleOption.Of(YearProgressStyle.Rings, Themia.Core.Localization.Str.Styles_YearProgress_Rings, 0xEA3A),
        Themia.Core.Widgets.StyleOption.Of(YearProgressStyle.Dots, Themia.Core.Localization.Str.Styles_YearProgress_Dots, 0xE75F),
    ];


    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("yearProgress"),
        ConfigType = typeof(YearProgressConfig),
        CreateDefault = () => new YearProgressConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["progress", "percent", "year", "month", "week", "day", "time passed", "elapsed"],
        LabelId = Str.YearProgress_Label,
        DescriptionId = Str.YearProgress_Desc,
        Glyph = Glyphs.YearProgress,
        DefaultWidth = 280,
        DefaultHeight = 220,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.Time, 2, 0),
    };

    /// <summary>[S-6040] Bars, rings or dots.</summary>
    public YearProgressStyle Style { get; set; } = YearProgressStyle.Bars;

    IReadOnlyList<Themia.Core.Widgets.StyleOption> Themia.Core.Widgets.IStyled.Styles => YearStyles;

    int Themia.Core.Widgets.IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (YearProgressStyle)value;
    }

    /// <summary>[S-4804] A minute: the day bar moves by a fraction of a percent per minute, nothing faster.</summary>
    [JsonIgnore]
    public TimeSpan TickInterval => TimeSpan.FromMinutes(1);

    public override WidgetConfig Clone() => new YearProgressConfig { Style = Style };
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


    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("nextMeeting"),
        ConfigType = typeof(NextMeetingConfig),
        CreateDefault = () => new NextMeetingConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["meeting", "join", "teams", "zoom", "google meet", "webex", "event", "calendar", "upcoming"],
        ProviderNames = ["Microsoft", "Google", "CalDAV"],
        LabelId = Str.Meeting_Label,
        DescriptionId = Str.Meeting_Desc,
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

/// <summary>[S-4840] Recent files. Nothing to set: the list is Windows' own Recent Items.</summary>
public sealed class RecentFilesConfig : WidgetConfig
{

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("recentFiles"),
        ConfigType = typeof(RecentFilesConfig),
        CreateDefault = () => new RecentFilesConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["recent", "recently opened", "history", "documents", "files", "quick access", "last opened"],
        LabelId = Str.RecentFiles_Label,
        DescriptionId = Str.RecentFiles_Desc,
        Glyph = Glyphs.RecentFiles,
        DefaultWidth = 320,
        DefaultHeight = 300,
        Traits = WidgetTraits.HasItems | WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.Files, 0, 2),
    };

    /// <summary>[S-4843] Keeps "2h" honest; the list itself redraws from the folder watch.</summary>
    [JsonIgnore]
    public TimeSpan TickInterval => TimeSpan.FromMinutes(1);

    public override WidgetConfig Clone() => new RecentFilesConfig();
}

/// <summary>[S-4860] Recycle Bin. Nothing to set.</summary>
public sealed class RecycleBinConfig : WidgetConfig
{

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("recycleBin"),
        ConfigType = typeof(RecycleBinConfig),
        CreateDefault = () => new RecycleBinConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["trash", "bin", "deleted", "empty", "restore", "wastebasket", "deleted files"],
        LabelId = Str.WidgetsC_Location_RecycleBin,
        DescriptionId = Str.RecycleBin_Desc,
        Glyph = Glyphs.RecycleBin,
        DefaultWidth = 200,
        DefaultHeight = 200,
        Traits = WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.Files, 0, 3),
    };

    public override WidgetConfig Clone() => new RecycleBinConfig();
}
