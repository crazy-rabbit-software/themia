using Themia.Core.Localization;

namespace Themia.Core.Widgets;

/// <summary>[S-4421] A focus session: none, or running from a start to an end.</summary>
public readonly record struct FocusSessionState(DateTimeOffset? Started, DateTimeOffset? Ends)
{
    public static FocusSessionState Idle { get; } = new(null, null);

    public bool Running => Started is not null && Ends is not null;

    /// <summary>What is left at <paramref name="now"/>, never below zero; zero when idle.</summary>
    public TimeSpan Remaining(DateTimeOffset now)
        => Ends is { } ends && ends > now ? ends - now : TimeSpan.Zero;

    /// <summary>How much of it has gone, 0..1; 0 when idle.</summary>
    public double Progress(DateTimeOffset now)
    {
        if (Started is not { } started || Ends is not { } ends || ends <= started)
        {
            return 0;
        }

        return Math.Clamp((now - started).TotalSeconds / (ends - started).TotalSeconds, 0, 1);
    }

    /// <summary>A running session whose end has come.</summary>
    public bool Over(DateTimeOffset now) => Ends is { } ends && now >= ends;
}

/// <summary>[S-4426] What the focus widget shows at a size.</summary>
/// <param name="Button">Start/End, with its word where <paramref name="Words"/>.</param>
/// <param name="Words">The button carries its word, not only its glyph.</param>
/// <param name="Steppers">The −/+ that change the length, while idle.</param>
/// <param name="Status">The line under the time: do not disturb, or Windows' own focus.</param>
/// <param name="Ring">The progress ring around the time.</param>
public readonly record struct FocusPlan(bool Button, bool Words, bool Steppers, bool Status, bool Ring);

/// <summary>What the hold asks of Windows' do not disturb.</summary>
public enum QuietCommand
{
    None = 0,
    TurnOn = 1,
    TurnOff = 2,
}

/// <summary>
/// [S-4423] Windows' do not disturb, lent to a running focus session and handed back when it ends —
/// Karpo's <c>FocusQuiet</c>, as Windows' own Focus does it:
/// <list type="bullet">
/// <item>it is turned on when a session starts and off when it ends (runs out or is ended);</item>
/// <item>on already when the session started, it is the person's, and left on at the end;</item>
/// <item>switched by the person during the session (anywhere: the toggle widget, Windows), it is theirs
/// from then on, and the end leaves it as they set it;</item>
/// <item>where Windows has not said yet what it is, the hold waits for the answer rather than guess;</item>
/// <item>where Windows cannot switch it, the session runs without it.</item>
/// </list>
/// Pure: the service feeds it the session and Windows' reading and carries out what it returns.
/// </summary>
public sealed class FocusQuietHold
{
    private Hold _hold;

    private enum Hold
    {
        /// <summary>No session holds it.</summary>
        None,

        /// <summary>A session started before Windows said what it was: waiting for the answer.</summary>
        Asking,

        /// <summary>It was off and the session asked Windows to turn it on: waiting for the answer.</summary>
        Turning,

        /// <summary>It was off and the session turned it on: off again when the session ends.</summary>
        Ours,

        /// <summary>It was on already, Windows cannot switch it, or the person switched it: left alone.</summary>
        Theirs,
    }

    /// <summary>Whether the session turned it on and will turn it off at its end.</summary>
    public bool TurnedOn => _hold is Hold.Ours or Hold.Turning;

    /// <summary>Whether a session holds (or is waiting to hold) it.</summary>
    public bool Holding => _hold != Hold.None;

    /// <summary>
    /// The session or the reading changed. <paramref name="wanted"/>: a session runs and wants do not
    /// disturb. Returns what to ask Windows, at most once per change.
    /// </summary>
    public QuietCommand Update(bool wanted, ToggleReading reading)
    {
        if (wanted)
        {
            switch (_hold)
            {
                case Hold.None:
                    _hold = Hold.Asking;
                    return Decide(reading);

                case Hold.Asking:
                    return Decide(reading);

                case Hold.Ours when reading.On == false:
                    // Switched off by the person while the session held it: theirs now.
                    _hold = Hold.Theirs;
                    return QuietCommand.None;

                default:
                    return QuietCommand.None;
            }
        }

        var ours = _hold is Hold.Ours or Hold.Turning;
        _hold = Hold.None;
        return ours ? QuietCommand.TurnOff : QuietCommand.None;
    }

