#if DEBUG
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.App.Shell.Gallery;

/// <summary>
/// [S-1307] The symbol search's Debug renders, without a network: <see cref="UseFixture"/> answers the stock
/// widgets' symbol searches from a fixed list (<c>--render editbar</c> types into them), as
/// <see cref="PlaceRender"/> answers the place searches. <see cref="Unanswered"/> never answers (the search's
/// "Searching…"); <see cref="Unreached"/> reaches no service (the search says why).
/// </summary>
internal static class SymbolRender
{
    private static readonly SymbolHit[] Fixture =
    [
        new("MSFT", "Microsoft Corporation", QuoteMarket.Listed, AssetClass.Equity, "NASDAQ"),
        new("MSF.DE", "Microsoft Corporation", QuoteMarket.Listed, AssetClass.Equity, "XETRA"),
        new("MSTR", "MicroStrategy Incorporated", QuoteMarket.Listed, AssetClass.Equity, "NASDAQ"),
        new("MU", "Micron Technology, Inc.", QuoteMarket.Listed, AssetClass.Equity, "NASDAQ"),
        new("MCHP", "Microchip Technology Incorporated", QuoteMarket.Listed, AssetClass.Equity, "NASDAQ"),
        new("^GSPC", "S&P 500", QuoteMarket.Listed, AssetClass.Index, "SNP"),
        new("bitcoin", "Bitcoin", QuoteMarket.Crypto, AssetClass.Crypto, string.Empty),
    ];

    /// <summary>What the fixture never answers.</summary>
    public const string Unanswered = "wait";

    /// <summary>What reaches no service.</summary>
    public const string Unreached = "offline";

    /// <summary>The market search's answers from <see cref="Fixture"/>: the hits whose name or symbol holds the query.</summary>
    public static void UseFixture()
        => SymbolLookup.DebugLookup = (query, cancel) =>
        {
            if (string.Equals(query, Unanswered, StringComparison.OrdinalIgnoreCase))
            {
                return new TaskCompletionSource<IReadOnlyList<SymbolHit>>().Task.WaitAsync(cancel);
            }

            if (string.Equals(query, Unreached, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromException<IReadOnlyList<SymbolHit>>(new SymbolSearchFailedException(FetchStatus.Unreachable));
            }

            IReadOnlyList<SymbolHit> hits = [.. Fixture.Where(h => h.Name.Contains(query, StringComparison.OrdinalIgnoreCase) || h.Symbol.Contains(query, StringComparison.OrdinalIgnoreCase))];
            return Task.FromResult(hits);
        };
}
#endif
