using System.Text.Json.Serialization;
using Themia.App.Shell;
using Themia.Core.Accounts;
using Themia.Core.Localization;
using Themia.Core.Storage;
using Themia.Core.Sync;
using Themia.Core.Widgets;
using Themia.Sdk;

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

/// <summary>[S-6165] The stock widget's styles: the same quote, two looks. What was its chart switch.</summary>
[JsonConverter(typeof(StyleJsonConverter<StocksStyle>))]
public enum StocksStyle
{
    /// <summary>[S-1311] The price and its change over the period's chart.</summary>
    Chart = 0,

    /// <summary>[S-6166] The price and its change large, no chart.</summary>
    Number = 1,
}

/// <summary>[S-6074] The stock's rows that only some styles, markets or periods use.</summary>
public enum StocksSetting
{
    /// <summary>Dim when the market is closed: not for crypto.</summary>
    MarketState,

    /// <summary>Extended hours: a listed symbol over one or five days.</summary>
    ExtendedHours,

    /// <summary>The high and low markers: the chart style.</summary>
    MinMax,
}

/// <summary>
/// One instrument's price, change and recent shape.
/// <para>
/// One symbol per widget, as in the original: a list of tickers in a 180px tile is four lines of
/// four-point type, and a desktop that wants three prices can hold three widgets, each of which can
/// then be the size its reader wants.
/// </para>
/// </summary>
public sealed class StocksConfig : WidgetConfig, IStyled, IShownSettings<StocksSetting>, IContentAligned, ISetupRule, IInterests
{
    /// <summary>[S-2608] What the background sync keeps fresh for it.</summary>
    public void AddInterests(InterestsBuilder interests)
    {
        if (SyncInterests.For(this, UpdateCadence.Interval(interests.Refresh, UpdateSource.Markets)) is { } wants)
        {
            interests.Quotes.Add(wants);
        }
    }

    /// <summary>[S-6906] It waits for setup while this holds.</summary>
    public bool NeedsSetup(AccountFeatures signedIn) => string.IsNullOrWhiteSpace(Symbol);

    private static readonly StyleOption[] StockStyles =
    [
        StyleOption.Of(StocksStyle.Chart, Str.Styles_Stocks_Chart, 0xE9D2),
        StyleOption.Of(StocksStyle.Number, Str.Styles_Stocks_Number, 0xE75F),
    ];


    /// <summary>[S-6902] This widget's descriptor: what the host knows of it without its UI.</summary>
    public static WidgetDescriptor Descriptor { get; } = new()
    {
        Id = new("stocks"),
        ConfigType = typeof(StocksConfig),
        CreateDefault = () => new StocksConfig(),
        Json = WidgetConfigJsonContext.Default,
        Keywords = ["shares", "ticker", "market", "finance", "crypto", "bitcoin", "forex", "currency", "portfolio", "price"],
        Label = Str.Widget_Stocks_Label.Local(),
        Description = Str.WidgetsA_Catalog_Stocks_Description.Local(),
        Glyph = Glyphs.Stocks,
        DefaultWidth = 320,
        DefaultHeight = 240,
        Traits = WidgetTraits.CanHaveHeader,
        Add = new(AddFolderId.Markets, 0, 0),
    };

    /// <summary>[S-6581] Only this style is one block to align.</summary>
    bool IContentAligned.AlignApplies => Style == StocksStyle.Number;

    /// <summary>The Yahoo symbol or CoinGecko id. Empty until the user sets one.</summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>
    /// [S-3790] Which service quotes it. Never chosen by hand: a search pick sets it from what the
    /// hit is (<see cref="Choose(SymbolHit)"/>), and a symbol typed in full is a listed one
    /// (<see cref="ChooseTyped"/>) — Yahoo quotes coins too, as <c>BTC-USD</c>.
    /// </summary>
    public QuoteMarket Market { get; set; } = QuoteMarket.Listed;

    /// <summary>[S-3790] The instrument's name from the search pick: the header's title. Empty shows the symbol.</summary>
    public string DisplayName { get; set; } = string.Empty;

    public QuoteRange Range { get; set; } = QuoteRange.Day;

    /// <summary>[S-6165] The widget's look: the chart, or the number alone.</summary>
    public StocksStyle Style { get; set; } = StocksStyle.Chart;

    IReadOnlyList<StyleOption> IStyled.Styles => StockStyles;

    int IStyled.StyleValue
    {
        get => (int)Style;
        set => Style = (StocksStyle)value;
    }

    /// <summary>Say so when the listing exchange is shut. Crypto is never marked: it trades all week.</summary>
    public bool ShowMarketState { get; set; } = true;

