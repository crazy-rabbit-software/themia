using System.Globalization;

namespace Themia.Core.Settings;

/// <summary>
/// [S-2309] A number typed into a page's number box, which shows a setting as its line writes it
/// ("42 %", "1.5 s", "12px"): the number is read out of the words, in the units the box shows, and
/// turned back into the setting's own (a percentage of a fraction, seconds of milliseconds) by the
/// factor its format writes at. The page holds it to the range and the grain. Pure.
/// </summary>
public static class NumberEntry
{
    /// <summary>
    /// The number in <paramref name="typed"/>: a leading minus, digits, and one decimal mark (a point
    /// or a comma, as the languages write it); grouping spaces ignored. Null where there is no digit.
    /// </summary>
    public static double? Read(string? typed)
    {
        if (string.IsNullOrWhiteSpace(typed))
        {
            return null;
        }

        var text = new System.Text.StringBuilder();
        var negative = false;
        var mark = false;
        foreach (var c in typed)
        {
            if (char.IsAsciiDigit(c))
            {
                text.Append(c);
            }
            else if ((c == '.' || c == ',') && !mark && text.Length > 0)
            {
                mark = true;
                text.Append('.');
            }
            else if ((c == '-' || c == '−') && text.Length == 0)
            {
                negative = true;
            }
            else if (text.Length > 0 && !char.IsWhiteSpace(c) && c != ' ' && c != ' ')
            {
                break;
            }
        }

        var digits = text.ToString().TrimEnd('.');
        if (digits.Length == 0 || !double.TryParse(digits, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
        {
            return null;
        }

        return negative ? -value : value;
    }

    /// <summary>
    /// [S-2309] What <paramref name="typed"/> means in the setting's own units: the number read, over the
    /// factor at which <paramref name="format"/> writes <paramref name="sample"/> (a non-zero value of the
    /// setting, its maximum). Null where nothing was typed that reads as a number.
    /// </summary>
    public static double? Value(string? typed, double sample, Func<double, string> format)
    {
        ArgumentNullException.ThrowIfNull(format);
        if (Read(typed) is not { } shown)
        {
            return null;
        }

        var factor = sample != 0 && Read(format(sample)) is { } written && written != 0 ? written / sample : 1;
        return shown / factor;
    }
}
