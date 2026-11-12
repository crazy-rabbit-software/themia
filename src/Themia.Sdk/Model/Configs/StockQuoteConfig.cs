using System.Text.Json.Serialization;
using Themia.Core.Localization;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk;

namespace Themia.Core.Model;

/// <summary>
/// [S-11160] What the stock atoms share: one instrument and its period. The price (<c>stock</c>) and its
/// chart (<c>stockHistory</c>) each hold it; set to the same symbol, period and extended hours they ask
/// the sync one quote ([S-2663]) and agree to the digit ([S-11171]): the change, its colour and the line
/// are one baseline ([S-2775]).
/// <para>
/// One symbol per widget, as in the original: a desktop that wants three prices holds three widgets.
/// </para>
/// </summary>
public abstract class StockQuoteConfig : WidgetConfig, ISetupRule, IInterests
{
    /// <summary>[S-2608] What the background sync keeps fresh for it.</summary>
    public void AddInterests(InterestsBuilder interests)
    {
        ArgumentNullException.ThrowIfNull(interests);
        if (Interest(UpdateCadence.Interval(interests.Refresh, HostCadences.Markets)) is { } wants)
        {
            interests.Of<QuoteInterest>().Add(wants);
        }
    }

    /// <summary>[S-2663] What it asks for, or null without a usable symbol: the one rule the widget and the sync share.</summary>
    public QuoteInterest? Interest() => Interest(UpdateCadence.Interval(HostCadences.Markets));

    /// <summary>[S-2663] <see cref="Interest()"/> at a given cadence ([S-3780]).</summary>
    public QuoteInterest? Interest(TimeSpan interval)
        => EffectiveSymbol is { Length: > 0 } symbol
            ? new QuoteInterest(symbol, Market, Range, WantsPrePost, interval)
            : null;

    /// <summary>[S-6906] It waits for setup while this holds.</summary>
    public bool NeedsSetup(FeatureSet signedIn) => string.IsNullOrWhiteSpace(Symbol);

    /// <summary>[S-1862] What the Set up row says beside its button: "Choose a symbol".</summary>
    string? ISetupRule.SetupSentence => Str.Stocks_Setup.Text();

    /// <summary>The Yahoo symbol or CoinGecko id. Empty until the user sets one.</summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>[S-3790] Which service quotes it: set by a search pick (<see cref="Choose"/>) or a typed symbol (<see cref="ChooseTyped"/>).</summary>
    public QuoteMarket Market { get; set; } = QuoteMarket.Listed;

    /// <summary>[S-3790] The instrument's name from the search pick: the header's title. Empty shows the symbol.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>[S-2778] The period: the change's baseline and the chart's span.</summary>
    public QuoteRange Range { get; set; } = QuoteRange.Day;

    /// <summary>[S-1308] Ask for pre-market and after-hours bars (one-day and five-day periods only). Off by default.</summary>
    public bool ShowExtendedHours { get; set; }

    /// <summary>Whether extended hours exist and fit the period: a listed symbol over one or five days.</summary>
    [JsonIgnore]
    public bool OffersExtendedHours => Market == QuoteMarket.Listed && Range is QuoteRange.Day or QuoteRange.FiveDays;

    /// <summary>Whether the request asks for extended-hours bars.</summary>
    [JsonIgnore]
    public bool WantsPrePost => ShowExtendedHours && OffersExtendedHours;

    /// <summary>The symbol as it is sent and displayed, or empty when it is not usable.</summary>
    [JsonIgnore]
    public string EffectiveSymbol => WebInput.IsPlausibleSymbol(Symbol)
        ? WebInput.NormaliseSymbol(Symbol, Market == QuoteMarket.Crypto)
        : string.Empty;

    /// <summary>[S-3780] The shared markets cadence (⚙ › Online updates); a minute is its floor.</summary>
    [JsonIgnore]
    public TimeSpan TickInterval => UpdateCadence.Interval(HostCadences.Markets);

    /// <summary>[S-3790] A search pick: its symbol, its service and its name, in one go.</summary>
    public void Choose(SymbolHit hit)
    {
        ArgumentNullException.ThrowIfNull(hit);
        Symbol = hit.Symbol;
        Market = hit.Market;
        DisplayName = hit.Name;
    }

    /// <summary>[S-3790] A symbol typed in full and committed: a listed symbol, with no name of its own. Unchanged text changes nothing.</summary>
    public void ChooseTyped(string symbol)
    {
        var typed = (symbol ?? string.Empty).Trim();
        if (typed == Symbol)
        {
            return;
        }

        Symbol = typed;
        Market = QuoteMarket.Listed;
        DisplayName = string.Empty;
    }

    /// <summary>[S-2039] What a click on the card opens: Yahoo Finance for a listed symbol, CoinGecko for a coin; nothing without a symbol.</summary>
    public CardOpen? Page()
    {
        var symbol = EffectiveSymbol;
        if (symbol.Length == 0)
        {
            return null;
        }

        return CardOpen.Web(Market == QuoteMarket.Crypto
            ? $"https://www.coingecko.com/en/coins/{Uri.EscapeDataString(symbol.ToLowerInvariant())}"
            : $"https://finance.yahoo.com/quote/{Uri.EscapeDataString(symbol)}/");
    }

    /// <summary>Copy the instrument and its period onto <paramref name="target"/>.</summary>
    protected T CopyQuoteTo<T>(T target) where T : StockQuoteConfig
    {
        ArgumentNullException.ThrowIfNull(target);
        target.Symbol = Symbol;
        target.Market = Market;
        target.DisplayName = DisplayName;
        target.Range = Range;
        target.ShowExtendedHours = ShowExtendedHours;
        return target;
    }
}
