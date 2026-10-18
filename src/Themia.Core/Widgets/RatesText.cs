namespace Themia.Core.Widgets;

/// <summary>[S-5145] The figures the exchange-rate widget prints.</summary>
public static class RatesText
{
    /// <summary>[S-5145] A day change, "+0.21%" / "−0.08%", or empty when there is no earlier day.</summary>
    public static string Change(double? percent) => percent is { } p ? MarketQuote.ChangePercent(p) : string.Empty;
}
