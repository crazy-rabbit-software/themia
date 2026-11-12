using System.Text.Json.Serialization;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>
/// [S-5140] [S-11150] What the exchange-rate atoms share: one pair of the ECB's reference rates, what one
/// unit of the base currency buys of the quote. The rate (<c>exchangeRate</c>) and its 90 days
/// (<c>exchangeHistory</c>) each hold it; set to the same pair they read the one table and agree to the
/// digit ([S-11171]). Several pairs are several widgets.
/// </summary>
public abstract class ExchangePairConfig : WidgetConfig, IInterests
{
    /// <summary>[S-2608] What the background sync keeps fresh for it: the one table every pair reads.</summary>
    public void AddInterests(InterestsBuilder interests) => interests.Of<RatesInterest>().Add(new RatesInterest(OnlineCadence.Rates));

    /// <summary>[S-5143] The base currency, an ISO 4217 code the ECB publishes; empty follows the Windows region.</summary>
    public string BaseCurrency { get; set; } = string.Empty;

    /// <summary>[S-5143] The currency priced in the base; empty is the euro, or the dollar for a euro base.</summary>
    public string QuoteCurrency { get; set; } = string.Empty;

    /// <summary>The base in force: the stored one, or the Windows region's currency ([S-5143]).</summary>
    [JsonIgnore]
    public string EffectiveBase => BaseCurrency.Length > 0 ? BaseCurrency : RegionCurrency.Current;

    /// <summary>
    /// [S-5143] The quote in force: the stored one, unless it is empty or the base itself (one CHF is
    /// one CHF), then <see cref="ExchangeRules.DefaultQuote"/> for the base.
    /// </summary>
    [JsonIgnore]
    public string EffectiveQuote
    {
        get
        {
            var basis = EffectiveBase;
            return QuoteCurrency.Length > 0 && !string.Equals(QuoteCurrency, basis, StringComparison.Ordinal)
                ? QuoteCurrency
                : ExchangeRules.DefaultQuote(basis);
        }
    }

    /// <summary>[S-5144] A base or quote the ECB does not publish follows the default, loudly.</summary>
    public override IReadOnlyList<string> Repair()
    {
        var repairs = new List<string>();
        BaseCurrency = Code(BaseCurrency, "base", "following the region", repairs);
        QuoteCurrency = Code(QuoteCurrency, "quote", "the default", repairs);
        return repairs;
    }

    /// <summary>Copy the pair onto <paramref name="target"/>.</summary>
    protected T CopyPairTo<T>(T target) where T : ExchangePairConfig
    {
        ArgumentNullException.ThrowIfNull(target);
        target.BaseCurrency = BaseCurrency;
        target.QuoteCurrency = QuoteCurrency;
        return target;
    }

    private static string Code(string? code, string what, string instead, List<string> repairs)
    {
        if (string.IsNullOrEmpty(code))
        {
            return string.Empty;
        }

        if (ExchangeRules.Normalise(code) is { } known)
        {
            return known;
        }

        repairs.Add($"exchange rates: {what} '{Clip(code)}' is not a published currency; {instead}");
        return string.Empty;
    }

    private static string Clip(string? text) => text is null ? "null" : text.Length <= 8 ? text : text[..8];
}
