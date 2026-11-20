using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell.Config;

/// <summary>The line the system history atoms share on their pages ([S-3719]): a history's span.</summary>
public static class AtomMenuLines
{
    /// <summary>[S-1122] [S-11002] A history atom's one setting: how much history its chart spans, on the first level.</summary>
    public static ItemMenuEntry Span(WidgetMenuLines menu, HistoryConfig history)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(history);
        return WidgetMenuLines.Shared(SharedSettings.HistorySpan, menu.Choice(Glyphs.Hours, Str.WidgetsB_Settings_Meters_ChartSpan_Label.Text(),
            [.. MeterTiming.SpanChoices.Select(span => (span, MeterTiming.SpanLabel(span)))],
            MeterTiming.NearestSpan(history.GraphSpanSeconds),
            span => history.GraphSpanSeconds = span));
    }
}
