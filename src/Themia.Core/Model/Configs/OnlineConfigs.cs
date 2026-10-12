using System.Text.Json.Serialization;
using Themia.Core.Widgets;

namespace Themia.Core.Model;

/// <summary>[S-5103] Which air quality index the widget reads out.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AirQualityScale>))]
public enum AirQualityScale
{
    /// <summary>The European index for a place in Europe, the US index everywhere else.</summary>
    Auto = 0,

    /// <summary>The European Environment Agency's index (EAQI), 0 to 100+.</summary>
    European = 1,

    /// <summary>The US EPA's index, 0 to 500.</summary>
    Us = 2,
}

/// <summary>
/// [S-5100] Air quality, UV and pollen at a place. A weather-family widget: the same place picker and
/// the same header rule ([S-728]), so it derives from <see cref="WeatherConfig"/>; the sync tells the
/// kinds apart ([S-5101]).
/// </summary>
public sealed class AirQualityConfig : WeatherConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.AirQuality;

    /// <summary>[S-5103] Auto picks by the place, not by the user's region: the index is about the air there.</summary>
    public AirQualityScale Scale { get; set; } = AirQualityScale.Auto;

    public override WidgetConfig Clone()
    {
        var copy = new AirQualityConfig { Scale = Scale };
        CopyTo(copy);
        return copy;
    }

    public override IReadOnlyList<string> Repair()
    {
        if (Enum.IsDefined(Scale))
        {
            return [];
        }

        var was = Scale;
        Scale = AirQualityScale.Auto;
        return [$"air quality: index {(int)was} is not one; Auto"];
    }
}

/// <summary>[S-5120] Rain in the next two hours at a place. A weather-family widget, like air quality.</summary>
public sealed class RainNextHourConfig : WeatherConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.RainNextHour;

    public override WidgetConfig Clone()
    {
        var copy = new RainNextHourConfig();
        CopyTo(copy);
        return copy;
    }
}

/// <summary>
/// [S-5140] The ECB's reference rates for one base currency against a short list of others.
/// </summary>
public sealed class ExchangeRatesConfig : WidgetConfig
{
    [JsonIgnore]
    public override WidgetKind Kind => WidgetKind.ExchangeRates;

    /// <summary>[S-5143] The base currency, an ISO 4217 code the ECB publishes; empty follows the Windows region.</summary>
    public string BaseCurrency { get; set; } = string.Empty;

    /// <summary>[S-5143] The currencies priced in the base, in the order shown.</summary>
    public List<string> Currencies { get; set; } = [.. ExchangeRules.DefaultQuotes];

    /// <summary>[S-5146] The 90-day line under each rate, where there is room.</summary>
    public bool ShowChart { get; set; } = true;

    /// <summary>The base in force: the stored one, or the Windows region's currency ([S-5143]).</summary>
    [JsonIgnore]
    public string EffectiveBase => BaseCurrency.Length > 0 ? BaseCurrency : RegionCurrency.Current;

    /// <summary>The currencies to show: the list without the base itself (one CHF is one CHF).</summary>
    [JsonIgnore]
    public IReadOnlyList<string> EffectiveQuotes
    {
        get
        {
            var basis = EffectiveBase;
            return [.. Currencies.Where(c => !string.Equals(c, basis, StringComparison.Ordinal))];
        }
    }

    public override WidgetConfig Clone() => new ExchangeRatesConfig
    {
        BaseCurrency = BaseCurrency,
        Currencies = [.. Currencies],
        ShowChart = ShowChart,
    };

    /// <summary>[S-5144] Codes the ECB does not publish are dropped, loudly; an empty list is the default.</summary>
    public override IReadOnlyList<string> Repair()
    {
        var repairs = new List<string>();

        if (BaseCurrency.Length > 0 && ExchangeRules.Normalise(BaseCurrency) is not { } basis)
        {
            repairs.Add($"exchange rates: base '{Clip(BaseCurrency)}' is not a published currency; following the region");
            BaseCurrency = string.Empty;
        }
        else if (BaseCurrency.Length > 0)
        {
            BaseCurrency = ExchangeRules.Normalise(BaseCurrency)!;
        }

        var kept = new List<string>();
        foreach (var code in Currencies ?? [])
        {
            if (ExchangeRules.Normalise(code) is not { } known)
            {
                repairs.Add($"exchange rates: '{Clip(code)}' is not a published currency; dropped");
            }
            else if (!kept.Contains(known))
            {
                kept.Add(known);
            }
        }

        if (kept.Count == 0)
        {
            repairs.Add("exchange rates: no currency left; the defaults");
            kept.AddRange(ExchangeRules.DefaultQuotes);
        }

        Currencies = kept;
        return repairs;
    }

    private static string Clip(string? text) => text is null ? "null" : text.Length <= 8 ? text : text[..8];
}
