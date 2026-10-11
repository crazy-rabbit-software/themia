using Themia.Core.Localization;

namespace Themia.Core.Widgets;

/// <summary>One sample of the chart: when (epoch seconds) and the price then.</summary>
public readonly record struct QuoteBar(long Time, double Close);

/// <summary>[S-2775] The chart series and the change, on one baseline (<see cref="MarketQuote.Trace"/>).</summary>
/// <param name="Bars">The series, starting at the baseline.</param>
/// <param name="ChangePercent">The price against that baseline, or null without one.</param>
public readonly record struct QuoteTrace(IReadOnlyList<QuoteBar> Bars, double? ChangePercent);

/// <summary>[S-1308] The last pre-market or after-hours print, against the regular close.</summary>
/// <param name="Price">The extended-session price.</param>
/// <param name="ChangePercent">Its move against the regular session's close.</param>
/// <param name="At">When it printed, epoch seconds.</param>
public readonly record struct ExtendedPrint(double Price, double ChangePercent, long At);

/// <summary>The extended prints of the day, each present only when a bar fell in its window.</summary>
public readonly record struct ExtendedPrints(ExtendedPrint? Pre, ExtendedPrint? Post);

/// <summary>
/// One instrument's quote, already validated and reduced to what a widget draws.
/// </summary>
/// <param name="Symbol">The symbol as asked for.</param>
/// <param name="Name">The service's own name for it, or empty.</param>
/// <param name="Price">The last regular-session price, in <paramref name="Currency"/>.</param>
/// <param name="ChangePercent">The move against the range's baseline, where the chart starts ([S-2775]), or null without one.</param>
/// <param name="Currency">ISO code; USD for crypto, since that is what is asked for.</param>
/// <param name="PriceHint">The venue's tick precision, when it sent one.</param>
/// <param name="Bars">The chart series, oldest first, every unusable sample dropped, times kept; its first point is the baseline ([S-2775]).</param>
/// <param name="Sessions">Today's trading windows, when the service sent a usable schedule.</param>
public sealed record TickerQuote(
    string Symbol,
    string Name,
    double? Price,
    double? ChangePercent,
    string Currency,
    int? PriceHint,
    IReadOnlyList<QuoteBar> Bars,
    TradingSessions Sessions)
{
    /// <summary>[S-1309] The exchange's UTC offset now, for labelling times in its own zone; null = local.</summary>
    public TimeSpan? ExchangeOffset { get; init; }

    /// <summary>[S-1309] The exchange zone's short name ("EDT"), or empty.</summary>
    public string ZoneName { get; init; } = string.Empty;

    /// <summary>Yahoo's quote type ("EQUITY", "CURRENCY"…), or empty: refines <see cref="MarketQuote.Classify"/>.</summary>
    public string QuoteType { get; init; } = string.Empty;

    /// <summary>Whether the series includes pre- and after-hours bars (asked for and sent).</summary>
    public bool HasPrePost { get; init; }
}

/// <summary>[S-1307] One symbol search result.</summary>
/// <param name="Symbol">The Yahoo symbol or CoinGecko id to store.</param>
/// <param name="Name">What to show and to use as the display name.</param>
/// <param name="Market">Which service quotes it.</param>
/// <param name="Asset">What kind of thing it is, for the row's icon.</param>
/// <param name="Exchange">The venue ("NASDAQ"), or empty.</param>
public sealed record SymbolHit(string Symbol, string Name, Model.QuoteMarket Market, AssetClass Asset, string Exchange);

/// <summary>A raw Yahoo search hit, before it is filtered.</summary>
public readonly record struct ListedCandidate(string? Symbol, string? ShortName, string? LongName, string? QuoteType, string? Exchange);

/// <summary>A raw CoinGecko search hit, before it is filtered.</summary>
public readonly record struct CoinCandidate(string? Id, string? Name, string? Symbol);

