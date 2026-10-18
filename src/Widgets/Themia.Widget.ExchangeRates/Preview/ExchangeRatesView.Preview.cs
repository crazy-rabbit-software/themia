#if DEBUG
using Themia.App.Services.Web;
using Themia.App.Shell.Gallery;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// Debug renders only ([S-1917]): a fixture table (<c>sample</c>), ninety days of the ECB document,
/// so the ladder can be looked at without the network or the sync. Built through the SDK's parser.
/// Nothing is fetched or saved.
/// </summary>
internal sealed partial class ExchangeRatesView : IWidgetPreviewState
{
    public IReadOnlyList<string> PreviewStates { get; } = ["sample", "stale"];

    public Task ShowPreviewStateAsync(string state)
    {
        if (state is not ("sample" or "stale"))
        {
            return Task.CompletedTask;
        }

        // Ninety days of a gently wandering euro, as the ECB document would carry them.
        var day = DateOnly.FromDateTime(DateTime.Today.AddDays(-1));
        var xml = new System.Text.StringBuilder("<Envelope><Cube>");
        for (int i = 0, written = 0; written < 63; i++)
        {
            var date = day.AddDays(-i);
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                continue;
            }

            var t = 63 - written++;
            string Rate(double centre, double swing, double drift)
                => (centre + (Math.Sin(t / 7.0) * swing) + (drift * t)).ToString("0.#####", System.Globalization.CultureInfo.InvariantCulture);

            xml.Append("<Cube time=\"").Append(date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)).Append("\">")
                .Append("<Cube currency=\"USD\" rate=\"").Append(Rate(1.12, 0.012, 0.0002)).Append("\"/>")
                .Append("<Cube currency=\"CHF\" rate=\"").Append(Rate(0.932, 0.004, 0.00005)).Append("\"/>")
                .Append("<Cube currency=\"GBP\" rate=\"").Append(Rate(0.846, 0.006, -0.0001)).Append("\"/>")
                .Append("<Cube currency=\"JPY\" rate=\"").Append(Rate(177.3, 1.8, 0.02)).Append("\"/>")
                .Append("<Cube currency=\"SEK\" rate=\"").Append(Rate(11.16, 0.08, 0.001)).Append("\"/>")
                .Append("</Cube>");
        }

        xml.Append("</Cube></Envelope>");

        _config = new ExchangeRatesConfig { BaseCurrency = "CHF", QuoteCurrency = "EUR", Style = _config.Style };
        _table = new Fetched<RateTable>(FetchStatus.Ok, EcbWire.Parse(xml.ToString()).Value, null, DateTimeOffset.Now.AddHours(-2));
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