    /// <summary>
    /// [S-1308] Ask for pre-market and after-hours bars (one-day and five-day charts only) and show
    /// the last extended print under the price. Off by default, as in the original.
    /// </summary>
    public bool ShowExtendedHours { get; set; }

    /// <summary>[S-1310] Mark the period's high and low on the chart.</summary>
    public bool ShowMinMax { get; set; }

    /// <summary>[S-1310] The colour of a rise, <c>#RRGGBB</c>; empty = the theme's success green.</summary>
    public string UpColor { get; set; } = string.Empty;

    /// <summary>[S-1310] The colour of a fall, <c>#RRGGBB</c>; empty = the theme's critical red.</summary>
    public string DownColor { get; set; } = string.Empty;

    /// <summary>
    /// [S-2776] How many of the instrument the user owns, for the holding's worth under the price.
    /// Null (the default) is none and is not shown; a stored 0, negative or unusable amount is
    /// repaired to none at load (<see cref="StockHolding.Normalise"/>).
    /// </summary>
    public double? SharesOwned { get; set; }

    /// <summary>
    /// [S-6074] The rows the page offers: the market's open/closed dimming where a market closes
    /// (not crypto, which trades round the clock), extended hours where they exist and fit (a listed
    /// symbol over one or five days, <see cref="WantsPrePost"/>), and the high and low markers on
    /// the chart style only.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<StocksSetting> ShownSettings
    {
        get
        {
            var shown = new List<StocksSetting>();
            if (Market != QuoteMarket.Crypto)
            {
                shown.Add(StocksSetting.MarketState);
            }

            if (Market == QuoteMarket.Listed && Range is QuoteRange.Day or QuoteRange.FiveDays)
            {
                shown.Add(StocksSetting.ExtendedHours);
            }

            if (Style == StocksStyle.Chart)
            {
                shown.Add(StocksSetting.MinMax);
            }

            return shown;
        }
    }

    /// <summary>
    /// Whether the request asks for extended-hours bars: only where they exist (a listed symbol)
    /// and where they fit the chart (one and five days).
    /// </summary>
    [JsonIgnore]
    public bool WantsPrePost => ShowExtendedHours && Market == QuoteMarket.Listed && Range is QuoteRange.Day or QuoteRange.FiveDays;

    /// <summary>The symbol as it is sent and displayed, or empty when it is not usable.</summary>
    [JsonIgnore]
    public string EffectiveSymbol => WebInput.IsPlausibleSymbol(Symbol)
        ? WebInput.NormaliseSymbol(Symbol, Market == QuoteMarket.Crypto)
        : string.Empty;

    /// <summary>[S-3780] The shared markets cadence (Settings › Widgets); a minute is its floor.</summary>
    [JsonIgnore]
    public TimeSpan TickInterval => Sync.UpdateCadence.Interval(Sync.UpdateSource.Markets);

    /// <summary>[S-3790] A search pick: its symbol, its service and its name, in one go.</summary>
    public void Choose(SymbolHit hit)
    {
        ArgumentNullException.ThrowIfNull(hit);

        Symbol = hit.Symbol;
        Market = hit.Market;
        DisplayName = hit.Name;
    }

    /// <summary>
    /// [S-3790] A symbol typed in full and committed: a listed symbol, with no name of its own (the
    /// header shows the service's name for it). Unchanged text changes nothing.
    /// </summary>
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

    /// <summary>A hand-edited colour that is not <c>#RRGGBB</c> goes back to the theme's; an unusable amount owned to none.</summary>
    public override IReadOnlyList<string> Repair()
    {
        var repairs = new List<string>();

        foreach (var (name, value, set) in new (string, string?, Action<string>)[]
        {
            ("up colour", UpColor, v => UpColor = v),
            ("down colour", DownColor, v => DownColor = v),
        })
        {
            var normal = HexColor.Normalise(value);

            if (!string.IsNullOrEmpty(value) && normal.Length == 0)
            {
                repairs.Add($"stocks: {name} '{value}' is not a colour");
            }

            set(normal);
        }

        SharesOwned = StockHolding.Normalise(SharesOwned, out var problem);

        if (problem is not null)
        {
            repairs.Add(problem);
        }

        return repairs;
    }

    public override WidgetConfig Clone() => new StocksConfig
    {
        Symbol = Symbol,
        Market = Market,
        DisplayName = DisplayName,
        Range = Range,
        Style = Style,
        ShowMarketState = ShowMarketState,
        ShowExtendedHours = ShowExtendedHours,
        ShowMinMax = ShowMinMax,
        UpColor = UpColor,
        DownColor = DownColor,
        SharesOwned = SharesOwned,
    };
}
