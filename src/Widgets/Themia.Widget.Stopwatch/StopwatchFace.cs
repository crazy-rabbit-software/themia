using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Widget.Stopwatch;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-11302] The stopwatch on the timers' sized face ([S-11300]): the time a one-readout Readout on the
/// family's one-readout step, centred, the lap under way its line under, the Controls row (Start, Pause or
/// Resume as the one main action, Lap and Reset as icon buttons) and
/// under it the newest laps, as many one-line rows as the config lists. The face is sized on the widest
/// the time is drawn: under an hour with its tenth ("88:88.8"), from an hour without it ("88:88:88").
/// </summary>
public static class StopwatchFace
{
    /// <summary>The widest reading of the hour: every figure an 8 with its tenth ("88:88.8", "88:88:88.8" from an hour), or without it.</summary>
    public static string Widest(bool hours, bool tenths = true)
    {
        var widest = hours ? StopwatchText.Widest : StopwatchText.Widest[3..];
        return tenths ? widest : widest[..^2];
    }

    /// <summary>[S-11302] Whether a stopped time shows its tenth: under an hour; from an hour the time is wider by its hours instead.</summary>
    public static bool Tenths(bool hours) => !hours;

    /// <summary>[S-11302] The widest the face draws: the wider of a time under an hour with its tenth and one from an hour without it.</summary>
    public static ReadoutText Worst()
    {
        var under = new ReadoutText(Widest(hours: false, tenths: true), LineUnder: true, OneReadout: true);
        var over = under with { Value = Widest(hours: true, tenths: false) };
        return HeroFit.Ems(over.Value) > HeroFit.Ems(under.Value) ? over : under;
    }

    /// <summary>[S-11302] The face.</summary>
    public static TimerSized Sized(StopwatchConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return TimerFace.Sized(Worst());
    }

    /// <summary>[S-11302] The lap rows the config lists, held to 0..<see cref="StopwatchConfig.MostLapRows"/>.</summary>
    public static int LapRows(StopwatchConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return Math.Clamp(config.LapRows, 0, StopwatchConfig.MostLapRows);
    }

    /// <summary>
    /// [S-11302] The stopwatch's sizing ([S-2438]): its face over the Controls row (the main action at
    /// its longest label, Lap and Reset), then a one-line row (28) per lap it lists, stepped by a row and
    /// following <see cref="StopwatchConfig.LapRows"/>; up to half again wider. No header by default.
    /// </summary>
    public static WidgetSizing Sizing(StopwatchConfig config, Locale locale)
    {
        ArgumentNullException.ThrowIfNull(config);
        var none = Face(config, locale);

        // [S-14100] The face with the card's inset over it, one band of whole cells, then a lap row each, whole cells, to
        // the card's foot: the card is exactly its face's band and its rows.
        var rows = SizeSteps.InCells(FamilyShape.Row1, 0, StopwatchConfig.MostLapRows, LapRows(config), FamilySpace.Inset + FaceHeight(config, locale));
        return WidgetSizing.Range(
                none.Min with { Height = rows.At(0) },
                none.Natural with { Height = rows.At(LapRows(config)) },
                none.Max with { Height = rows.At(StopwatchConfig.MostLapRows) })
            .WithHeightSteps(rows);
    }

    /// <summary>[S-11302] The face's card without laps: the reading over its Controls row, the inset round them.</summary>
    private static WidgetSizing Face(StopwatchConfig config, Locale locale)
    {
        var main = TimerFace.Longest(
            StopwatchStr.Stopwatch_Start.Text(locale), StopwatchStr.Stopwatch_Pause.Text(locale), StopwatchStr.Stopwatch_Resume.Text(locale));
        var controls = TimerFace.ControlsWidth([main], icons: 2);
        return TimerFace.Sizing(Sized(config), controls, header: false);
    }

    /// <summary>
    /// [S-12834] [S-14100] The face's block over the laps as the sizing states it (the reading and its Controls row, the
    /// card's inset off its top and foot): the view lays it in that band grown to a grid line, the laps under it.
    /// </summary>
    public static double FaceHeight(StopwatchConfig config, Locale locale)
    {
        ArgumentNullException.ThrowIfNull(config);
        return Face(config, locale).Natural.Height - (2 * FamilySpace.Inset);
    }

    /// <summary>[S-11302] The config a stopwatch dragged to <paramref name="size"/> means: as many lap rows as its height holds.</summary>
    public static WidgetConfig SizeSets(StopwatchConfig config, SizeDip size)
    {
        ArgumentNullException.ThrowIfNull(config);
        var rows = Sizing(config, Strings.Current).HeightSteps!.Nearest(size.Height);
        if (rows == LapRows(config))
        {
            return config;
        }

        var next = (StopwatchConfig)config.Clone();
        next.LapRows = rows;
        return next;
    }
}
