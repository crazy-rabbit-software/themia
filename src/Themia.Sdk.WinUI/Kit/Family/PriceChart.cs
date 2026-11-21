using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Widgets;
using Themia.Core.Links;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Sdk.WinUI.Kit;

/// <summary>
/// [S-6756] [S-11910] [S-11911] The one price-history panel, the stock's and the rate's: the Chart block the card's edge
/// to edge ([S-6754]), time left to right in every language ([S-1113]), its own surface taking the pointer (the rest of
/// the card is still the card's to drag). The sample under the pointer is the nearest of the samples spread evenly across
/// it (<see cref="LinkInstants.IndexAt"/>), its moment published through <see cref="ChartMoments"/>; the hairline (the
/// secondary text role) stands at the moment shown, this chart's own or one pointed at elsewhere where the period holds
/// it, down the plot between what the chart writes at its top and its foot; with a readout (the stock's), the point under
/// the pointer is written inside the plot's text box at its top. Nothing moves on hover ([S-12831]).
/// </summary>
public sealed partial class PriceChart : Grid
{
    private readonly Canvas _overlay = new() { IsHitTestVisible = false };
    private readonly Line _rule = new() { StrokeThickness = FamilyShape.Hairline, Visibility = Visibility.Collapsed };
    private readonly Border _probe = new() { Visibility = Visibility.Collapsed, Padding = new Thickness(FamilySpace.S1, 0, FamilySpace.S1, 0) };
    private readonly TextBlock _probeText = Family.Text(FamilyType.Caption);
    private ChartMoments? _moments;
    private Func<int> _count = () => 0;
    private Func<DateTimeOffset, int?> _index = _ => null;
    private Func<int, string>? _readout;

    public PriceChart()
    {
        _rule.Stroke = Family.Brush(ColourRole.TextSecondary);
        _probe.Child = _probeText;
        _probe.CornerRadius = Family.Radius;
        _probe.Background = Family.Brush(ColourRole.Solid);
        _overlay.Children.Add(_rule);
        _overlay.Children.Add(_probe);
        Children.Add(Series);
        Children.Add(_overlay);

        // A clear fill is what makes the chart's empty area hit-testable.
        FlowDirection = FlowDirection.LeftToRight;
        Background = Family.Brush(ColourRole.Clear);
    }

    /// <summary>The series, drawn by the owner (its model and its role).</summary>
    public Chart Series { get; } = new();

    /// <summary>
    /// Take part in the view's moments on <paramref name="topic"/> (the kind names it, <see cref="LinkTopics.HoveredInstant"/>):
    /// <paramref name="subject"/> what the series is of, <paramref name="count"/> its samples
    /// as drawn now, <paramref name="at"/> a sample's moment, <paramref name="index"/> the sample a moment pointed at stands on
    /// (null outside the period), <paramref name="readout"/> the words written for the sample under the pointer (null: none).
    /// </summary>
    public void Moments(
        WidgetLinks links,
        LinkTopic<InstantLink> topic,
        Func<string> subject,
        Func<int> count,
        Func<int, DateTimeOffset> at,
        Func<DateTimeOffset, int?> index,
        Func<int, string>? readout = null)
    {
        ArgumentNullException.ThrowIfNull(at);
        _count = count ?? throw new ArgumentNullException(nameof(count));
        _index = index ?? throw new ArgumentNullException(nameof(index));
        _readout = readout;
        _moments = new ChartMoments(
            links,
            topic,
            this,
            args => _count() is var n && n >= 2 && LinkInstants.IndexAt(args.GetCurrentPoint(this).Position.X, ActualWidth, n) is { } i ? at(i) : null,
            subject,
            Mark);
    }

    /// <summary>Draw the moment shown again: the series was drawn anew.</summary>
    public void Mark() => Mark(_moments?.Shown);

    /// <summary>Take the hairline and the readout down (the view is off its card).</summary>
    public void Clear()
    {
        _rule.Visibility = Visibility.Collapsed;
        _probe.Visibility = Visibility.Collapsed;
    }

    /// <summary>[S-11911] The hairline at <paramref name="at"/>'s sample, and the readout while the pointer is here.</summary>
    private void Mark(DateTimeOffset? at)
    {
        var n = _count();
        if (at is not { } moment || n < 2 || _index(moment) is not { } i || i < 0 || i >= n)
        {
            Clear();
            return;
        }

        var width = ActualWidth;
        var x = i * width / (n - 1);
        var frame = Series.Frame ?? ChartPlot.Frame(Series.Model, width, ActualHeight);
        _rule.X1 = _rule.X2 = x;
        (_rule.Y1, _rule.Y2) = (frame.RulesTop, frame.RulesBottom);
        _rule.Visibility = Visibility.Visible;

        if (_moments?.Own is null || _readout is null)
        {
            _probe.Visibility = Visibility.Collapsed;
            return;
        }

        // [S-6754] Inside the plot's text box, as everything written on the chart.
        _probeText.Text = _readout(i);
        _probe.Visibility = Visibility.Visible;
        var estimate = (HeroFit.Ems(_probeText.Text) * FamilyType.Caption.FontSize) + (2 * FamilySpace.S1);
        Canvas.SetLeft(_probe, ChartPlot.Across(frame, x, estimate));
        Canvas.SetTop(_probe, frame.Text.Top);
    }
}
