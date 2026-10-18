using System.Text.Json.Serialization;
using Themia.Core.Widgets;

namespace Themia.Core.Model;

/// <summary>
/// Which of the two quote services a symbol belongs to.
/// <para>
/// A closed set with its own discriminator rather than a string, because it selects the endpoint:
/// a coin id is not a Yahoo symbol and asking the wrong service for it produces a confident 404.
/// What the instrument <i>is</i> — equity, index, currency, commodity — is a presentation question
/// answered by <see cref="MarketQuote.Classify"/>, and does not change which service is called.
/// </para>
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<QuoteMarket>))]
public enum QuoteMarket
{
    /// <summary>Anything Yahoo's chart endpoint quotes: shares, ETFs, funds, indices, FX, futures.</summary>
    Listed = 0,

    /// <summary>A CoinGecko coin id.</summary>
    Crypto = 1,
}

/// <summary>How much history the sparkline covers.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<QuoteRange>))]
public enum QuoteRange
{
    Day = 0,
    FiveDays = 1,
    Month = 2,
    ThreeMonths = 3,
    SixMonths = 4,
    Year = 5,
    TwoYears = 6,
    FiveYears = 7,
    TenYears = 8,
}
