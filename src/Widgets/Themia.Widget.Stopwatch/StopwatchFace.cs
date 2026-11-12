using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Widget.Stopwatch;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-11302] The stopwatch on the timers' sized face ([S-11300]): the time a one-readout Readout on the
/// step its Size names, centred, the lap under way its line under, the Controls row (Start, Pause or
/// Resume as the one main action, Lap and Reset as icon buttons; at Small all three icon buttons) and
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

    /// <summary>[S-11302] The face at the stopwatch's Size.</summary>
    public static TimerSized Sized(StopwatchConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        return TimerFace.Sized(config.Size, Worst());
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
        var main = TimerFace.Longest(
            StopwatchStr.Stopwatch_Start.Text(locale), StopwatchStr.Stopwatch_Pause.Text(locale), StopwatchStr.Stopwatch_Resume.Text(locale));
        var controls = TimerFace.ControlsWidth(config.Size, [main], icons: 2);
        var none = TimerFace.Sizing(Sized(config), controls, header: false);
        var rows = new SizeSteps(none.Natural.Height, FamilyShape.Row1, 0, StopwatchConfig.MostLapRows, LapRows(config));
        return WidgetSizing.Range(
                none.Min with { Height = rows.At(0) },
                none.Natural with { Height = rows.At(LapRows(config)) },
                none.Max with { Height = rows.At(StopwatchConfig.MostLapRows) })
            .WithHeightSteps(rows);
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
