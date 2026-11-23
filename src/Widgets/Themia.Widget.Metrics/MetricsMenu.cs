using Themia.App.Services;
using Themia.App.Shell;
using Themia.App.Shell.Config;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Settings;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.WinUI;

namespace Themia.Widget.Metrics;

/// <summary>The lists the page reads that only the running host has (a test hands its own).</summary>
/// <param name="Drives">The drives Windows lists.</param>
/// <param name="Adapters">The network adapters.</param>
/// <param name="Gpus">The GPU adapters.</param>
/// <param name="Lookup">The symbol search.</param>
internal sealed record MetricsMenuSources(
    Func<IReadOnlyList<DriveEntry>> Drives,
    Func<IReadOnlyList<NetworkAdapterEntry>> Adapters,
    Func<IReadOnlyList<GpuAdapter>> Gpus,
    Func<string, CancellationToken, Task<IReadOnlyList<SymbolHit>>> Lookup)
{
    /// <summary>The host's own lists, each read only where the metric's line needs it.</summary>
    public static MetricsMenuSources Host() => new(DriveList.Now, AdapterList.Now, GpuAdapters.List, SymbolLookup.FindAsync);
}

/// <summary>
/// [S-16308] The widget's page ([S-3719]): the subject and the metric (grouped by subject, a long subject's metrics found by
/// typing), what the metric reads from (the shared place, drive, adapter, host and GPU lines; the price widgets' symbol and the
/// exchange rate's pair), its step where it has two, history or forecast where it has both, the span; under the Style line the
/// graph's type, only those the metric's shape draws. A line with one option is not there.
/// </summary>
internal static class MetricsMenu
{
    /// <summary>A choice longer than this is found by typing ([S-3537]): the page shows that many options at once.</summary>
    private const int Shown = 8;

    public static void Lines(WidgetMenuLines menu, MetricsConfig config, MetricsMenuSources sources)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(sources);
        var def = config.Def;

        // [S-16308] The subject, then its metrics.
        menu.Lines.Add(menu.Choice(Glyphs.Apps, MetricsStr.Metrics_Menu_Subject.Text(),
            [.. Enum.GetValues<MetricSubject>().Select(s => (s, Subject(s)))],
            def.Subject,
            s => config.Choose(MetricCatalog.All.First(m => m.Subject == s).Id)));
        var metrics = MetricCatalog.All.Where(m => m.Subject == def.Subject).ToList();
        var metric = menu.Choice(def.Glyph, MetricsStr.Metrics_Menu_Metric.Text(), [.. metrics.Select(m => (m.Id, m.Name.Text()))], config.Metric, config.Choose);
        menu.Lines.Add(metrics.Count > Shown ? metric with { Filter = MetricsStr.Metrics_Menu_Find.Text() } : metric);

        // [S-16308] What it reads from.
        menu.Lines.AddRange(Source(menu, config, sources));

        // [S-16302] Its step, its way and its span.
        var steps = MetricCatalog.Steps(def);
        if (steps.Count > 1)
        {
            menu.Lines.Add(menu.Choice(Glyphs.Calendar, MetricsStr.Metrics_Menu_Step.Text(), [.. steps.Select(s => (s, Step(s)))], config.Step, config.ChooseStep));
        }

        var feed = config.Feed;
        var span = config.SpanInForce;
        var ways = MetricCatalog.Ways(feed);
        if (ways.Count > 1)
        {
            menu.Lines.Add(menu.Choice(Glyphs.History, MetricsStr.Metrics_Menu_Way.Text(), [.. ways.Select(w => (w, Way(w)))], span.Way, config.ChooseWay));
        }

        var spans = MetricCatalog.Spans(feed, span.Way);
        if (spans.Count > 1)
        {
            menu.Lines.Add(menu.Choice(Glyphs.Hours, MetricsStr.Metrics_Menu_Span.Text(), [.. spans.Select(s => (s.Count, SpanLabel(s)))], span.Count, count => config.Span = count));
        }

        // [S-16304] The graph's type, where the style draws one and the shape offers a choice.
        var graphs = MetricCatalog.Graphs(feed.Shape);
        if (config.Style == MetricsStyle.Graph && graphs.Count > 1)
        {
            menu.StyleLines.Add(menu.Choice(Glyphs.ChartType, MetricsStr.Metrics_Menu_Graph.Text(), [.. graphs.Select(g => (g, Graph(g)))], config.GraphInForce, g => config.Graph = g));
        }

