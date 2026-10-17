namespace Themia.Core.Widgets;

/// <summary>How often the quote changes. A closed set, because it is persisted.</summary>
public enum QuoteCadence
{
    /// <summary>Only when the widget is clicked.</summary>
    Manual = 0,

    /// <summary>On the hour.</summary>
    Hourly = 1,

    /// <summary>At local midnight. The original widget's "quote of the day".</summary>
    Daily = 2,
}

/// <summary>A line and whoever said it.</summary>
/// <param name="Text">The quote itself, without decorative quotation marks — the view draws those.</param>
/// <param name="Author">Who said it. May be empty.</param>
public readonly record struct QuoteLine(string Text, string Author);

/// <summary>
/// Which quote to show, and when it changes.
/// <para>
/// Deliberately a pure function of (how many quotes, which period, the seed): no clock is read
/// here and no random number generator is kept. The view passes the period it computed and the
/// number of times the user has clicked, so the rule is reproducible — the same inputs give the
/// same quote on every machine, on every restart, and in a test.
/// </para>
/// <para>
/// Offline by construction: the quotes are the user's own list, with the bundled set below as the
/// starting point. Nothing here reaches the network.
/// </para>
/// </summary>
public static class QuoteRotation
{
    /// <summary>
    /// The set a new widget starts with. Short, attributable lines that are worth rereading, which
    /// is the whole brief for this widget.
    /// <para>
    /// [S-1495] The original's sixteen, word for word and in its order, so a daily, unshuffled
    /// rotation shows the quote the original showed that day (<c>QUOTES[localDay % 16]</c>).
    /// </para>
    /// </summary>
    public static IReadOnlyList<QuoteLine> Bundled { get; } =
    [
        new("The secret of getting ahead is getting started.", "Mark Twain"),
        new("Simplicity is the ultimate sophistication.", "Leonardo da Vinci"),
        new("Well done is better than well said.", "Benjamin Franklin"),
        new("What we know is a drop, what we don’t know is an ocean.", "Isaac Newton"),
        new("It always seems impossible until it’s done.", "Nelson Mandela"),
        new("The only way to do great work is to love what you do.", "Steve Jobs"),
        new("Whether you think you can or you can’t, you’re right.", "Henry Ford"),
        new("Quality is not an act, it is a habit.", "Aristotle"),
        new("The journey of a thousand miles begins with one step.", "Lao Tzu"),
        new("Make it work, make it right, make it fast.", "Kent Beck"),
        new("Perfection is achieved when there is nothing left to take away.", "Antoine de Saint-Exupéry"),
        new("First, solve the problem. Then, write the code.", "John Johnson"),
        new("Knowing yourself is the beginning of all wisdom.", "Aristotle"),
        new("The best time to plant a tree was 20 years ago. The second best is now.", "Chinese Proverb"),
        new("Act as if what you do makes a difference. It does.", "William James"),
        new("Done is better than perfect.", "Sheryl Sandberg"),
    ];

    /// <summary>A list this long is a book, not a widget setting. It bounds the state file.</summary>
    public const int MaxQuotes = 256;

    /// <summary>
    /// Which period the given instant falls in, as an index that steps by one each time the quote
    /// is due to change.
    /// <para>
    /// Computed from the <i>offset-local</i> parts of the instant, not from UTC, so "daily" means
    /// the user's midnight rather than Greenwich's — a widget that changed its quote at one in the
    /// afternoon would look broken.
    /// </para>
    /// </summary>
    public static long Period(DateTimeOffset now, QuoteCadence cadence) => cadence switch
    {
        QuoteCadence.Hourly => Floor(now.DateTime, TimeSpan.TicksPerHour),
        QuoteCadence.Daily => Floor(now.DateTime, TimeSpan.TicksPerDay),

        // Manual has no period of its own: the view's click count is the whole index.
        _ => 0,
    };

    /// <summary>How often the widget has to wake to notice the quote is due to change.</summary>
    public static TimeSpan TickInterval(QuoteCadence cadence) => cadence switch
    {
        // A minute, not an hour: the widget must turn over ON the hour, and a timer aligned to the
        // hour would show the previous quote for up to an hour after a restart at half past.
        QuoteCadence.Hourly => TimeSpan.FromMinutes(1),
        QuoteCadence.Daily => TimeSpan.FromMinutes(1),

        // Nothing to wake for. A manual quote changes when it is clicked.
        _ => TimeSpan.Zero,
    };