/// <summary>
/// [S-1307] The symbol search the original's settings had (<c>SymbolSearch.tsx</c>): Yahoo's search
/// merged with CoinGecko's, alternating, at most twelve. Only the instrument kinds the chart endpoint
/// quotes survive (shares, ETFs, funds, indices, currency pairs, futures); an option or a news item
/// is not a thing this widget can show.
/// </summary>
public static class MarketSearch
{
    public const int MaxPerService = 8;

    public const int MaxHits = 12;

    /// <summary>The Yahoo quote types a widget can chart.</summary>
    public static IReadOnlySet<string> SearchableTypes { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "EQUITY", "ETF", "MUTUALFUND", "INDEX", "CURRENCY", "FUTURE" };

    /// <summary>Whether a query is worth sending: something typed, and not absurdly long.</summary>
    public static bool IsQuery(string? text) => text?.Trim() is { Length: > 0 and <= 60 };

    public static IReadOnlyList<SymbolHit> Listed(IEnumerable<ListedCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        var hits = new List<SymbolHit>();

        foreach (var candidate in candidates)
        {
            if (hits.Count >= MaxPerService)
            {
                break;
            }

            var symbol = candidate.Symbol?.Trim() ?? string.Empty;

            if (!SearchableTypes.Contains(candidate.QuoteType ?? string.Empty) || !WebInput.IsPlausibleSymbol(symbol))
            {
                continue;
            }

            var name = FirstText(candidate.ShortName, candidate.LongName) ?? symbol;
            hits.Add(new SymbolHit(
                symbol,
                name,
                Model.QuoteMarket.Listed,
                MarketQuote.Classify(symbol, Model.QuoteMarket.Listed, candidate.QuoteType),
                candidate.Exchange?.Trim() ?? string.Empty));
        }

        return hits;
    }

    public static IReadOnlyList<SymbolHit> Coins(IEnumerable<CoinCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        var hits = new List<SymbolHit>();

        foreach (var candidate in candidates)
        {
            if (hits.Count >= MaxPerService)
            {
                break;
            }

            var id = candidate.Id?.Trim() ?? string.Empty;

            if (!WebInput.IsPlausibleSymbol(id))
            {
                continue;
            }

            var name = FirstText(candidate.Name) ?? id;
            var ticker = candidate.Symbol?.Trim().ToUpperInvariant();
            hits.Add(new SymbolHit(
                id,
                string.IsNullOrEmpty(ticker) ? name : $"{name} ({ticker})",
                Model.QuoteMarket.Crypto,
                AssetClass.Crypto,
                string.Empty));
        }

        return hits;
    }

    /// <summary>Alternate the two lists (a share, a coin, a share…) and keep twelve, as the original.</summary>
    public static IReadOnlyList<SymbolHit> Merge(IReadOnlyList<SymbolHit> listed, IReadOnlyList<SymbolHit> coins)
    {
        ArgumentNullException.ThrowIfNull(listed);
        ArgumentNullException.ThrowIfNull(coins);

        var merged = new List<SymbolHit>();

        for (var i = 0; i < Math.Max(listed.Count, coins.Count) && merged.Count < MaxHits; i++)
        {
            if (i < listed.Count)
            {
                merged.Add(listed[i]);
            }

            if (i < coins.Count && merged.Count < MaxHits)
            {
                merged.Add(coins[i]);
            }
        }

        return merged;
    }

    /// <summary>"Shares", "Crypto"…: the label shown beside a symbol for its class.</summary>
    public static string Label(AssetClass asset) => Label(Strings.Current, asset);

    public static string Label(Locale locale, AssetClass asset) => Strings.Get(locale, asset switch
    {
        AssetClass.Crypto => Str.WidgetsC_Stocks_Asset_Crypto,
        AssetClass.Currency => Str.WidgetsC_Stocks_Asset_Currency,
        AssetClass.Commodity => Str.WidgetsC_Stocks_Asset_Commodity,
        AssetClass.Index => Str.WidgetsC_Stocks_Asset_Index,
        _ => Str.WidgetsC_Stocks_Asset_Shares,
    });

    private static string? FirstText(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                var trimmed = value.Trim();
                return trimmed.Length > 120 ? trimmed[..120] : trimmed;
            }
        }

        return null;
    }
}
