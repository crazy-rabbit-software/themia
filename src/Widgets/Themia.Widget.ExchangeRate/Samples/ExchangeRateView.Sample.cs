using Themia.App.Services.Web;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Samples;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11850] [S-2056] The exchange rate's sample: the sample table (the SDK's <see cref="SampleRates"/>),
/// built through the SDK's parser, at the pair CHF/EUR. Nothing is fetched or saved. Its latest day is
/// the working day before the sample day ([S-2050]), so the header's date agrees with the calendar beside it.
/// </summary>
internal sealed partial class ExchangeRateView : IWidgetSample
{
    public Task ShowSampleAsync()
    {
        HoldSample(SampleRates.Table());
        return Task.CompletedTask;
    }

    /// <summary>The card's own config with the sample pair ([S-2448]), and <paramref name="table"/> held as its answer.</summary>
    internal void HoldSample(Fetched<RateTable> table)
    {
        var sample = (ExchangeRateConfig)_config.Clone();
        sample.BaseCurrency = "CHF";
        sample.QuoteCurrency = "EUR";
        _config = sample;
        _table = table;
        _web = null;
        PublishTitle();
        Render();
    }
}
