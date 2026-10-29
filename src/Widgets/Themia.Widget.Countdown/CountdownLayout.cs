using Themia.Core.Model;
using Themia.Sdk.Kit;

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
/// [S-1447] [S-6030] The countdown's face as planned: its look, the readout's step (the cards' on the
/// Flip look), whether the line under shows (the date; the cards' units), the ring's diameter (0 off
/// the ring), whether the progress bar shows and the cards' plan.
/// </summary>
public readonly record struct CountdownPlan(CountdownLook Look, TypeStep Step, bool Under, double Ring, bool Bar, FlipPlan Flip);

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

    /// <summary>What the progress bar takes from the readout's box: the gap and the track.</summary>
    public const double BarCost = FamilySpace.S2 + FamilyShape.Track;

    /// <summary>
    /// [S-1447] A reading's shape: its figures as 8s ("3d 5h" is "8d 8h"), so the readout is sized on
    /// the widest its figures get and does not move as they tick; a new shape (a unit going at the last
    /// day) lays the face out again.
    /// </summary>
    public static string Shape(string reading) => new([.. (reading ?? string.Empty).Select(c => char.IsDigit(c) ? '8' : c)]);

    /// <summary>
    /// [S-1447] [S-6030] [S-6739] The face for <paramref name="style"/> in a view of <paramref name="area"/>
    /// (the card less its header): the duration as the Readout on the Hero steps with the target's date
    /// under it, sized on the reading's shape; Ring puts it in the Chart ring of the time left wherever
    /// a ring holds it, Flip draws the cards (<see cref="FlipRules"/>) with their units under them where
    /// the row fits, and either falls back to the text. The progress bar (<paramref name="progress"/>:
    /// there is a share to draw) stands 8 under the text on L and XL, never under a Ring style's face.
    /// No Controls row: a countdown has nothing to start or stop.
    /// </summary>
    public static CountdownPlan Plan(CountdownStyle style, WidgetArea area, string reading, IReadOnlyList<CountdownCard> cards, bool progress)
    {
        ArgumentNullException.ThrowIfNull(cards);
        var width = Math.Max(0, area.Width - (2 * FamilySpace.Inset));
        var height = Math.Max(0, area.Height - FamilySpace.Inset);
        var worst = new ReadoutText(Shape(reading), LineUnder: true, OneReadout: true);

        if (style == CountdownStyle.Ring)
        {
            var ring = TimerFace.Ring(area, width, height, worst, controls: false);
            if (ring.Ring > 0)
            {
                return new CountdownPlan(CountdownLook.Ring, ring.Step, ring.Under, ring.Ring, false, default);
            }
        }

        if (style == CountdownStyle.Flip && cards.Count > 0)
        {
            var widest = new string('8', Math.Max(2, cards.Max(c => c.Digits.Length)));
            var flip = FlipRules.Plan(area, width, height, cards.Count, widest, oneReadout: true, under: true);
            if (FlipRules.RowWidth(flip.Step, cards.Count, widest) <= width && flip.CardHeight <= height)
            {
                return new CountdownPlan(CountdownLook.Flip, flip.Step, flip.Under, 0, false, flip);
            }
        }

        var bar = progress && style != CountdownStyle.Ring && ShowsProgress(area)
            && ReadoutRules.Height(FamilyType.Big28, worst with { LineUnder = false }) + BarCost <= height;
        var text = TimerFace.Plan(area, width, height - (bar ? BarCost : 0), worst, controls: false);
        return new CountdownPlan(CountdownLook.Text, text.Step, text.Under, 0, bar, default);
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
