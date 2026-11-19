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
/// [S-12703] The weather graphs' one part, on the Chart block ([S-6717]): the top line (the kind's glyph leading the
/// reading now, primary or its band's Caution/Critical, a chosen measure after it, secondary; the stale glyph at the
/// line's end, [S-7042]), the plot, the wind's arrow line and the time line ("Now" and the window's end, Caption tertiary).
/// The plot is the one <see cref="Chart"/> ([S-6750]) in the config's chart look ([S-6752]): it draws the shaded spans (Subtle), the days' borders (Track hairlines), the series as a line, a line
/// with its 16 % wash, or round columns, each piece in its band's role, the second series tertiary, the moment
/// now (a hairline and a dot on the line) and the figures at the marked slots. Geometry is <see cref="HourGraphRules"/>'s;
/// every kind draws through this one part so they read as one family. Time runs left to right in every language.
/// <para>
/// [S-12800] It keeps the family's inset at the sides and the foot only: the card gives its top ([S-5700]), the same
/// joined or alone ([S-12483]).
/// </para>
/// </summary>
public sealed partial class HourGraph : Grid
{
    private readonly FontIcon _glyph = Family.Glyph(string.Empty, FamilyShape.Icon, ColourRole.TextSecondary);
    private readonly TextBlock _reading = Family.Text(FamilyType.BodyStrong);
    private readonly TextBlock _label = Family.Text(FamilyType.Body, ColourRole.TextSecondary);
    private readonly FontIcon _problem = Family.Glyph(Shell.Glyphs.Warning, FamilyShape.Icon, ColourRole.Caution);
    private readonly Chart _plot = new();
    private readonly Grid _times = new() { Margin = new Thickness(0, FamilySpace.S1, 0, 0) };
    private readonly Canvas _arrows = new() { IsHitTestVisible = false, Height = FamilyShape.Icon };
    private readonly TextBlock _start = Family.Text(FamilyType.Caption, ColourRole.TextTertiary);
    private readonly TextBlock _end = Family.Text(FamilyType.Caption, ColourRole.TextTertiary);
    private HourGraphModel? _model;

    public HourGraph()
    {
        FlowDirection = FlowDirection.LeftToRight;
        Margin = Family.SideInset with { Bottom = FamilySpace.Inset };
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // [S-12801] The glyph says what the reading is; the measure only where the kind offers a choice of them.
        var top = new Grid { Margin = new Thickness(0, 0, 0, FamilySpace.S2), ColumnSpacing = FamilySpace.GlyphGap };
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

        SetRow(_plot, 1);
        Children.Add(_plot);

        _arrows.Margin = new Thickness(0, FamilySpace.S1, 0, 0);
        _arrows.Visibility = Visibility.Collapsed;
        SetRow(_arrows, 2);
        Children.Add(_arrows);

        _end.HorizontalAlignment = HorizontalAlignment.Right;
        _times.Children.Add(_start);
        _times.Children.Add(_end);
        SetRow(_times, 3);
        Children.Add(_times);

        // The wind's arrows stand under the slots: laid out again with the plot's width.
        _plot.SizeChanged += (_, _) =>
        {
            _arrows.Children.Clear();
            if (_model is { Slots.Count: > 0 } model)
            {
                Arrows(model.Slots, _plot.ActualWidth);
            }
        };
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
        _start.Text = model.StartLabel;
        _end.Text = model.EndLabel;
        _arrows.Visibility = model.Arrows ? Visibility.Visible : Visibility.Collapsed;
        Draw();
    }

    /// <summary>[S-7042] The stale rule's lines: a Caution glyph after the reading, its tooltip the lines; none hides it.</summary>
    public void Problems(IReadOnlyList<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        _problem.Visibility = lines.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        ToolTipService.SetToolTip(_problem, lines.Count > 0 ? string.Join(Environment.NewLine, lines) : null);
    }

    /// <summary>[S-6750] The plot is the one chart: the slots on their cells' middles, the shares on the frame's scale.</summary>
    private void Draw()
    {
        if (_model is not { } model)
        {
            return;
        }

        _times.Visibility = model.Detail == ChartDetail.Detailed ? Visibility.Collapsed : Visibility.Visible;
        _plot.Show(size => Layout(model, size.Width));
        _arrows.Children.Clear();
        if (model.Slots.Count > 0 && _plot.ActualWidth > 0)
        {
            Arrows(model.Slots, _plot.ActualWidth);
        }
    }

