using System.Globalization;
using System.Xml;
using Themia.Core.Localization;

namespace Themia.Core.Widgets;

/// <summary>One working day's reference rates: how many units of each currency one euro buys.</summary>
public sealed record RateDay(DateOnly Date, IReadOnlyDictionary<string, double> PerEuro);

/// <summary>
/// [S-5141] The ECB's reference rates for the last 90 days, newest day first. Euro-based, as the ECB
/// publishes them; every other pair is a cross rate through the euro (<see cref="ExchangeRules.Cross"/>).
/// </summary>
public sealed record RateTable(IReadOnlyList<RateDay> Days)
{
    public RateDay Latest => Days[0];

    /// <summary>The document this was parsed from, so the offline store can save and re-parse it ([S-5161]).</summary>
    public string Source { get; init; } = string.Empty;
}

/// <summary>[S-5141] The ECB XML's parser: the trust boundary for that service.</summary>
public static class EcbWire
{
    /// <summary>The 90-day file is about 70 KB; four times that is not the document asked for.</summary>
    private const int MaxLength = 320 * 1024;

    /// <summary>
    /// Parse <c>eurofxref-hist-90d.xml</c> (or <c>eurofxref-daily.xml</c>, the same shape with one
    /// day). The XML is read with DTDs prohibited and no resolver. A document that is not XML or has
    /// no dated <c>Cube</c> is rejected; a day with a bad date, a rate that is not a positive number
    /// or a code that is not three capital letters is dropped (the rest of the day stands), and a day
    /// left with no rate is dropped whole. Days come out newest first, one per date.
    /// </summary>
    public static WireResult<RateTable> Parse(string? xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return WireResult<RateTable>.Bad("an empty reply");
        }

        if (xml.Length > MaxLength)
        {
            return WireResult<RateTable>.Bad("the reply is too large");
        }

        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreComments = true,
            IgnoreWhitespace = true,
            MaxCharactersInDocument = MaxLength,
        };

        var days = new SortedDictionary<DateOnly, Dictionary<string, double>>();
        var sawDay = false;

        try
        {
            using var reader = XmlReader.Create(new StringReader(xml), settings);
            Dictionary<string, double>? day = null;

            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "Cube")
                {
                    continue;
                }

                if (reader.GetAttribute("time") is { } time)
                {
                    sawDay = true;
                    day = DateOnly.TryParseExact(time, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                        && !days.ContainsKey(date)
                            ? days[date] = new Dictionary<string, double>(StringComparer.Ordinal)
                            : null;
                }
                else if (day is not null && reader.GetAttribute("currency") is { } code && reader.GetAttribute("rate") is { } text)
                {
                    if (ExchangeRules.IsCode(code)
                        && double.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var rate)
                        && double.IsFinite(rate)
                        && rate > 0
                        && rate < 1e9)
                    {
                        day.TryAdd(code, rate);
                    }
                }
            }
        }
        catch (XmlException ex)
        {
            return WireResult<RateTable>.Bad($"not XML: {ex.Message}");
        }

        if (!sawDay)
        {
            return WireResult<RateTable>.Bad("no dated rates in the document");
        }

        var kept = days.Where(d => d.Value.Count > 0).Reverse()
            .Select(d => new RateDay(d.Key, d.Value))
            .ToList();

        return kept.Count == 0
            ? WireResult<RateTable>.Nothing("no usable rate in the document")
            : WireResult<RateTable>.Ok(new RateTable(kept) { Source = xml });
    }
}

/// <summary>[S-5145] One pair as the widget shows it.</summary>
/// <param name="Rate">Units of the quote one unit of the base buys, on the latest day.</param>
/// <param name="ChangePercent">Against the working day before, or null when there is none.</param>
/// <param name="History">The rate on each day the table holds both, oldest first, for the line.</param>
/// <param name="Date">The day of <paramref name="Rate"/>.</param>
public sealed record PairRate(string Base, string Quote, double Rate, double? ChangePercent, IReadOnlyList<double> History, DateOnly Date);

/// <summary>[S-5142]..[S-5145] Currencies, cross rates and their text.</summary>
public static class ExchangeRules
{
    /// <summary>
    /// [S-5143] The quote a widget shows until one is chosen: the euro, the ECB's own currency and
    /// the one most bases are priced against; for a euro base, the dollar.
    /// </summary>
    public static string DefaultQuote(string basis) => string.Equals(basis, "EUR", StringComparison.Ordinal) ? "USD" : "EUR";

    /// <summary>
    /// [S-5142] The currencies the ECB publishes reference rates for (its list since 2022), and the
    /// euro itself, alphabetically. A code outside this list cannot be priced and is not offered.
    /// </summary>
    public static IReadOnlyList<string> Published { get; } =
    [
        "AUD", "BGN", "BRL", "CAD", "CHF", "CNY", "CZK", "DKK", "EUR", "GBP", "HKD", "HUF", "IDR", "ILS",
        "INR", "ISK", "JPY", "KRW", "MXN", "MYR", "NOK", "NZD", "PHP", "PLN", "RON", "SEK", "SGD", "THB",
        "TRY", "USD", "ZAR",
    ];

    /// <summary>Three ASCII capitals: the shape of an ISO 4217 code, nothing more.</summary>
    public static bool IsCode(string? code)
        => code is { Length: 3 } && code.All(c => c is >= 'A' and <= 'Z');

