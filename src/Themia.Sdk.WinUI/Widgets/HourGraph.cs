using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;
using Windows.Foundation;

namespace Themia.App.Widgets;

/// <summary>
/// [S-12703] [S-6754] The weather graphs' one part, on the Chart block ([S-6717]): the plot is the one <see cref="Chart"/>
/// ([S-6750]) in the config's chart look ([S-6752]), the card's edge to edge (owner, 2026-11-19: "Plot edge to edge"), and
/// what is written stands over it: the top line over its top (the kind's glyph leading the reading now, primary or its
/// band's Caution/Critical, a chosen measure after it, secondary; the stale glyph at the line's end, [S-7042]), the wind's
/// arrow line over its foot, and the chart's own "Now" and the window's end (Simple) or times (Detailed) along the foot.
/// The chart draws the shaded spans (Subtle), the days' borders (Track hairlines), the series as a line, a line with its
/// 16 % wash, or round columns, each piece in its band's role, the second series tertiary, the moment now (a hairline and
/// a dot on the line) and the figures at the marked slots, the line kept clear of the top line and the foot's words.
/// Geometry is <see cref="HourGraphRules"/>'s; every kind draws through this one part so they read as one family. Time
/// runs left to right in every language.
/// <para>
/// [S-12800] The plot owns the card's top edge ([S-5700]); the top line keeps the family's inset over it, the same joined
/// or alone ([S-12483]).
/// </para>
/// </summary>
public sealed partial class HourGraph : Grid
{
    private readonly FontIcon _glyph = Family.Glyph(string.Empty, FamilyShape.Icon, ColourRole.TextSecondary);
    private readonly TextBlock _reading = Family.Text(FamilyType.BodyStrong);
    private readonly TextBlock _label = Family.Text(FamilyType.Body, ColourRole.TextSecondary);
    private readonly FontIcon _problem = Family.Glyph(Shell.Glyphs.Warning, FamilyShape.Icon, ColourRole.Caution);
    private readonly Chart _plot = new();
    private readonly Canvas _arrows = new() { IsHitTestVisible = false };
    private HourGraphModel? _model;

    public HourGraph()
    {
        FlowDirection = FlowDirection.LeftToRight;

        // [S-6754] The plot first, the card's whole box; what is written over it after.
        Children.Add(_plot);

        // [S-12801] The glyph says what the reading is; the measure only where the kind offers a choice of them.
        var top = new Grid
        {
            Margin = new Thickness(FamilySpace.Inset, FamilySpace.Inset, FamilySpace.Inset, 0),
            VerticalAlignment = VerticalAlignment.Top,
            Height = FamilyType.Body.LineHeight,
            ColumnSpacing = FamilySpace.GlyphGap,
        };
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _glyph.VerticalAlignment = VerticalAlignment.Center;
        _label.TextTrimming = TextTrimming.CharacterEllipsis;
        _label.TextWrapping = TextWrapping.NoWrap;
        _problem.VerticalAlignment = VerticalAlignment.Center;
        _problem.Visibility = Visibility.Collapsed;
        SetColumn(_reading, 1);
        SetColumn(_label, 2);
        SetColumn(_problem, 3);
        top.Children.Add(_glyph);
        top.Children.Add(_reading);
        top.Children.Add(_label);
        top.Children.Add(_problem);
        Children.Add(top);

        Children.Add(_arrows);

        // The wind's arrows stand over the plot's foot, over its time line: laid out again with every frame it draws.
        _plot.Drawn += Arrows;
    }

    /// <summary>Draw <paramref name="model"/>; the plot is drawn again whenever its box changes.</summary>
    public void Show(HourGraphModel model)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _glyph.Glyph = model.Glyph;
        _glyph.Visibility = model.Glyph.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        _label.Text = model.Label;
        _label.Visibility = model.Label.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
        _reading.Text = model.Reading;
        _reading.Foreground = Family.Brush(model.ReadingRole);
        _plot.Show(size => HourGraphRules.Chart(model, size.Width, Strings.Current, TimeZoneInfo.Local));
    }

    /// <summary>[S-7042] The stale rule's lines: a Caution glyph after the reading, its tooltip the lines; none hides it.</summary>
    public void Problems(IReadOnlyList<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        _problem.Visibility = lines.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        ToolTipService.SetToolTip(_problem, lines.Count > 0 ? string.Join(Environment.NewLine, lines) : null);
    }

    /// <summary>[S-12703] [S-6754] The wind's arrows: where it blows to, each over its slot inside the text box, on the frame's arrow line.</summary>
    private void Arrows(ChartFrame frame)
    {
        _arrows.Children.Clear();
        if (_model is not { Arrows: true } model)
        {
            return;
        }

        foreach (var (slot, x) in HourGraphRules.Arrows(model.Slots.Count, frame))
        {
            if (model.Slots[slot].Direction is not { } from)
            {
                continue;
            }

            var arrow = Family.Glyph(Shell.Glyphs.MoveUp, FamilyShape.Icon, ColourRole.TextSecondary);
            arrow.RenderTransformOrigin = new Point(0.5, 0.5);
            arrow.RenderTransform = new RotateTransform { Angle = (from + 180) % 360 };
            Canvas.SetLeft(arrow, x - (FamilyShape.Icon / 2));
            Canvas.SetTop(arrow, frame.UnderTop);
            _arrows.Children.Add(arrow);
        }
    }
}
