using Themia.Core.Model;

namespace Themia.Core.Widgets;

/// <summary>[S-6032] What one flip card counts.</summary>
public enum CountdownUnit
{
    Days = 0,
    Hours = 1,
    Minutes = 2,
    Seconds = 3,
}

/// <summary>[S-6032] One split-flap card of the Flip style: its figures and what they count.</summary>
public readonly record struct CountdownCard(string Digits, CountdownUnit Unit);

/// <summary>
/// The Countdown widget's ladder and the rules behind its readout: how far along it is, the widest
/// its duration gets, its ring and its flip cards. Pure; the view only measures.
/// </summary>
public static class CountdownLayout
{
    /// <summary>
    /// [S-1447] The progress bar under the readout (time gone from when the target was set to the
    /// target) is a rung of the large tiles alone: Expanded both ways.
    /// </summary>
    public static bool ShowsProgress(WidgetArea area) => area.AtLeast(SizeClass.Expanded, SizeClass.Expanded);

    /// <summary>
    /// [S-1448] How far along the countdown is, 0 to 1, from when its target was set to the target;
    /// null when that cannot be said (no target, no creation time, or one at or after the target).
    /// </summary>
    public static double? Progress(CountdownConfig config, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (config.Target is not { } target || config.Created == default || config.Created >= target)
        {
            return null;
        }

        var whole = (target - config.Created).TotalSeconds;
        var gone = (now - config.Created).TotalSeconds;

        return Math.Clamp(gone / whole, 0, 1);
    }

    /// <summary>
    /// [S-1447] The widest a duration in <paramref name="format"/> gets, in characters, so the hero's
    /// type is sized once for the format rather than hopping as the digits tick ("10h" → "9h").
    /// </summary>
    public static int HeroCharacters(CountdownFormat format, bool elapsed)
    {
        var widest = format switch
        {
            CountdownFormat.Days => 9,
            CountdownFormat.DaysHoursMinutes => 11,
            CountdownFormat.DaysHoursMinutesSeconds => 15,
            _ => 9,
        };

        return elapsed ? widest + 4 : widest;
    }

    /// <summary>
    /// [S-6031] The share still to go, 1 when the target was set and 0 at the target: what the Ring
    /// style draws. Null when that cannot be said ([S-1448]); the ring then draws its track alone.
    /// </summary>
    public static double? Left(CountdownConfig config, DateTimeOffset now)
        => Progress(config, now) is { } gone ? 1 - gone : null;

    /// <summary>
    /// [S-6032] The Flip style's cards for the time between <paramref name="now"/> and
    /// <paramref name="target"/>, either way round (a target gone by counts up, as its text does).
    /// Days only while there is at least one, then hours and minutes, two figures each, and the
    /// seconds when the timer's format shows them. ASCII figures in every language, as the clock's
    /// flip cards: a split-flap shows digits.
    /// </summary>
    public static IReadOnlyList<CountdownCard> Cards(DateTimeOffset target, DateTimeOffset now, bool seconds)
    {
        var total = (long)Math.Floor((target - now).Duration().TotalSeconds);
        var days = total / 86400;
        var cards = new List<CountdownCard>(4);

        if (days > 0)
        {
            cards.Add(new CountdownCard(days.ToString(System.Globalization.CultureInfo.InvariantCulture), CountdownUnit.Days));
        }

        cards.Add(new CountdownCard(Two(total / 3600 % 24), CountdownUnit.Hours));
        cards.Add(new CountdownCard(Two(total / 60 % 60), CountdownUnit.Minutes));

        if (seconds)
        {
            cards.Add(new CountdownCard(Two(total % 60), CountdownUnit.Seconds));
        }

        return cards;

        static string Two(long value) => value.ToString("00", System.Globalization.CultureInfo.InvariantCulture);
    }
}
