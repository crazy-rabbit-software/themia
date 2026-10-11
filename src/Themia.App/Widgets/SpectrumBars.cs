using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Shell;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-3205] The audio visualizer's bars: up to <see cref="Spectrum.Bands"/> rounded rectangles placed
/// once per layout from a <see cref="VisualizerPlan"/>, each drawn at its full length and scaled from
/// its base. A frame is one transform write per bar and no layout pass, which is what lets it run at
/// the capture rate on the UI thread for nothing.
/// </summary>
internal sealed partial class SpectrumBars : Canvas
{
    private readonly Rectangle[] _bars = new Rectangle[Spectrum.Bands];
    private readonly ScaleTransform[] _scales = new ScaleTransform[Spectrum.Bands];
    private readonly float[] _grouped = new float[Spectrum.Bands];
    private VisualizerPlan _plan = VisualizerLayout.Plan(default);

    public SpectrumBars()
    {
        var fill = FluentLayout.Themed("AccentFillColorDefaultBrush", Colors.DodgerBlue);

        for (var i = 0; i < _bars.Length; i++)
        {
            var scale = new ScaleTransform();
            _scales[i] = scale;

            var bar = new Rectangle
            {
                Fill = fill,
                RenderTransform = scale,
                Visibility = Visibility.Collapsed,
            };

            _bars[i] = bar;
            Children.Add(bar);
        }

        Clear();
    }

    /// <summary>Place the bars for <paramref name="plan"/>; the bars keep their levels.</summary>
    public void Arrange(VisualizerPlan plan)
    {
        _plan = plan;
        var radius = Math.Min(plan.Thickness / 2, Fluent.ControlRadius / 2);

        for (var i = 0; i < _bars.Length; i++)
        {
            var bar = _bars[i];

            if (i >= plan.Bars)
            {
                bar.Visibility = Visibility.Collapsed;
                continue;
            }

            var step = i * (plan.Thickness + plan.Gap);
            bar.RadiusX = radius;
            bar.RadiusY = radius;

            if (plan.Horizontal)
            {
                // A column: the bars lie across it, growing from the start edge, the bass at the bottom.
                bar.Width = plan.Length;
                bar.Height = plan.Thickness;
                bar.RenderTransformOrigin = new Windows.Foundation.Point(0, 0.5);
                SetLeft(bar, plan.Inset);
                SetTop(bar, plan.Inset + ((plan.Bars - 1) * (plan.Thickness + plan.Gap)) - step);
                _scales[i].ScaleY = 1;
            }
            else
            {
                bar.Width = plan.Thickness;
                bar.Height = plan.Length;
                bar.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 1);
                SetLeft(bar, plan.Inset + step);
                SetTop(bar, plan.Inset);
                _scales[i].ScaleX = 1;
            }

            bar.Visibility = Visibility.Visible;
        }

        Draw();
    }

    /// <summary>Draw one frame of the analyser's bands; missing levels are silence.</summary>
    public void SetLevels(ReadOnlySpan<float> bands)
    {
        VisualizerLayout.Group(bands, _grouped.AsSpan(0, _plan.Bars));
        Draw();
    }

    /// <summary>[S-3206] Every bar down to the baseline: the idle state.</summary>
    public void Clear()
    {
        Array.Clear(_grouped);
        Draw();
    }

    private void Draw()
    {
        for (var i = 0; i < _plan.Bars && i < _scales.Length; i++)
        {
            var level = Math.Max(_plan.Floor, _grouped[i]);

            if (_plan.Horizontal)
            {
                _scales[i].ScaleX = level;
            }
            else
            {
                _scales[i].ScaleY = level;
            }
        }
    }
}
