using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Storage;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>[S-6408] The Habits widget's styles: the same list and log, three ways of drawing them.</summary>
[JsonConverter(typeof(StyleJsonConverter<HabitTrackerStyle>))]
public enum HabitTrackerStyle
{
    /// <summary>A row per habit: box, name, run and history dots ([S-1427]).</summary>
    Dots = 0,

    /// <summary>[S-6409] A calendar grid of the last weeks, per habit or for the whole list, by size.</summary>
    Heatmap = 1,

    /// <summary>[S-6410] The current period only: each name with its box, nothing else.</summary>
    Checklist = 2,
}

/// <summary>[S-6074] The Habits rows that only some styles or periods use.</summary>
public enum HabitsSetting
{
    /// <summary>The custom period's minutes: only under the custom period.</summary>
    Interval,

    /// <summary>The streak: the dots style's run.</summary>
    Streak,

    /// <summary>The history dots: the dots style's strip.</summary>
    History,
}

/// <summary>
/// [S-1420] Which habit list a Habits widget shows, and how.
/// <para>
/// The habits themselves are not here. They live in the device-wide pool beside the state file
/// (<c>habits.json</c>, <see cref="Themia.Core.Habits.HabitListStore"/>), and this config only
/// points into it: two widgets can track the same list, a list outlives the widget that made it, and
/// copying a widget copies the pointer, never a second diverging copy of the history.
/// </para>
/// </summary>
public sealed class HabitTrackerConfig : WidgetConfig, IStyled, IShownSettings<HabitsSetting>
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.HabitTracker;

    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("habitTracker"),
        Kind = WidgetKind.HabitTracker,
        ConfigType = typeof(HabitTrackerConfig),
        CreateDefault = () => new HabitTrackerConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["streaks", "daily", "routine", "goals", "checklist", "tracker"],
        LabelId = Str.WidgetsA_Catalog_Habits_Label,
        DescriptionId = Str.WidgetsA_Catalog_Habits_Description,
        Glyph = Glyphs.Habit,
        DefaultWidth = 320,
        DefaultHeight = 240,
    };

    /// <summary>
    /// The list in the pool, or empty for "not chosen yet" — the widget then takes the device's
    /// first list, creating one only if there is none. A list that was deleted is not replaced:
    /// the widget asks for one rather than showing somebody else's habits.
    /// </summary>
    public string ListId { get; set; } = string.Empty;

    /// <summary>[S-6408] Dots, heatmap or checklist: the widget's style.</summary>
    public HabitTrackerStyle Style { get; set; } = HabitTrackerStyle.Dots;

    private static readonly StyleOption[] HabitStyles =
    [
        StyleOption.Of(HabitTrackerStyle.Dots, Str.Styles_Habits_Dots, 0xF127),
        StyleOption.Of(HabitTrackerStyle.Heatmap, Str.Styles_Habits_Heatmap, 0xF232),
        StyleOption.Of(HabitTrackerStyle.Checklist, Str.Styles_Habits_Checklist, 0xE9D5),
    ];

    IReadOnlyList<StyleOption> IStyled.Styles => HabitStyles;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (HabitTrackerStyle)value;
    }

    /// <summary>
    /// [S-6408] [S-6074] The rows the page offers: the custom minutes under the custom period, and
    /// the streak and history only on the dots, the one style that draws a run and a strip. The
    /// values are kept for when the period or the style comes back.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<HabitsSetting> ShownSettings
    {
        get
        {
            var shown = new List<HabitsSetting>();
            if (Cadence.Period == HabitPeriod.Custom)
            {
                shown.Add(HabitsSetting.Interval);
            }

            if (Style == HabitTrackerStyle.Dots)
            {
                shown.Add(HabitsSetting.Streak);
                shown.Add(HabitsSetting.History);
            }

            return shown;
        }
    }

    /// <summary>[S-1422] How often the boxes clear. Per widget, as in the original: the log keeps every cadence's keys.</summary>
    public HabitPeriod Period { get; set; } = HabitPeriod.Daily;

    /// <summary>[S-1422] The custom window, in minutes (1–1440). Read through <see cref="Cadence"/>, which clamps.</summary>
    public int CustomMinutes { get; set; } = HabitCadence.DefaultCustomMinutes;

    /// <summary>
    /// [S-1426] How many past periods each row shows as dots. Zero removes the strip; a narrow tile
    /// shows fewer, dropping the oldest first.
    /// </summary>
    public int HistoryDots { get; set; } = 7;

    /// <summary>Show the current run beside each habit.</summary>
    public bool ShowStreak { get; set; } = true;

    [JsonIgnore]
    public int EffectiveHistoryDots => Math.Clamp(HistoryDots, 0, HabitRules.MaxHistory);

    [JsonIgnore]
    public HabitCadence Cadence => new(Enum.IsDefined(Period) ? Period : HabitPeriod.Daily, CustomMinutes);

    /// <summary>[S-1422] 15 s for the short cadences, a minute otherwise: only so a past period's tick clears by itself.</summary>
    [JsonIgnore]
    public TimeSpan TickInterval => HabitPeriods.TickInterval(Cadence.Period);

    public override WidgetConfig Clone() => new HabitTrackerConfig
    {
        Style = Style,
        ListId = ListId,
        Period = Period,
        CustomMinutes = CustomMinutes,
        HistoryDots = HistoryDots,
        ShowStreak = ShowStreak,
    };

    public override IReadOnlyList<string> Repair()
    {
        var repairs = new List<string>();

        if (!Enum.IsDefined(Period))
        {
            repairs.Add($"habit period {(int)Period} is unknown; reset to daily");
            Period = HabitPeriod.Daily;
        }

        if (CustomMinutes != Cadence.Minutes)
        {
            repairs.Add($"habit interval {CustomMinutes} min is out of range; clamped to {Cadence.Minutes}");
            CustomMinutes = Cadence.Minutes;
        }

        if (HistoryDots != EffectiveHistoryDots)
        {
            repairs.Add($"habit history {HistoryDots} is out of range; clamped to {EffectiveHistoryDots}");
            HistoryDots = EffectiveHistoryDots;
        }

        return repairs;
    }
}
