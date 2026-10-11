using System.Text.Json.Serialization;

namespace Themia.App.Services.Web;

// The wire shapes the four network widgets read, and nothing else.
//
// Declared by hand, with an explicit [JsonPropertyName] on every member, for two reasons.
//
// Native AOT is the first: the reflection-based JsonSerializer overloads are trimmed out of the
// published app, so every type that crosses the wire has to be reachable from a source-generated
// context. A type that is missing from WebJsonContext below does not fail the build; it throws the
// first time the widget fetches, in the shipped binary, on somebody's desktop.
//
// The second is that these are other people's schemas. open-meteo is snake_case, Yahoo is
// camelCase, CoinGecko is snake_case with a dynamic top-level key; no single naming policy fits all
// three, and a policy that silently fails to bind a field yields a null that looks exactly like a
// field the service did not send. Spelling each one out means a rename upstream shows up as an
// empty readout rather than as a plausible wrong number.
//
// Every numeric field is nullable. These services pad, omit and null fields freely — an illiquid
// minute has a null close, a forecast day can be missing its precipitation total — and a
// non-nullable double would bind those to 0.0, which is a price and a temperature.

// ---- open-meteo ----------------------------------------------------------------------------

/// <summary>A reply from <c>api.open-meteo.com/v1/forecast</c>. Both blocks are optional.</summary>
internal sealed class OpenMeteoReply
{
    [JsonPropertyName("current")]
    public OpenMeteoCurrent? Current { get; init; }

    [JsonPropertyName("daily")]
    public OpenMeteoDaily? Daily { get; init; }

    /// <summary>open-meteo answers an invalid request with <c>{"error":true,"reason":"..."}</c>.</summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; init; }
}

internal sealed class OpenMeteoCurrent
{
    [JsonPropertyName("temperature_2m")]
    public double? Temperature { get; init; }

    [JsonPropertyName("apparent_temperature")]
    public double? ApparentTemperature { get; init; }

    [JsonPropertyName("relative_humidity_2m")]
    public double? Humidity { get; init; }

    [JsonPropertyName("wind_speed_10m")]
    public double? WindSpeed { get; init; }

    [JsonPropertyName("weather_code")]
    public int? WeatherCode { get; init; }

    [JsonPropertyName("precipitation")]
    public double? Precipitation { get; init; }

    [JsonPropertyName("pressure_msl")]
    public double? Pressure { get; init; }

    [JsonPropertyName("wind_direction_10m")]
    public double? WindDirection { get; init; }

    [JsonPropertyName("wind_gusts_10m")]
    public double? WindGusts { get; init; }

    [JsonPropertyName("cloud_cover")]
    public double? CloudCover { get; init; }

    [JsonPropertyName("uv_index")]
    public double? UvIndex { get; init; }
}

/// <summary>
/// The daily block, as five parallel arrays. They are supposed to be the same length; the parser
/// walks the shortest of them rather than trusting that.
/// </summary>
internal sealed class OpenMeteoDaily
{
    [JsonPropertyName("time")]
    public List<string?>? Time { get; init; }

    [JsonPropertyName("weather_code")]
    public List<int?>? WeatherCode { get; init; }

    [JsonPropertyName("temperature_2m_max")]
    public List<double?>? High { get; init; }

    [JsonPropertyName("temperature_2m_min")]
    public List<double?>? Low { get; init; }

    [JsonPropertyName("precipitation_sum")]
    public List<double?>? Precipitation { get; init; }

    [JsonPropertyName("precipitation_probability_max")]
    public List<double?>? PrecipitationChance { get; init; }

    [JsonPropertyName("wind_speed_10m_max")]
    public List<double?>? WindMax { get; init; }

    [JsonPropertyName("uv_index_max")]
    public List<double?>? UvMax { get; init; }

    /// <summary>Local wall-clock times, "2026-10-04T07:31".</summary>
    [JsonPropertyName("sunrise")]
    public List<string?>? Sunrise { get; init; }

    [JsonPropertyName("sunset")]
    public List<string?>? Sunset { get; init; }
}

/// <summary>A reply from <c>geocoding-api.open-meteo.com/v1/search</c>.</summary>
internal sealed class GeocodeReply
{
    /// <summary>Absent, not empty, when nothing matched — which is why it is nullable.</summary>
    [JsonPropertyName("results")]
    public List<GeocodeHit>? Results { get; init; }
}

internal sealed class GeocodeHit
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("latitude")]
    public double? Latitude { get; init; }

    [JsonPropertyName("longitude")]
    public double? Longitude { get; init; }

    [JsonPropertyName("country")]
    public string? Country { get; init; }

    [JsonPropertyName("admin1")]
    public string? Admin1 { get; init; }

    /// <summary>The county or district: what tells two Springfields in one state apart ([S-2842]).</summary>
    [JsonPropertyName("admin2")]
    public string? Admin2 { get; init; }
}

// ---- Yahoo Finance -------------------------------------------------------------------------

internal sealed class YahooChartReply
{
    [JsonPropertyName("chart")]
    public YahooChart? Chart { get; init; }
}

internal sealed class YahooChart
{
    [JsonPropertyName("result")]
    public List<YahooResult>? Result { get; init; }
}

internal sealed class YahooResult
{
    [JsonPropertyName("meta")]
    public YahooMeta? Meta { get; init; }

