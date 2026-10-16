using Microsoft.UI.Xaml;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Shell.Config;

/// <summary>The rows every meter shares.</summary>
internal static partial class WidgetSettings
{
    /// <summary>
    /// [S-1122] What every meter's page ends with: whether it draws its history and how much of it.
    /// How often it samples is the kind's own ([S-3793]). A kind's own rows go above these.
    /// </summary>
    private static void MeterRows(List<FrameworkElement> rows, GaugeConfig gauge, Action save)
    {
        rows.Add(FluentLayout.Header(Str.WidgetsB_Settings_Meters_History.Text()));

        // [S-6000] A styled meter's history is its graph style, chosen in the Style row: no switch beside it.
        if (!WidgetStyles.Offers(gauge.Kind))
        {
            AddGraphToggle(rows, gauge, save);
        }

        var spans = MeterTiming.SpanChoices.Select(span => (span, MeterTiming.SpanLabel(span))).ToList();

        rows.Add(FluentLayout.Row(Glyphs.Hours, Str.WidgetsB_Settings_Meters_ChartSpan_Label.Text(),
            Str.WidgetsB_Settings_Meters_ChartSpan_Desc.Text(),
            FluentLayout.Choice(spans, MeterTiming.NearestSpan(gauge.GraphSpanSeconds),
                value => { gauge.GraphSpanSeconds = value; save(); }),
            "graph span", "time span"));
    }

    /// <summary>The history switch.</summary>
    private static void AddGraphToggle(List<FrameworkElement> rows, GaugeConfig gauge, Action save)
        => rows.Add(FluentLayout.Row(Glyphs.Motion, Str.WidgetsB_Settings_Meters_ShowHistory_Label.Text(),
            Str.WidgetsB_Settings_Meters_ShowHistory_Desc.Text(),
            FluentLayout.Toggle(gauge.ShowGraph, value => { gauge.ShowGraph = value; save(); }),
            "graph", "chart", "sparkline"));
}
