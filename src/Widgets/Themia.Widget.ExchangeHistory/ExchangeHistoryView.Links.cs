using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Widgets;
using Themia.Core.Links;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;

namespace Themia.Widget.ExchangeHistory;

/// <summary>
/// [S-11910] [S-11911] The rate's history on its view's links: the day under the pointer is published as a
/// moment of this pair (<c>rate/chf/eur</c>, the day's noon), and a moment pointed at on any history chart
/// of the view draws the hairline at its day where the history holds it.
/// </summary>
internal sealed partial class ExchangeHistoryView
{
    private readonly WidgetLinks _links = new();
    private readonly Rectangle _marker = new()
    {
        Width = FamilyShape.Hairline,
        HorizontalAlignment = HorizontalAlignment.Left,
        Visibility = Visibility.Collapsed,
        IsHitTestVisible = false,
    };

    private PairRate? _shownRate;
    private InstantLink? _marked;
    private DateTimeOffset? _own;

    private string Subject => LinkInstants.Subject("rate", $"{_config.EffectiveBase}/{_config.EffectiveQuote}");

    private void JoinInstants()
    {
        _marker.Fill = Family.Brush(ColourRole.TextSecondary);
        Children.Add(_marker);
        _links.Follow(LinkTopics.HoveredInstant, moment =>
        {
            _marked = moment;
            MarkInstant();
        });

        // The chart's own surface takes the pointer; a clear fill makes its empty part hit-testable.
        _chart.Background = Family.Brush(ColourRole.Clear);
        _chart.PointerMoved += OnChartPointer;
        _chart.PointerExited += OnChartLeft;
        _chart.PointerCanceled += OnChartLeft;
        _chart.PointerCaptureLost += OnChartLeft;
    }

    private void OnChartPointer(object sender, PointerRoutedEventArgs args)
    {
        if (_shownRate is not { Days: { Count: > 1 } days } || _chart.ActualWidth <= 0)
        {
            return;
        }

        var x = args.GetCurrentPoint(_chart).Position.X;
        var index = (int)Math.Round(Math.Clamp(x / _chart.ActualWidth, 0, 1) * (days.Count - 1));
        var at = PairRate.Noon(days[index], DateTimeOffset.Now.Offset);
        _own = at;
        _links.Set(LinkTopics.HoveredInstant, new InstantLink(at, Subject));
        MarkInstant();
    }

    private void OnChartLeft(object sender, PointerRoutedEventArgs args)
    {
        _own = null;
        _links.Set(LinkTopics.HoveredInstant, null);
        MarkInstant();
    }

    /// <summary>[S-11911] The hairline at the day of the moment pointed at (here or elsewhere), or none.</summary>
    private void MarkInstant()
    {
        var at = _own ?? _marked?.At;
        var day = at is { } moment ? _shownRate?.At(moment)?.Day : null;
        if (day is not { } d || _shownRate?.Days is not { Count: > 1 } days || _chart.Visibility != Visibility.Visible)
        {
            _marker.Visibility = Visibility.Collapsed;
            return;
        }

        var index = days.ToList().IndexOf(d);
        _marker.Margin = new Thickness(index * _chart.ActualWidth / (days.Count - 1), 0, 0, 0);
        _marker.Visibility = Visibility.Visible;
    }
}