    [JsonPropertyName("timestamp")]
    public List<long?>? Timestamp { get; init; }

    [JsonPropertyName("indicators")]
    public YahooIndicators? Indicators { get; init; }
}

internal sealed class YahooIndicators
{
    [JsonPropertyName("quote")]
    public List<YahooQuoteSeries>? Quote { get; init; }
}

internal sealed class YahooQuoteSeries
{
    /// <summary>Nulls are real: illiquid minutes are padded so the array stays aligned with the timestamps.</summary>
    [JsonPropertyName("close")]
    public List<double?>? Close { get; init; }
}

internal sealed class YahooMeta
{
    [JsonPropertyName("symbol")]
    public string? Symbol { get; init; }

    [JsonPropertyName("shortName")]
    public string? ShortName { get; init; }

    [JsonPropertyName("longName")]
    public string? LongName { get; init; }

    [JsonPropertyName("currency")]
    public string? Currency { get; init; }

    [JsonPropertyName("regularMarketPrice")]
    public double? RegularMarketPrice { get; init; }

    [JsonPropertyName("chartPreviousClose")]
    public double? ChartPreviousClose { get; init; }

    [JsonPropertyName("previousClose")]
    public double? PreviousClose { get; init; }

    /// <summary>The venue's tick precision: 2 for a share, 4 for an FX pair.</summary>
    [JsonPropertyName("priceHint")]
    public int? PriceHint { get; init; }

    [JsonPropertyName("instrumentType")]
    public string? InstrumentType { get; init; }

    /// <summary>Today's pre, regular and post windows, already resolved for the exchange's calendar.</summary>
    [JsonPropertyName("currentTradingPeriod")]
    public YahooTradingPeriod? CurrentTradingPeriod { get; init; }

    // [S-1309] The exchange's zone: its offset now, in seconds, and its short name ("EDT").
    [JsonPropertyName("gmtoffset")]
    public long? GmtOffset { get; init; }

    [JsonPropertyName("timezone")]
    public string? Timezone { get; init; }

    [JsonPropertyName("hasPrePostMarketData")]
    public bool? HasPrePostMarketData { get; init; }
}

/// <summary>[S-1307] <c>v1/finance/search</c>: the instruments matching a query.</summary>
internal sealed class YahooSearchReply
{
    [JsonPropertyName("quotes")]
    public List<YahooSearchQuote>? Quotes { get; init; }
}

internal sealed class YahooSearchQuote
{
    [JsonPropertyName("symbol")]
    public string? Symbol { get; init; }

    [JsonPropertyName("shortname")]
    public string? ShortName { get; init; }

    [JsonPropertyName("longname")]
    public string? LongName { get; init; }

    [JsonPropertyName("quoteType")]
    public string? QuoteType { get; init; }

    [JsonPropertyName("exchDisp")]
    public string? Exchange { get; init; }
}

internal sealed class YahooTradingPeriod
{
    [JsonPropertyName("pre")]
    public YahooWindow? Pre { get; init; }

    [JsonPropertyName("regular")]
    public YahooWindow? Regular { get; init; }

    [JsonPropertyName("post")]
    public YahooWindow? Post { get; init; }
}

/// <summary>One trading window. Bounds are epoch <b>seconds</b>.</summary>
internal sealed class YahooWindow
{
    [JsonPropertyName("start")]
    public double? Start { get; init; }

    [JsonPropertyName("end")]
    public double? End { get; init; }
}

// ---- CoinGecko -----------------------------------------------------------------------------

/// <summary>
/// <c>simple/price</c> answers with the coin id as the top-level key, so the reply is a dictionary
/// rather than a type: <c>{"bitcoin":{"usd":84470,"usd_24h_change":0.18}}</c>.
/// </summary>
internal sealed class CoinPrice
{
    [JsonPropertyName("usd")]
    public double? Usd { get; init; }

    [JsonPropertyName("usd_24h_change")]
    public double? Change24H { get; init; }
}

/// <summary>[S-1307] CoinGecko's <c>search</c>: the coins matching a query.</summary>
internal sealed class CoinSearchReply
{
    [JsonPropertyName("coins")]
    public List<CoinSearchHit>? Coins { get; init; }
}

internal sealed class CoinSearchHit
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("symbol")]
    public string? Symbol { get; init; }
}

/// <summary><c>coins/{id}/market_chart</c>: an array of <c>[epochMilliseconds, price]</c> pairs.</summary>
internal sealed class CoinChartReply
{
    [JsonPropertyName("prices")]
    public List<List<double>>? Prices { get; init; }
}

/// <summary>
/// The source-generated metadata for everything above.
/// <para>
/// No naming policy on purpose: every property carries its own name, so adding a type here without
/// annotating it produces an obviously empty object rather than a half-bound one.
/// </para>
/// </summary>
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = false)]
[JsonSerializable(typeof(OpenMeteoReply))]
[JsonSerializable(typeof(GeocodeReply))]
[JsonSerializable(typeof(YahooChartReply))]
[JsonSerializable(typeof(Dictionary<string, CoinPrice>))]
[JsonSerializable(typeof(CoinChartReply))]
[JsonSerializable(typeof(YahooSearchReply))]
[JsonSerializable(typeof(CoinSearchReply))]
internal sealed partial class WebJsonContext : JsonSerializerContext;
