using System.Globalization;
using Themia.Core.Localization;
using Themia.Widget.Stocks;

namespace Themia.Core.Widgets;

/// <summary>A sum of money in one currency: what a holding is worth, or how much it moved.</summary>
/// <param name="Value">The amount, in <paramref name="Currency"/>'s whole units.</param>
/// <param name="Currency">The ISO code it is counted in (never a sub-unit such as GBp).</param>
public readonly record struct Money(double Value, string Currency);

/// <summary>
/// [S-2776] "Shares owned": how many of the instrument the user holds, and what that is worth.
/// <para>
/// The amount is the one number on the Stocks page the user types freely, so it is a trust
/// boundary: it is parsed here, in the language's own decimal separator (or a point where that is
/// not the grouping), and refused
/// rather than coerced — a typo that silently became 0 or 1250 would print a confident, wrong sum
/// of the user's money. Empty and 0 both mean "no holding", which is not shown.
/// </para>
/// <para>
/// Worth and change are counted in the quote's currency, never converted: a conversion needs a
/// second quote and its own staleness, and a wrong rate is worse than a foreign currency. A venue
/// that quotes in hundredths (London's pence "GBp"/"GBX", Johannesburg's cents "ZAc", Tel Aviv's
/// agorot "ILA") is counted in the whole currency, because "41,230 GBp" is not how anyone reads
/// what they own.
/// </para>
/// </summary>
public static class StockHolding
{
    /// <summary>The most decimals an amount keeps: a coin's satoshi-scale fractions, and no further.</summary>
    public const int AmountDecimals = 8;

    /// <summary>Past this an amount is a typo, not a holding (and its worth stops being exact in a double).</summary>
    public const double MaxAmount = 1e12;

    /// <summary>The decimals a sum of money is written with: to the cent.</summary>
    public const int MoneyDecimals = 2;

    /// <summary>[S-2777] From this width the caption names the amount as well: "10 held · 4,202.70 USD".</summary>
    public const double LongLineWidth = 300;

    /// <summary>
    /// [S-2777] The holding's caption at a tile width: the worth alone where it is narrow (concise
    /// first: the sum is the point), the amount and the worth from <see cref="LongLineWidth"/>.
    /// </summary>
    public static string Caption(Locale locale, double amount, Money worth, double width)
    {
        var sum = Sum(locale, worth);
        return width >= LongLineWidth
            ? StocksStr.WidgetsC_Stocks_Holding_Line.Format(locale, ("amount", AmountShown(locale, amount)), ("value", sum))
            : sum;
    }

    /// <summary>
    /// Read what was typed into the field. True with null for an empty field or 0 (no holding);
    /// true with the amount, rounded to <see cref="AmountDecimals"/>, for a usable one; false for
    /// anything else (letters, a sign, two separators, grouping, a number past <see cref="MaxAmount"/>).
    /// </summary>
    public static bool TryParse(Locale locale, string? text, out double? amount)
    {
        amount = null;
        var typed = (text ?? string.Empty).Trim();

        if (typed.Length == 0)
        {
            return true;
        }

        // One separator, the language's (or a point where a point is not its grouping); digits
        // around it. No grouping at all: "1.000" is a thousand in German and one in English, and
        // guessing is how a holding gains or loses three zeros, so German refuses it outright.
        var separator = LocaleFormat.DecimalSeparator(locale);
        var pointIsDecimal = !LocaleFormat.GroupSeparator(locale).Contains('.', StringComparison.Ordinal);
        var seen = false;
        var digits = 0;
        var normal = new char[typed.Length];

        for (var i = 0; i < typed.Length; i++)
        {
            var c = typed[i];

            if (c is >= '0' and <= '9')
            {
                digits++;
                normal[i] = c;
            }
            else if ((c == separator || (c == '.' && pointIsDecimal)) && !seen)
            {
                seen = true;
                normal[i] = '.';
            }
            else
            {
                return false;
            }
        }

        if (digits == 0
            || !double.TryParse(new string(normal), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)
            || !double.IsFinite(value)
            || value > MaxAmount)
        {
            return false;
        }

        var rounded = Math.Round(value, AmountDecimals, MidpointRounding.AwayFromZero);
        amount = rounded > 0 ? rounded : null;
        return true;
    }

