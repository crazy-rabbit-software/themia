using Themia.App.Services.Web;
using Themia.App.Widgets;
using Themia.Core.Widgets;
using Themia.Sdk.Samples;

namespace Themia.Widget.ExchangeHistory;

/// <summary>[S-11850] [S-2056] The rate history's sample: the sample table (<see cref="SampleRates"/>), the rate atom's, at its pair CHF/EUR.</summary>
internal sealed partial class ExchangeHistoryView : IWidgetSample
{
    public Task ShowSampleAsync()
    {
        HoldSample(SampleRates.Table());
        return Task.CompletedTask;
    }

    /// <summary>The card's own config with the sample pair ([S-2448]), and <paramref name="table"/> held as its answer.</summary>
    internal void HoldSample(Fetched<RateTable> table)
    {
        var sample = (ExchangeHistoryConfig)_config.Clone();
        sample.BaseCurrency = "CHF";
        sample.QuoteCurrency = "EUR";
        _config = sample;
        _table = table;
        _web = null;
        PublishTitle();
        Render();
    }
}
