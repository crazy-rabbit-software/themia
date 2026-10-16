using System.Collections.Concurrent;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;

namespace Themia.Core.Widgets;

/// <summary>
/// [S-6020]..[S-6024] The Clock's word style: the time as a sentence, in five-minute steps, in the
/// language's own conventions.
/// <para>
/// Each language writes its own rules into its locale strings (<c>Locales/wordclock</c>), so a new
/// convention is a translation, never code: <c>wordClock.hours</c> holds one or more hour forms
/// (<c>;</c> between forms, <c>|</c> between the 12 or 24 entries of one), and
/// <c>wordClock.m00</c>..<c>wordClock.m55</c> one sentence per five minutes, where <c>[h]</c> is the
/// hour in form 1, <c>[n]</c> the next hour in form 1, <c>[h2]</c>/<c>[n2]</c> form 2 and so on. A
/// form of 12 is indexed by the hour mod 12 (0 is "twelve"), a form of 24 by the hour itself (so a
/// language can say midnight, noon or a part of the day). Grammar a word cannot carry by itself
/// (case, article, verb agreement: "Es la una" / "Son las dos") lives in the form entries.
/// </para>
/// </summary>
public static class WordClock
{
    private const string Scope = "word-clock";

    private static readonly Str[] Sentences =
    [
        Str.WordClock_M00, Str.WordClock_M05, Str.WordClock_M10, Str.WordClock_M15,
        Str.WordClock_M20, Str.WordClock_M25, Str.WordClock_M30, Str.WordClock_M35,
        Str.WordClock_M40, Str.WordClock_M45, Str.WordClock_M50, Str.WordClock_M55,
    ];

    private static readonly ConcurrentDictionary<Locale, string[][]> Forms = new();

    /// <summary>
    /// [S-6021] The five-minute step a time reads as: the nearest one, so 10:13 is "quarter past
    /// ten" and 10:58 is "eleven o'clock". Returns the hour (0..23) and minute (0, 5 .. 55) said.
    /// </summary>
    public static (int Hour, int Minute) Round(int hour, int minute)
    {
        var total = ((((hour * 60) + minute + 2) / 5 * 5) % 1440 + 1440) % 1440;
        return (total / 60, total % 60);
    }

    /// <summary>[S-6020] The sentence for <paramref name="hour"/>:<paramref name="minute"/> (24-hour) in <paramref name="locale"/>.</summary>
    public static string Compose(Locale locale, int hour, int minute)
    {
        var (h, m) = Round(hour, minute);
        var forms = FormsFor(locale);
        var template = Strings.Get(locale, Sentences[m / 5]);

        if (Fill(template, forms, h) is { } text)
        {
            return Capitalise(text, locale);
        }

        // [S-6023] A sentence that names a form the language does not have: English, never a
        // half-filled line with a marker showing.
        Log.Warn(Scope, $"{locale}: '{Sentences[m / 5].Key()}' names an hour form the language does not have; English");
        return locale == Locale.En
            ? template
            : Compose(Locale.En, hour, minute);
    }

    private static readonly ConcurrentDictionary<Locale, string> LongestByLocale = new();

    /// <summary>[S-6024] The share of the width the fit counts on: the estimate's margin for semibold and long words.</summary>
    private const double WidthMargin = 0.86;

    /// <summary>
    /// [S-6024] The language's longest sentence over the whole day, by drawn width: what the word
    /// style sizes its type by, so the text does not change size every five minutes.
    /// </summary>
    public static string Longest(Locale locale) => LongestByLocale.GetOrAdd(locale, static l =>
    {
        var longest = string.Empty;
        var widest = 0.0;
        for (var hour = 0; hour < 24; hour++)
        {
            for (var minute = 0; minute < 60; minute += 5)
            {
                var text = Compose(l, hour, minute);
                var cells = Cells(text);
                if (cells > widest)
                {
                    widest = cells;
                    longest = text;
                }
            }
        }

        return longest;
    });

    /// <summary>
    /// [S-6024] The largest type step at which <paramref name="sample"/>, wrapped at words, fits
    /// the box: the ramp's steps while it is small, then <see cref="FluentType.TitleLarge"/> scaled
    /// (as <see cref="FluentType.Hero"/> does). Never below <paramref name="floor"/>. A script without
    /// spaces (Chinese, Japanese, Thai) wraps anywhere, as the text block does.
    /// </summary>
    public static TypeStep Fit(double width, double height, string sample, TypeStep floor)
    {
        ArgumentNullException.ThrowIfNull(sample);

        if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0)
        {
            return floor;
        }

        // The ramp's average advance is a regular weight's; the large steps are semibold and wider,
        // and a word the estimate lets through by a hair is broken mid-word by the text block.
        var room = width * WidthMargin;
        var chosen = floor;
        foreach (var step in Candidates())
        {
            if (step.FontSize >= floor.FontSize && Lines(sample, room, step) * step.LineHeight <= height)
            {
                chosen = step;
            }
        }

