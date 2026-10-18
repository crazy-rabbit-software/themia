using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Typography = Microsoft.UI.Xaml.Documents.Typography;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Shell;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Windows.Foundation;

namespace Themia.App.Widgets;

/// <summary>[S-6300] The theme brushes the meter parts paint with, looked up once.</summary>
public static class MeterBrushes
{
    private static readonly Brush Accent = ThemeBrush.Of("AccentFillColorDefaultBrush", Colors.DodgerBlue);
    private static readonly Brush Caution = ThemeBrush.Of("SystemFillColorCautionBrush", Colors.Orange);
    private static readonly Brush Critical = ThemeBrush.Of("SystemFillColorCriticalBrush", Colors.Red);
    private static readonly Brush Success = ThemeBrush.Of("SystemFillColorSuccessBrush", Colors.Green);

    // [S-837] The empty part of a gauge or bar (the free share, the idle time) is a progress bar's
    // track, never the disabled fill; an item with no reading is the disabled track. Both are the
    // item layer's track ([S-6513]), painted per element through ItemFill, so a widget's own item
    // layer reaches them.

    /// <summary>[S-1123] The pressure colours every meter shares.</summary>
    public static Brush Pressure(MeterPressure pressure) => pressure switch
    {
        MeterPressure.Critical => Critical,
        MeterPressure.Elevated => Caution,
        MeterPressure.Healthy => Success,
        _ => Accent,
    };
}