    /// <summary>
    /// The index to show, or -1 when there is nothing to show.
    /// <para>
    /// With <paramref name="shuffle"/> off the list is walked in order, which is what someone who
    /// arranged their own quotes expects. With it on the period is run through a hash first, so
    /// consecutive days are unrelated quotes rather than the next one down — and still the same
    /// unrelated quote every time that day comes round, because the hash is of the period and the
    /// seed and nothing else.
    /// </para>
    /// </summary>
    /// <param name="count">How many quotes there are. Zero is a real answer, not an error.</param>
    /// <param name="period">The period index, from <see cref="Period"/>, plus any manual steps.</param>
    /// <param name="seed">The user's shuffle seed. Changing it reshuffles the whole rotation.</param>
    /// <param name="shuffle">Whether to walk the list in order or to hash the period.</param>
    public static int Select(int count, long period, int seed, bool shuffle)
        => SelectCore(count, period, seed, shuffle);

    /// <summary>
    /// [S-1495] The original's shuffle button: a random quote, never the one already showing. Returns
    /// how far to move the period (the view adds it to its ephemeral step) so <see cref="Select"/>
    /// lands on another quote; <paramref name="roll"/> is the caller's random number, which keeps
    /// this pure. Zero when there is nothing else to show.
    /// </summary>
    public static long RandomStep(int count, long period, int seed, bool shuffle, int roll)
    {
        if (count < 2)
        {
            return 0;
        }

        var current = SelectCore(count, period, seed, shuffle);
        var first = 1 + (long)((uint)roll % (uint)(count - 1));

        // In order, any step short of a full lap is another quote. Shuffled, a step can hash back to
        // the same one, so walk on until it does not: a few tries at most, bounded all the same.
        for (var step = first; step < first + (count * 4L); step++)
        {
            if (SelectCore(count, period + step, seed, shuffle) != current)
            {
                return step;
            }
        }

        return first;
    }

    private static int SelectCore(int count, long period, int seed, bool shuffle)
    {
        if (count <= 0)
        {
            return -1;
        }

        if (count == 1)
        {
            // Not an optimisation: it is the guarantee that a one-quote list never moves, whatever
            // the period, the seed or how many times it is clicked.
            return 0;
        }

        var index = shuffle ? (long)(Mix((ulong)period, (ulong)(uint)seed) % (ulong)count) : period % count;

        // A period before 1970 is negative, and C#'s % keeps the sign. Without this the widget
        // would index backwards off the front of the list.
        return (int)(((index % count) + count) % count);
    }

    /// <summary>
    /// SplitMix64's finaliser over the period and the seed. Chosen because it is four lines, has no
    /// state, and spreads adjacent inputs — which is the entire requirement: a deterministic
    /// shuffle, not a secure one.
    /// </summary>
    private static ulong Mix(ulong period, ulong seed)
    {
        var value = period + (seed * 0x9E3779B97F4A7C15UL);
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }

    /// <summary>
    /// How many whole units of <paramref name="ticksPerUnit"/> have passed since the epoch, rounding
    /// towards minus infinity so the step is uniform on both sides of 1970.
    /// </summary>
    private static long Floor(DateTime local, long ticksPerUnit)
    {
        var ticks = local.Ticks - DateTime.UnixEpoch.Ticks;
        var units = ticks / ticksPerUnit;

        return ticks < 0 && ticks % ticksPerUnit != 0 ? units - 1 : units;
    }
}

/// <summary>[S-1990] The quote's key map (<see cref="ControlKey"/>): a pure function of the key and the list, so it is tested here and the view only carries the answer out.</summary>
public static class QuoteKeys
{
    /// <summary>
    /// [S-1990] Quote: Right, Space or Enter the next quote (what a tap does), Left the one before.
    /// One quote has nowhere to go, so nothing answers.
    /// </summary>
    public static ControlKey Of(WidgetKey key, int quotes) => quotes <= 1 ? ControlKey.None : key switch
    {
        WidgetKey.Right or WidgetKey.Space or WidgetKey.Enter => ControlKey.Next,
        WidgetKey.Left => ControlKey.Previous,
        _ => ControlKey.None,
    };
}
