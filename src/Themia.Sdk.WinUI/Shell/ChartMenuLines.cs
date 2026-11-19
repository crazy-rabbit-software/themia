using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk.Kit;

namespace Themia.Sdk.WinUI;

/// <summary>
/// [S-6752] The two lines every graph widget's page has under its Style picker, the same on every graph kind: Chart
/// type (the types the kind offers, where it offers a choice) and Detail (Simple or Detailed, where the kind offers
/// it). Both are shared settings ([S-11750]): several graphs selected set them once.
/// </summary>
public static class ChartMenuLines
{
    /// <summary>Add the chart look's lines to the page's style lines.</summary>
    public static void Add(WidgetMenuLines menu, IChartLooks looks)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(looks);
        if (looks.ChartTypes.Count > 1)
        {
            menu.StyleLines.Add(WidgetMenuLines.Shared(SharedSettings.ChartType, menu.Choice(Glyphs.ScreenTimeChart, Str.Chart_Type.Text(),
                [.. looks.ChartTypes.Select(type => (type, Label(type)))],
                ChartLooks.Effective(looks),
                type => looks.Chart = type)));
        }

        if (looks.OffersDetail)
        {
            menu.StyleLines.Add(WidgetMenuLines.Shared(SharedSettings.ChartDetail, menu.Choice(Glyphs.Grid, Str.Chart_Detail.Text(),
                [(ChartDetail.Simple, Str.Chart_Detail_Simple.Text()), (ChartDetail.Detailed, Str.Chart_Detail_Detailed.Text())],
                ChartLooks.EffectiveDetail(looks),
                detail => looks.Detail = detail)));
        }
    }

    /// <summary>A type's name on the page.</summary>
    public static string Label(ChartType type) => type switch
    {
        ChartType.Line => Str.Chart_Type_Line.Text(),
        ChartType.Bars => Str.Chart_Type_Bars.Text(),
        ChartType.Candles => Str.Chart_Type_Candles.Text(),
        _ => Str.Chart_Type_Area.Text(),
    };
}