    /// <summary>
    /// Windows answered the session's <see cref="QuietCommand.TurnOn"/>: taken, it is the session's to
    /// hand back; refused, the session runs without it and leaves it alone. A reading of "off" that
    /// arrives before this answer is the state before the switch, and is not the person switching it.
    /// </summary>
    public void Settled(bool succeeded)
    {
        if (_hold == Hold.Turning)
        {
            _hold = succeeded ? Hold.Ours : Hold.Theirs;
        }
    }

    /// <summary>What it was before the session, once Windows has said: on is the person's; off, the session turns it on.</summary>
    private QuietCommand Decide(ToggleReading reading)
    {
        if (reading.Block != ToggleBlock.None && reading.Block != ToggleBlock.Reading)
        {
            // Windows cannot switch it here: the session runs without it.
            _hold = Hold.Theirs;
            return QuietCommand.None;
        }

        if (reading.On is not { } on)
        {
            return QuietCommand.None;
        }

        if (on)
        {
            _hold = Hold.Theirs;
            return QuietCommand.None;
        }

        _hold = Hold.Turning;
        return QuietCommand.TurnOn;
    }
}

/// <summary>[S-4420] The focus session's rules: its length, its time, its ladder and its words.</summary>
public static class FocusSessionRules
{
    public const int MinMinutes = 5;
    public const int MaxMinutes = 240;

    /// <summary>Windows' own focus session starts at 30 minutes.</summary>
    public const int DefaultMinutes = 30;

    /// <summary>[S-4421] Windows' Clock steps a focus session by five minutes.</summary>
    public const int StepMinutes = 5;

    public static int ClampMinutes(int minutes) => Math.Clamp(minutes, MinMinutes, MaxMinutes);

    /// <summary>[S-4421] One step longer or shorter, landing on the five-minute grid, inside the range.</summary>
    public static int Step(int minutes, int direction)
    {
        var clamped = ClampMinutes(minutes);
        if (direction == 0)
        {
            return clamped;
        }

        var onGrid = direction > 0
            ? ((clamped / StepMinutes) + 1) * StepMinutes
            : ((clamped + StepMinutes - 1) / StepMinutes - 1) * StepMinutes;
        return ClampMinutes(onGrid);
    }

    /// <summary>[S-4421] A session of <paramref name="minutes"/> from <paramref name="now"/>.</summary>
    public static FocusSessionState Start(DateTimeOffset now, int minutes)
        => new(now, now + TimeSpan.FromMinutes(ClampMinutes(minutes)));

    /// <summary>
    /// [S-4422] What is left, as the clock shows it: "24:59", "1:04:59" from an hour; a length at rest
    /// the same way ("30:00"). Whole seconds, rounded up, so the last second reads "0:01", never "0:00"
    /// while it still runs.
    /// </summary>
    public static string Clock(TimeSpan left)
    {
        var seconds = (long)Math.Ceiling(Math.Max(0, left.TotalSeconds));
        var hours = seconds / 3600;
        var minutes = seconds / 60 % 60;
        var rest = seconds % 60;
        return hours > 0
            ? $"{hours}:{minutes:00}:{rest:00}"
            : $"{seconds / 60:00}:{rest:00}";
    }

    /// <summary>The widest the clock reads for a length: what the time's room is measured for.</summary>
    public static string WidestClock(int minutes) => ClampMinutes(minutes) >= 60 ? "0:00:00" : "00:00";

    /// <summary>
    /// [S-4422] The second tick only while a session runs, on the second it changes; nothing at rest.
    /// </summary>
    public static TimeSpan? Tick(FocusSessionState state) => state.Running ? TimeSpan.FromSeconds(1) : null;

    /// <summary>
    /// [S-4426] The ladder: the time alone on the smallest tile (a click on it starts or ends); the
    /// button from 128 high; its word, the steppers and the status line as the tile grows; the ring
    /// from medium both ways.
    /// </summary>
    public static FocusPlan Plan(WidgetArea area)
    {
        if (!area.IsMeasured || (area.HeightClass == SizeClass.Compact && area.WidthClass == SizeClass.Compact))
        {
            return new FocusPlan(Button: false, Words: false, Steppers: false, Status: false, Ring: false);
        }

        var wide = area.WidthClass >= SizeClass.Regular;
        var tall = area.HeightClass >= SizeClass.Regular;
        return new FocusPlan(
            Button: true,
            Words: wide,
            Steppers: wide || tall,
            Status: tall && wide,
            Ring: area.AtLeast(SizeClass.Expanded, SizeClass.Expanded));
    }

    /// <summary>A length in words for a tooltip and screen readers ("30 min").</summary>
    public static string Length(int minutes) => Str.Panels_Focus_Minutes.Format(("minutes", LocaleFormat.Integer(ClampMinutes(minutes))));
}
