#if DEBUG
using Themia.App.Services.Web;
using Themia.App.Shell.Gallery;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Samples;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917], [S-2056]): the sample table (<c>sample</c>, the SDK's <see cref="SampleRates"/>), so the rate can be looked at without the network or the sync.
/// Built through the SDK's parser. Nothing is fetched or saved. Its latest day is the working day
/// before the sample day ([S-2050]), so the header's date agrees with the calendar beside it.
/// </summary>
internal sealed partial class ExchangeRateView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "stale"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (state is not ("sample" or "stale"))
        {
            return Task.CompletedTask;
        }

        _config = new ExchangeRateConfig { BaseCurrency = "CHF", QuoteCurrency = "EUR" };
        _table = SampleRates.Table();
        if (state == "stale")
        {
            // [S-7042] The answer on screen, and the last refresh failed: the header's problem glyph.
            _table = _table.Stale(FetchStatus.Offline, null);
        }
        _web = null;
        PublishTitle();
        Render();
        return Task.CompletedTask;
    }
}
#endif
