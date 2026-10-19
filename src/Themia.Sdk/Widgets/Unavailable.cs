using Themia.App.Services;
using Themia.Core.SystemData;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-1940] [S-1941] A widget whose subject is not on this machine (no battery, no display brightness,
/// no output device, no adapter) draws its own content in the disabled look, never an empty state in
/// its place. These are the figures that content stands at: a level is drawn at the middle, a number
/// is never made up (it reads "--", [S-82]).
/// </summary>
public static class Unavailable
{
    /// <summary>[S-1941] Where a level stands while there is none: the middle, claiming neither empty nor full.</summary>
    public const double Share = 0.5;

    /// <summary>[S-1941] The same on a 0..100 control (a slider).</summary>
    public const int Percent = 50;

    /// <summary>
    /// [S-1941] The share a level part draws for a reading (a battery's fill, a ring's sweep, a gauge):
    /// its own while it is available, the middle while the thing measured is not on this machine, none
    /// while it is there but cannot be read right now ([S-82]: an empty part at once).
    /// </summary>
    public static double? Fraction(MeterReading reading)
    {
        if (reading.Available)
        {
            return reading.Fraction is { } f && double.IsFinite(f) ? Math.Clamp(f, 0, 1) : null;
        }

        return reading.Absent ? Share : null;
    }

    /// <summary>[S-1941] A slider's position: the level while there is one (clamped to 0..100), else the middle.</summary>
    public static double Slider(double? percent)
        => percent is { } p && double.IsFinite(p) ? Math.Clamp(p, 0, 100) : Percent;

    /// <summary>
    /// [S-1941] A meter's history for a thing not on this machine: a flat line at the middle across the
    /// whole chart, so the chart is drawn (dimmed) where it would be, never an empty band.
    /// </summary>
    public static IReadOnlyList<SeriesPoint> History(int points)
        => [.. Enumerable.Repeat(new SeriesPoint(Share), Math.Max(0, points))];

    /// <summary>
    /// [S-1942] A detail sheet for a thing that is not on this machine: the hero "--", the facts the widget
    /// shows when it is there, each "--", all dimmed, the reason for the tooltip. Same ladder, same parts.
    /// </summary>
    public static DetailSheet Sheet(string note, IEnumerable<string> labels)
    {
        ArgumentNullException.ThrowIfNull(labels);
        return new DetailSheet("--", null, [.. labels.Select(DetailFact.Unavailable)], [], Absent: true, Note: note, HeroAvailable: false);
    }
}
