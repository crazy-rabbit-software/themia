#if DEBUG
using Themia.App.Services.Web;
using Themia.App.Shell.Gallery;
using Themia.Core.Widgets;
using Themia.Sdk.Samples;
using Themia.Widget.StockHistory;

namespace Themia.App.Widgets;

/// <summary>Debug renders only ([S-1917]): the SDK's sample quote (<see cref="SampleQuote"/>), the price atom's, its high and low marked.</summary>
internal sealed partial class StockHistoryView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["quote", "falling", "stale"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (state is not ("quote" or "falling" or "stale"))
        {
            return Task.CompletedTask;
        }

        // [S-2448] The card's own config with the sample symbol.
        var sample = (StockHistoryConfig)_config.Clone();
        sample.Symbol = "MSFT";
        sample.ShowMinMax = true;
        _config = sample;
        var quote = SampleQuote.Quote(state != "falling");
        _feed.Preview(state == "stale" ? quote.Stale(FetchStatus.Offline, null) : quote, FetchStatus.Ok);
        return Task.CompletedTask;
    }
}
#endif