    /// <summary>
    /// A stored amount made safe: null for none, 0, a negative, not finite or past the cap (with the
    /// reason, for the load log); else the amount rounded as <see cref="TryParse"/> keeps it.
    /// </summary>
    public static double? Normalise(double? stored, out string? problem)
    {
        problem = null;

        if (stored is not { } value)
        {
            return null;
        }

        if (!double.IsFinite(value) || value < 0 || value > MaxAmount)
        {
            problem = $"stocks: shares owned '{value.ToString(CultureInfo.InvariantCulture)}' is not an amount";
            return null;
        }

        var rounded = Math.Round(value, AmountDecimals, MidpointRounding.AwayFromZero);
        return rounded > 0 ? rounded : null;
    }

    /// <summary>
    /// The currency a quote's prices are counted in, and what one price unit is of it: "GBp" is
    /// (GBP, 0.01). An empty code stays empty (the sum is then written without one).
    /// </summary>
    public static (string Code, double Factor) Currency(string? code)
    {
        var trimmed = (code ?? string.Empty).Trim();

        // Case matters: "GBp" is pence and "GBP" pounds. GBX is the ISO-style name for pence.
        return trimmed switch
        {
            "GBp" or "GBX" => ("GBP", 0.01),
            "ZAc" or "ZAC" => ("ZAR", 0.01),
            "ILA" => ("ILS", 0.01),
            _ => (trimmed.ToUpperInvariant(), 1.0),
        };
    }

    /// <summary>What <paramref name="amount"/> of the instrument is worth at <paramref name="price"/>; null without either.</summary>
    public static Money? Worth(double? amount, double? price, string? currency)
    {
        if (amount is not > 0 || price is not { } p || !double.IsFinite(p))
        {
            return null;
        }

        var (code, factor) = Currency(currency);
        return new Money(Round(amount.Value * p * factor), code);
    }

    /// <summary>
    /// How much the holding moved over the shown period: the amount times the price against the
    /// change's own baseline ([S-2775]), so its sign is always the percent's. Null without a change.
    /// </summary>
    public static Money? Change(double? amount, double? price, double? changePercent, string? currency)
    {
        if (amount is not > 0 || price is not { } p || !double.IsFinite(p)
            || changePercent is not { } percent || !double.IsFinite(percent) || percent <= -100)
        {
            return null;
        }

        var baseline = p / (1 + (percent / 100));
        var (code, factor) = Currency(currency);
        return new Money(Round(amount.Value * (p - baseline) * factor), code);
    }

    /// <summary>An amount as the field shows it: the language's separator, no grouping, no trailing zeros.</summary>
    public static string Amount(Locale locale, double amount)
    {
        var text = Math.Round(amount, AmountDecimals, MidpointRounding.AwayFromZero)
            .ToString("0." + new string('#', AmountDecimals), CultureInfo.InvariantCulture);
        return text.Replace('.', LocaleFormat.DecimalSeparator(locale));
    }

    /// <summary>An amount as the widget writes it: grouped like every other number on screen.</summary>
    public static string AmountShown(Locale locale, double amount)
    {
        var rounded = Math.Round(amount, AmountDecimals, MidpointRounding.AwayFromZero);
        var invariant = rounded.ToString("0." + new string('#', AmountDecimals), CultureInfo.InvariantCulture);
        var dot = invariant.IndexOf('.');
        var decimals = dot < 0 ? 0 : invariant.Length - dot - 1;

        return LocaleFormat.Number(locale, rounded, decimals);
    }

    /// <summary>A sum with its code, to the cent: "4,202.70 USD".</summary>
    public static string Sum(Locale locale, Money money)
    {
        var number = LocaleFormat.Number(locale, money.Value, MoneyDecimals);
        return money.Currency.Length == 0 ? number : $"{number} {money.Currency}";
    }

    /// <summary>A move, always signed, to the cent, without the code (it stands once, after the worth): "+82.95".</summary>
    public static string SignedSum(Locale locale, Money money)
    {
        var cents = Round(money.Value);
        var number = LocaleFormat.Number(locale, Math.Abs(cents), MoneyDecimals);
        return cents == 0 ? number : (cents > 0 ? "+" : "-") + number;
    }

    private static double Round(double value) => Math.Round(value, MoneyDecimals, MidpointRounding.AwayFromZero);
}
