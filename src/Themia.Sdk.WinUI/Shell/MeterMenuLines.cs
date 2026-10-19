using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell.Config;

/// <summary>The lines every meter's properties menu shares ([S-3719]).</summary>
public static class MeterMenuLines
{
    /// <summary>
    /// [S-1122] What every meter offers of its history: the chart span, under Style where the look
    /// draws a history ([S-6074]: a styled meter's graph style, none in a look without one); a meter
    /// without styles has no Style submenu, so its history switch and span stand on the first level.
    /// How often it samples is the kind's own ([S-3793]). A kind's own lines go on the first level.
    /// </summary>
    public static void Lines(WidgetMenuLines menu, GaugeConfig gauge)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(gauge);

        var styled = WidgetStyles.Offers(gauge.Kind);
        var under = styled ? menu.StyleLines : menu.Lines;

        if (gauge.Offers(MeterSetting.History))
        {
            under.Add(menu.Toggle(styled ? null : Glyphs.Motion, Str.WidgetsB_Settings_Meters_ShowHistory_Label.Text(), gauge.ShowGraph, on => gauge.ShowGraph = on));
        }

        if (gauge.Offers(MeterSetting.Span))
        {
            under.Add(menu.Choice(Glyphs.Hours, Str.WidgetsB_Settings_Meters_ChartSpan_Label.Text(),
                [.. MeterTiming.SpanChoices.Select(span => (span, MeterTiming.SpanLabel(span)))],
                MeterTiming.NearestSpan(gauge.GraphSpanSeconds),
                span => gauge.GraphSpanSeconds = span));
        }
    }
}
