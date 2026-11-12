using Microsoft.UI.Xaml.Input;
using Themia.App.Widgets.Meters;
using Themia.Core.Links;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;

namespace Themia.App.Widgets;

/// <summary>
/// [S-11910]..[S-11914] A meter on its view's links: a history chart (the history atom, the cores' graphs)
/// publishes the moment under the pointer and draws the hairline at a moment pointed at on any chart of
/// the view; a reading of the same subject (the same series: the same adapter, drive, host) shows its
/// value then, with the moment as its caption, for as long as the hover lasts, and is live again after.
/// A meter whose part names no instant topic takes no part.
/// </summary>
public sealed partial class MeterView
{
    private readonly WidgetLinks _links = new();
    private LinkTopic<InstantLink>? _instantTopic;

    /// <summary>[S-11912] The moment the reading stands at now, or null while it is live.</summary>
    private InstantLink? _standsAt;

    private TimeSpan Period => TimeSpan.FromSeconds(Math.Max(1, (_series ?? _part.Series(_config)).Key.PeriodSeconds));

    private string Subject => LinkInstants.Subject(_series ?? _part.Series(_config));

    /// <summary>Whether the face draws a history the pointer can stand on.</summary>
    private bool Charts => _graph || _look == MeterLook.Graphs;

    /// <summary>Take part when the kind names the topic (from the constructor).</summary>
    private void JoinInstants()
    {
        if (_part.Instant is not { } topic)
        {
            return;
        }

        _instantTopic = topic;
        _links.Follow(topic, OnInstant);
        PointerMoved += OnChartPointer;
        PointerExited += OnChartLeft;
        PointerCanceled += OnChartLeft;
        PointerCaptureLost += OnChartLeft;
    }

    /// <summary>[S-11911] The pointer over a history: the moment under it, published and marked here.</summary>
    private void OnChartPointer(object sender, PointerRoutedEventArgs args)
    {
        if (_detached || _instantTopic is not { } topic || !Charts)
        {
            return;
        }

        var band = _graph ? _band : _cells.Select(cell => cell.Band).FirstOrDefault(b => Inside(b, args)) ?? (_cells.Count > 0 ? _cells[0].Band : _band);
        var at = band.InstantAt(args.GetCurrentPoint(band).Position.X, Period, DateTimeOffset.Now);
        var moment = new InstantLink(at, Subject);
        _links.Set(topic, moment);
        Mark(moment);
    }

    private static bool Inside(SparkBand band, PointerRoutedEventArgs args)
    {
        var at = args.GetCurrentPoint(band).Position;
        return at.X >= 0 && at.Y >= 0 && at.X <= band.ActualWidth && at.Y <= band.ActualHeight;
    }

    private void OnChartLeft(object sender, PointerRoutedEventArgs args)
    {
        if (_instantTopic is { } topic && Charts)
        {
            _links.Set(topic, null);
            Mark(null);
        }
    }

    /// <summary>Another widget of the view points at a moment, or let go of it.</summary>
    private void OnInstant(InstantLink? moment)
    {
        if (_detached)
        {
            return;
        }

        Mark(moment);
        if (_part.Atom != MeterAtom.Reading)
        {
            return;
        }

        if (moment is not null && moment.Subject == Subject && ValueAt(moment.At) is { } value)
        {
            _standsAt = moment;
            ShowReading(_reading, value, true, null, ColourRole.TextPrimary, LinkInstants.Caption(moment.At, DateTimeOffset.Now));
        }
        else if (_standsAt is not null)
        {
            _standsAt = null;
            Present(_last ?? _part.Waiting);
        }
    }

    /// <summary>[S-11911] The hairline on every history this face draws.</summary>
    private void Mark(InstantLink? moment)
    {
        _band.Mark(moment?.At, Period);
        foreach (var cell in _cells)
        {
            cell.Band.Mark(moment?.At, Period);
        }
    }

    /// <summary>[S-11913] The reading's own series at <paramref name="at"/>, as the reading writes it; null outside its history.</summary>
    private string? ValueAt(DateTimeOffset at)
    {
        if (_watch is not { } watch)
        {
            return null;
        }

        var points = watch.History(MeterTiming.MaxSpanSeconds);
        return LinkInstants.Index(points.Length, Period, DateTimeOffset.Now, at) is { } index
            ? _part.ValueAt(points[index])
            : null;
    }
}
