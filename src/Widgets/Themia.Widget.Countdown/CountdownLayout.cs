using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Widget.Countdown;

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

/// <summary>[S-6030] What a countdown tile draws: the text, the ring with the text in it, or the flip cards.</summary>
public enum CountdownLook
{
    Text,
    Ring,
    Flip,
}

/// <summary>
/// [S-11303] The countdown's face at its Size: its look, the readout's step (the cards' on the Flip
/// look), the ring's diameter (0 off the ring), the cards' plan, and the face's own width and height.
/// </summary>
public readonly record struct CountdownPlan(CountdownLook Look, TypeStep Step, double Ring, FlipPlan Flip, double Width, double Height);

/// <summary>
/// The Countdown widget's face and the rules behind its readout: how far along it is, the widest
/// its reading gets, its ring and its flip cards. Pure; the view only draws.
/// </summary>
public static class CountdownLayout
{
    /// <summary>What the progress bar takes under the face: the gap and the track.</summary>
    public const double BarCost = FamilySpace.S2 + FamilyShape.Track;

    /// <summary>The figures a day count is sized on: up to 999 days.</summary>
    private const long WidestDays = 888;

    /// <summary>
    /// [S-1447] A reading's shape: its figures as 8s ("3d 5h" is "8d 8h"), as the widest reading is
    /// measured.
    /// </summary>
    public static string Shape(string reading) => new([.. (reading ?? string.Empty).Select(c => char.IsDigit(c) ? '8' : c)]);

    /// <summary>
    /// [S-11303] The widest reading <paramref name="format"/> writes in <paramref name="locale"/>, every
    /// figure an 8, up to 999 days, ahead or gone by ("888 days ago"): the readout is sized on it, so the
    /// card does not move as the time left changes.
    /// </summary>
    public static string Widest(CountdownFormat format, Locale locale)
    {
        List<(string Text, long? Days)> ahead = format switch
        {
            CountdownFormat.Days => [(CountdownStr.WidgetsA_Countdown_Days.Plural(locale, WidestDays), WidestDays)],
            CountdownFormat.DaysHoursMinutes => [(CountdownStr.WidgetsA_Countdown_Compact_Dhm.Format(locale, ("days", WidestDays), ("hours", 88), ("minutes", 88)), null)],
            CountdownFormat.DaysHoursMinutesSeconds => [(CountdownStr.WidgetsA_Countdown_Compact_Dhms.Format(locale, ("days", WidestDays), ("hours", 88), ("minutes", 88), ("seconds", 88)), null)],
            _ =>
            [
                (CountdownStr.WidgetsA_Countdown_InAMoment.Text(locale), null),
                (CountdownStr.WidgetsA_Countdown_JustNow.Text(locale), null),
                (CountdownStr.WidgetsA_Countdown_Minutes.Plural(locale, 88), null),
                (CountdownStr.WidgetsA_Countdown_Hours.Plural(locale, 88), null),
                (CountdownStr.WidgetsA_Countdown_Compact_Hm.Format(locale, ("hours", 88), ("minutes", 88)), null),
                (CountdownStr.WidgetsA_Countdown_Compact_Dh.Format(locale, ("days", 8), ("hours", 88)), null),
                (CountdownStr.WidgetsA_Countdown_Days.Plural(locale, WidestDays), WidestDays),
            ],
        };

        var all = ahead.Select(a => a.Text)
            .Concat(ahead.Select(a => a.Days is { } days
                ? CountdownStr.WidgetsA_Countdown_DaysAgo.Plural(locale, days)
                : CountdownStr.WidgetsA_Countdown_Ago.Format(locale, ("duration", a.Text))));
        return Shape(all.OrderByDescending(HeroFit.Ems).First());
    }

    /// <summary>[S-11303] The Flip style's cards at their widest: a days card of three figures, hours, minutes and the seconds where the format shows them.</summary>
    public static int WidestCards(CountdownFormat format) => format == CountdownFormat.DaysHoursMinutesSeconds ? 4 : 3;

    /// <summary>The figures every card is sized on: three, as the days card's widest.</summary>
    public const string CardFigures = "888";

    /// <summary>
    /// [S-11303] The face at <paramref name="config"/>'s Size: the Text style the reading on the Size's step
    /// (40, 68, 96) with the target's date under it; Ring that reading one step under in the least ring
    /// that holds it ([S-11300]); Flip the cards one step under, each as wide as three figures, their units
    /// under them. The progress bar is not part of the face.
    /// </summary>
    public static CountdownPlan Plan(CountdownConfig config, Locale locale)
    {
        ArgumentNullException.ThrowIfNull(config);
        var worst = new ReadoutText(Widest(config.Format, locale), LineUnder: true, OneReadout: true);
        switch (config.Style)
        {
            case CountdownStyle.Ring:
            {
                var ring = TimerFace.Sized(config.Size, worst, ring: true);
                return new CountdownPlan(CountdownLook.Ring, ring.Step, ring.Ring, default, ring.Width, ring.Height);
            }

            case CountdownStyle.Flip:
            {
                var step = TimerFace.RingStep(config.Size);
                var cards = WidestCards(config.Format);
                var flip = new FlipPlan(step, FlipRules.CardWidth(step, CardFigures), Under: true);
                return new CountdownPlan(CountdownLook.Flip, step, 0, flip, FlipRules.RowWidth(step, cards, CardFigures), step.LineHeight + FamilyType.Under(step).LineHeight);
            }

            default:
            {
                var text = TimerFace.Sized(config.Size, worst);
                return new CountdownPlan(CountdownLook.Text, text.Step, 0, default, text.Width, text.Height);
            }
        }
    }

    /// <summary>
    /// [S-11303] The countdown's sizing ([S-2438]): its face at its Size with the bar under it where it is
    /// on, no Controls row (a countdown has nothing to start or stop), under its header (the event's name,
    /// on by default); one height, up to half again wider.
    /// </summary>
    public static WidgetSizing Sizing(CountdownConfig config, Locale locale)
    {
        ArgumentNullException.ThrowIfNull(config);
        var plan = Plan(config, locale);
        return TimerFace.Sizing(new TimerSized(plan.Step, plan.Ring, plan.Width, plan.Height), controls: 0, header: true, extra: config.DrawsProgress ? BarCost : 0);
    }

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