    /// <summary>A published code, upper-cased and trimmed, or null.</summary>
    public static string? Normalise(string? code)
    {
        var text = code?.Trim().ToUpperInvariant();
        return text is not null && Published.Contains(text) ? text : null;
    }

    /// <summary>Units of a currency one euro buys on a day; the euro is 1, a code missing that day null.</summary>
    private static double? PerEuro(RateDay day, string code)
        => code == "EUR" ? 1 : day.PerEuro.TryGetValue(code, out var rate) ? rate : null;

    /// <summary>
    /// [S-5145] The pair through the euro: base→quote = (quote per euro) / (base per euro). Null when
    /// the latest day lacks either code (a currency the ECB stopped publishing). The change is against
    /// the newest earlier day that holds both; the history keeps the days that hold both.
    /// </summary>
    public static PairRate? Cross(RateTable table, string basis, string quote)
    {
        ArgumentNullException.ThrowIfNull(table);

        if (table.Days.Count == 0 || Rate(table.Latest, basis, quote) is not { } latest)
        {
            return null;
        }

        double? previous = null;
        foreach (var day in table.Days.Skip(1))
        {
            if (Rate(day, basis, quote) is { } earlier)
            {
                previous = earlier;
                break;
            }
        }

        var history = table.Days
            .Reverse()
            .Select(d => Rate(d, basis, quote))
            .Where(r => r is not null)
            .Select(r => r!.Value)
            .ToList();

        var change = previous is { } before ? ((latest - before) / before) * 100 : (double?)null;
        return new PairRate(basis, quote, latest, change, history, table.Latest.Date);
    }

    private static double? Rate(RateDay day, string basis, string quote)
        => PerEuro(day, basis) is { } b && PerEuro(day, quote) is { } q ? q / b : null;

    /// <summary>
    /// [S-5145] A rate with the precision its size needs: four decimals under 10 ("0.9313",
    /// "1.0738"), three under 100, two from there ("177.34", "20,036.94"). Locale digits and separators.
    /// </summary>
    public static string Format(Locale locale, double rate)
        => LocaleFormat.Number(locale, rate, rate < 10 ? 4 : rate < 100 ? 3 : 2);

    public static string Format(double rate) => Format(Strings.Current, rate);

    /// <summary>The longest rate this widget prints, in characters, for a stable type step.</summary>
    public const double RateCharacters = 9;

    /// <summary>[S-5141] The days the ECB's history file covers.</summary>
    public const int HistoryDays = 90;
}

/// <summary>
/// [S-5143] The currency of the Windows region (Settings › Time &amp; language › Region), the base a
/// new exchange-rates widget follows. <see cref="Country"/> is set by the app from Windows; the map
/// covers the countries whose currency the ECB publishes, and everywhere else falls back to the euro.
/// </summary>
public static class RegionCurrency
{
    private static volatile string _country = string.Empty;

    /// <summary>The ISO 3166 two-letter region Windows reports, upper case; empty when unread.</summary>
    public static string Country
    {
        get => _country;
        set => _country = (value ?? string.Empty).Trim().ToUpperInvariant();
    }

    /// <summary>The region's currency now.</summary>
    public static string Current => For(Country);

    private static readonly string[] EuroArea =
    [
        "AT", "BE", "HR", "CY", "EE", "FI", "FR", "DE", "GR", "IE", "IT", "LV", "LT", "LU", "MT", "NL",
        "PT", "SK", "SI", "ES", "AD", "MC", "SM", "VA", "ME", "XK",
    ];

    private static readonly Dictionary<string, string> Countries = new(StringComparer.Ordinal)
    {
        ["AU"] = "AUD", ["BG"] = "BGN", ["BR"] = "BRL", ["CA"] = "CAD", ["CH"] = "CHF", ["LI"] = "CHF",
        ["CN"] = "CNY", ["CZ"] = "CZK", ["DK"] = "DKK", ["GL"] = "DKK", ["FO"] = "DKK", ["GB"] = "GBP",
        ["IM"] = "GBP", ["JE"] = "GBP", ["GG"] = "GBP", ["HK"] = "HKD", ["HU"] = "HUF", ["ID"] = "IDR",
        ["IL"] = "ILS", ["IN"] = "INR", ["IS"] = "ISK", ["JP"] = "JPY", ["KR"] = "KRW", ["MX"] = "MXN",
        ["MY"] = "MYR", ["NO"] = "NOK", ["NZ"] = "NZD", ["PH"] = "PHP", ["PL"] = "PLN", ["RO"] = "RON",
        ["SE"] = "SEK", ["SG"] = "SGD", ["TH"] = "THB", ["TR"] = "TRY", ["US"] = "USD", ["PR"] = "USD",
        ["EC"] = "USD", ["SV"] = "USD", ["PA"] = "USD", ["ZA"] = "ZAR",
    };

    /// <summary>The currency for a region code; the euro for the euro area and for anywhere unmapped.</summary>
    public static string For(string? country)
    {
        var code = (country ?? string.Empty).Trim().ToUpperInvariant();
        return Countries.TryGetValue(code, out var currency) ? currency : "EUR";
    }

    /// <summary>Whether a region is in the euro area (for the docs and the tests; the fallback is the euro anyway).</summary>
    public static bool UsesEuro(string? country) => EuroArea.Contains((country ?? string.Empty).Trim().ToUpperInvariant());
}
