using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell.Config;

/// <summary>The lines the system atoms share on their properties menus ([S-3719]): a history's span, a facts atom's pick.</summary>
public static class AtomMenuLines
{
    /// <summary>[S-1122] [S-11002] A history atom's one setting: how much history its chart spans, on the first level.</summary>
    public static ItemMenuEntry Span(WidgetMenuLines menu, HistoryConfig history)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(history);
        return menu.Choice(Glyphs.Hours, Str.WidgetsB_Settings_Meters_ChartSpan_Label.Text(),
            [.. MeterTiming.SpanChoices.Select(span => (span, MeterTiming.SpanLabel(span)))],
            MeterTiming.NearestSpan(history.GraphSpanSeconds),
            span => history.GraphSpanSeconds = span);
    }

    /// <summary>
    /// [S-11007] A facts atom's pick, as the menu's list ([S-3729]): the facts it shows in their order
    /// (dragged to reorder, each with Remove, the last one kept), then one line per fact it does not
    /// show yet, which adds it at the end. Its card's height follows the count.
    /// </summary>
    public static ItemMenuEntry Facts(WidgetMenuLines menu, FactsConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);

        string Name(string key) => config.Options.FirstOrDefault(o => o.Key == key) is { } option ? option.Label.Text() : key;

        var adds = config.Options
            .Where(o => !config.Facts.Contains(o.Key))
            .Select(o => menu.Command(Glyphs.Add, o.Label.Text(), () => config.Facts.Add(o.Key)))
            .ToList();

        return menu.List(Glyphs.ListLayout, Str.Details_Facts.Text(), config.Facts,
            new MenuList<string>(Name, Str.Details_Facts.Text()) { Min = 1, AddLines = adds });
    }
}