        // [S-16308] The data's credit, where it is a service's.
        if (Credit(feed.Source) is { } credit)
        {
            menu.Lines.Add(SettingsLine.Separator);
            menu.Lines.Add(WidgetMenuLines.Note(Str.Online_Source.Format(("name", credit))));
        }
    }

    /// <summary>[S-16308] The lines of what the metric reads from: none for a metric that reads this PC as a whole.</summary>
    private static IEnumerable<SettingsLine> Source(WidgetMenuLines menu, MetricsConfig config, MetricsMenuSources sources)
    {
        switch (config.Def.Need)
        {
            case MetricNeed.Place:
                foreach (var line in WeatherPlaceRows.Lines(menu, config))
                {
                    yield return line;
                }

                break;
            case MetricNeed.Drive:
                yield return DiskMenuLines.Drive(menu, config, sources.Drives());
                break;
            case MetricNeed.NetworkAdapter:
                yield return AdapterMenuLines.Adapter(menu, config, sources.Adapters());
                break;
            case MetricNeed.PingHost:
                yield return WidgetMenuLines.Shared(SharedSettings.PingHost, PingHostLine.Line(menu, config.Host, value => config.Host = value));
                break;
            case MetricNeed.GpuAdapter when GpuAdapterLine.Line(menu, config.GpuAdapter, value => config.GpuAdapter = value, sources.Gpus()) is { } gpu:
                yield return gpu;
                break;
            case MetricNeed.Symbol:
            {
                // The price widgets' symbol search over the stock's settings in their shape, written back before the save.
                var quote = config.Quote();
                var through = Through(menu, () => config.Take(quote));
                yield return StockQuoteLines.Symbol(through, quote, sources.Lookup) with { Shares = null };
                break;
            }

            case MetricNeed.CurrencyPair:
            {
                var pair = config.Pair();
                var through = Through(menu, () => config.Take(pair));
                foreach (var line in ExchangePairLines.Lines(through, pair, Glyphs.CurrencyExchange).Where(l => l is not SettingsSeparator and not SettingsNote))
                {
                    yield return line with { Shares = null };
                }

                break;
            }
        }
    }

    /// <summary>A page whose writes go to <paramref name="back"/> first, then <paramref name="menu"/>'s save.</summary>
    private static WidgetMenuLines Through(WidgetMenuLines menu, Action back)
        => new(() =>
        {
            back();
            menu.Save();
        })
        {
            OpenSetting = menu.OpenSetting,
            Device = menu.Device,
            OpenLocationSettings = menu.OpenLocationSettings,
        };

    private static string? Credit(MetricSource source) => source switch
    {
        MetricSource.Hours or MetricSource.Days or MetricSource.AirHours => "Open-Meteo",
        MetricSource.Rates => "European Central Bank",
        _ => null,
    };

    internal static string Subject(MetricSubject subject) => subject switch
    {
        MetricSubject.Weather => MetricsStr.Metrics_Subject_Weather.Text(),
        MetricSubject.Air => MetricsStr.Metrics_Subject_Air.Text(),
        MetricSubject.Money => MetricsStr.Metrics_Subject_Money.Text(),
        _ => MetricsStr.Metrics_Subject_System.Text(),
    };

    internal static string Step(MetricStep step) => step switch
    {
        MetricStep.Day => MetricsStr.Metrics_Step_Daily.Text(),
        MetricStep.Hour => MetricsStr.Metrics_Step_Hourly.Text(),
        _ => MetricsStr.Metrics_Step_Live.Text(),
    };

    internal static string Way(MetricWay way) => way == MetricWay.Future ? MetricsStr.Metrics_Way_Future.Text() : MetricsStr.Metrics_Way_Past.Text();

    internal static string Graph(MetricGraphType graph) => graph switch
    {
        MetricGraphType.Line => MetricsStr.Metrics_Graph_Line.Text(),
        MetricGraphType.Bars => MetricsStr.Metrics_Graph_Bars.Text(),
        MetricGraphType.Step => MetricsStr.Metrics_Graph_Step.Text(),
        MetricGraphType.Range => MetricsStr.Metrics_Graph_Range.Text(),
        MetricGraphType.Candles => MetricsStr.Metrics_Graph_Candles.Text(),
        _ => MetricsStr.Metrics_Graph_Area.Text(),
    };

    /// <summary>[S-16302] A span's name: "5 minutes", "24 hours", "7 days", a quote's "3 months".</summary>
    internal static string SpanLabel(MetricSpan span)
    {
        var count = LocaleFormat.Integer(Strings.Current, span.Count);
        return span.Step switch
        {
            MetricStep.Live => MeterTiming.SpanLabel(span.Count),
            MetricStep.Hour => Str.WeatherGraph_WindowHours.Format(("count", count)),
            _ when span.Count >= 365 => Str.WidgetsC_Stocks_Period_Years.Plural(span.Count / 365),
            _ when span.Count >= 28 && span.Count % 30 == 0 => Str.WidgetsC_Stocks_Period_Months.Plural(span.Count / 30),
            _ => Str.WidgetsC_Stocks_Period_Days.Plural(span.Count),
        };
    }
}
