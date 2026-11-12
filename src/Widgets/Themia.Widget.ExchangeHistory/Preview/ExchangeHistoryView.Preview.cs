#if DEBUG
using Themia.App.Services.Web;
using Themia.App.Shell.Gallery;
using Themia.Core.Widgets;
using Themia.Sdk.Samples;

namespace Themia.Widget.ExchangeHistory;

/// <summary>Debug renders only ([S-2056]): the sample table (<see cref="SampleRates"/>), the rate atom's, at its pair CHF/EUR.</summary>
internal sealed partial class ExchangeHistoryView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "stale"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (state is not ("sample" or "stale"))
        {
            return Task.CompletedTask;
        }

        // [S-2448] The card's own config with the sample pair.
        var sample = (ExchangeHistoryConfig)_config.Clone();
        sample.BaseCurrency = "CHF";
        sample.QuoteCurrency = "EUR";
        _config = sample;
        _table = state == "stale" ? SampleRates.Table().Stale(FetchStatus.Offline, null) : SampleRates.Table();
        _web = null;
        PublishTitle();
        Render();
        return Task.CompletedTask;
    }
}
#endif