        return chosen;
    }

    private static IEnumerable<TypeStep> Candidates()
    {
        foreach (var step in FluentType.Ramp)
        {
            if (step.FontSize <= FluentType.TitleLarge.FontSize)
            {
                yield return step;
            }
        }

        for (var factor = 1.1; factor <= 8; factor += 0.1)
        {
            yield return FluentType.Scaled(FluentType.TitleLarge, factor);
        }
    }

    /// <summary>[S-6024] How many lines <paramref name="text"/> wraps to at <paramref name="step"/> in <paramref name="width"/>.</summary>
    public static int Lines(string text, double width, TypeStep step)
    {
        ArgumentNullException.ThrowIfNull(text);

        var space = FluentType.EstimateWidth(step, 1);
        var lines = 1;
        var used = 0.0;

        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var wide = FluentType.EstimateWidth(step, Cells(word));
            if (wide > width)
            {
                // Longer than a line: the text block breaks it wherever it must.
                var start = used > 0 ? used + space : 0;
                var total = start + wide;
                lines += (int)Math.Ceiling(total / width) - 1;
                used = total % width;
                continue;
            }

            var next = used > 0 ? used + space + wide : wide;
            if (next > width)
            {
                lines++;
                used = wide;
            }
            else
            {
                used = next;
            }
        }

        return lines;
    }

    /// <summary>
    /// A text's width in average Latin characters: a Han, kana or Hangul character is drawn about
    /// a full em wide, nearly twice the average advance the ramp estimates with.
    /// </summary>
    internal static double Cells(string text)
    {
        var cells = 0.0;
        foreach (var c in text)
        {
            cells += c is >= '\u1100' and <= '\u11FF' or >= '\u2E80' and <= '\uA4CF' or >= '\uAC00' and <= '\uD7AF' or >= '\uF900' and <= '\uFAFF' or >= '\uFF00' and <= '\uFF60'
                ? 1.75
                : 1;
        }

        return cells;
    }

    /// <summary>
    /// [S-6023] Whether a language's word-clock strings are whole: every form has 12 or 24 non-empty
    /// entries and every sentence names only forms that exist. Null when they are, else what is wrong.
    /// </summary>
    public static string? Problem(Locale locale)
    {
        if (Parse(Strings.Get(locale, Str.WordClock_Hours)) is not { } forms)
        {
            return "wordClock.hours: every form needs 12 or 24 non-empty entries";
        }

        foreach (var id in Sentences)
        {
            if (Fill(Strings.Get(locale, id), forms, 0) is null)
            {
                return $"{id.Key()}: a marker names a form that does not exist";
            }
        }

        return null;
    }

    private static string[][] FormsFor(Locale locale) => Forms.GetOrAdd(locale, static l =>
    {
        if (Parse(Strings.Get(l, Str.WordClock_Hours)) is { } forms)
        {
            return forms;
        }

        Log.Warn(Scope, $"{l}: wordClock.hours is malformed; English hours");
        return Parse(Strings.Get(Locale.En, Str.WordClock_Hours))!;
    });

    /// <summary>The hour forms of one language, or null when one is not 12 or 24 non-empty entries.</summary>
    internal static string[][]? Parse(string text)
    {
        var forms = text.Split(';').Select(form => form.Split('|').Select(word => word.Trim()).ToArray()).ToArray();
        return forms.All(f => f.Length is 12 or 24 && f.All(w => w.Length > 0)) ? forms : null;
    }

    /// <summary>The sentence with its markers filled; null when a marker names a form that does not exist.</summary>
    private static string? Fill(string template, string[][] forms, int hour)
    {
        var text = new System.Text.StringBuilder(template.Length + 16);
        var at = 0;

        while (at < template.Length)
        {
            if (template[at] == '[' && Marker(template, at) is { } marker)
            {
                if (marker.Form > forms.Length)
                {
                    return null;
                }

                var said = marker.Next ? (hour + 1) % 24 : hour;
                var form = forms[marker.Form - 1];
                text.Append(form.Length == 24 ? form[said] : form[said % 12]);
                at = marker.End;
                continue;
            }

            text.Append(template[at]);
            at++;
        }

        return text.ToString();
    }

    /// <summary>A marker at <paramref name="at"/>: <c>[h]</c>, <c>[n]</c>, <c>[h2]</c>, <c>[n3]</c> …</summary>
    private static (bool Next, int Form, int End)? Marker(string template, int at)
    {
        if (at + 2 >= template.Length || template[at + 1] is not ('h' or 'n'))
        {
            return null;
        }

        var next = template[at + 1] == 'n';
        var i = at + 2;
        var form = 0;
        while (i < template.Length && char.IsAsciiDigit(template[i]))
        {
            form = (form * 10) + (template[i] - '0');
            i++;
        }

        if (i >= template.Length || template[i] != ']')
        {
            return null;
        }

        return (next, Math.Max(1, form), i + 1);
    }

    /// <summary>[S-6022] The sentence starts with a capital ("Сейчас", "Il est"), Turkish's dotted İ included.</summary>
    private static string Capitalise(string text, Locale locale)
    {
        if (text.Length == 0 || !char.IsLower(text[0]))
        {
            return text;
        }

        var first = locale == Locale.Tr && text[0] == 'i' ? 'İ' : char.ToUpperInvariant(text[0]);
        return first + text[1..];
    }
}