    /// <summary>[S-12703] [S-6751] What the chart draws for <paramref name="model"/> across a plot <paramref name="w"/> wide.</summary>
    private static ChartModel Layout(HourGraphModel model, double w)
    {
        var slots = model.Slots;
        var n = slots.Count;
        var (low, high) = model.Range ?? (0, 1);
        double Value(double? share) => share is { } v ? low + (v * (high - low)) : double.NaN;
        var chart = new ChartModel
        {
            Type = model.Look switch { HourGraphLook.Line => ChartType.Line, HourGraphLook.Bars => ChartType.Bars, _ => ChartType.Area },
            Detail = model.Detail,
            Low = low,
            High = high,
            Baseline = low,
            PadTop = FamilyType.CaptionLine,
            PadBottom = FamilyType.CaptionLine,
            WashToFloor = true,
            RoundCaps = true,
            LoneDots = true,
            Format = model.Range is null ? null : model.Format,
            Caption = model.Caption,
        };

        if (n == 0)
        {
            return chart;
        }

        var first = slots[0].At;
        var locale = Strings.Current;
        var times = new List<ChartTick>();
        if (model.Detail == ChartDetail.Detailed)
        {
            var (at, step) = ChartPlot.Times(first, slots[^1].At, ChartPlot.TimeRoom(w), TimeZoneInfo.Local);
            foreach (var moment in at)
            {
                var local = TimeZoneInfo.ConvertTime(moment, TimeZoneInfo.Local);
                var text = step >= TimeSpan.FromDays(1)
                    ? LocaleFormat.WeekdayName(locale, local.DayOfWeek, NameWidth.Short)
                    : LocaleFormat.Time(locale, TimeOnly.FromDateTime(local.DateTime));
                times.Add(new ChartTick(HourGraphRules.X(moment, first, model.Step, n, w), text));
            }
        }

        return chart with
        {
            Series = [.. slots.Select((s, i) => new ChartSample(HourGraphRules.X(i, n, w), Value(s.Value), s.Role))],
            Second = [.. slots.Select((s, i) => new ChartSample(HourGraphRules.X(i, n, w), Value(s.Second)))],
            Shades = [.. model.Shade.Select(span => new ChartShade(HourGraphRules.X(span.Rise, first, model.Step, n, w), HourGraphRules.X(span.Set, first, model.Step, n, w)))],
            Rules = [.. HourGraphRules.Midnights(first, slots[^1].At, TimeZoneInfo.Local).Select(m => HourGraphRules.X(m, first, model.Step, n, w))],
            Now = HourGraphRules.X(model.Now, first, model.Step, n, w),
            Marks = [.. model.Marks.Where(m => m.Slot >= 0 && m.Slot < n).Select(m => new ChartMark(HourGraphRules.X(m.Slot, n, w), Value(slots[m.Slot].Value), m.Text, m.Under, m.Under ? ColourRole.TextSecondary : ColourRole.TextPrimary))],
            Times = times,
        };
    }

    /// <summary>[S-12703] The wind's arrows: where it blows to, as many as stand a glyph and a gap apart, the first slot's first.</summary>
    private void Arrows(IReadOnlyList<HourSlot> slots, double w)
    {
        if (_model is not { Arrows: true })
        {
            return;
        }

        var cell = w / slots.Count;
        var every = Math.Max(1, (int)Math.Ceiling((FamilyShape.Icon + FamilySpace.S1) / cell));
        for (var i = 0; i < slots.Count; i += every)
        {
            if (slots[i].Direction is not { } from)
            {
                continue;
            }

            var arrow = Family.Glyph(Shell.Glyphs.MoveUp, FamilyShape.Icon, ColourRole.TextSecondary);
            arrow.RenderTransformOrigin = new Point(0.5, 0.5);
            arrow.RenderTransform = new RotateTransform { Angle = (from + 180) % 360 };
            Canvas.SetLeft(arrow, HourGraphRules.X(i, slots.Count, w) - (FamilyShape.Icon / 2));
            _arrows.Children.Add(arrow);
        }
    }
}
