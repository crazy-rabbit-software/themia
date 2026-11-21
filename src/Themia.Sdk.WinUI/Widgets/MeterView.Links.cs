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
/// <para>
/// [S-11953] A meter of one drive (its part names <see cref="MeterPart.Drive"/>) is that drive as a whole card
/// (<see cref="LinkCard{T}"/>): the pointer on it publishes the drive, besides the moment a history publishes, and
/// the card takes the item layer's hover fill while the drive is pointed at elsewhere; every drive together is none.
/// </para>
/// </summary>
public sealed partial class MeterView
{
    private readonly WidgetLinks _links = new();

    /// <summary>[S-11912] The moment the reading stands at now, or null while it is live.</summary>
    private InstantLink? _standsAt;

    private TimeSpan Period => TimeSpan.FromSeconds(Math.Max(1, (_series ?? _part.Series(_config)).Key.PeriodSeconds));

    private string Subject => LinkInstants.Subject(_series ?? _part.Series(_config));

    /// <summary>Whether the face draws a history the pointer can stand on.</summary>
    private bool Charts => _shown is Graphs;

    /// <summary>[S-11953] The card as its drive: what it publishes under the pointer and lights for; null for a kind about no drive.</summary>
    private LinkCard<DriveLink>? _driveCard;

    /// <summary>[S-11953] Take part as a drive when the kind names the topic (from the constructor).</summary>
    private void JoinDrives()
    {
        if (_part.Drive is { } topic)
        {
            _driveCard = new LinkCard<DriveLink>(_links, topic, this);
        }
    }

    /// <summary>[S-11952] The drive the config is about now (every drive together: none).</summary>
    private void ShowDrive()
    {
        if (_driveCard is { } card)
        {
            card.Key = _part.DriveOf(_config);
        }
    }

    /// <summary>
    /// [S-6756] Take part when the kind names the topic (from the constructor), on the Chart block's one path: it lives as
    /// long as the view, whose pointer and links hold it.
    /// </summary>
    private void JoinInstants()
    {
        if (_part.Instant is { } topic)
        {
            _ = new ChartMoments(_links, topic, this, InstantUnder, () => Subject, Mark, OnInstant, () => !_detached && Charts);
        }
    }

    /// <summary>[S-11911] The pointer over a history: the moment under it, on the band it is over (the first one's otherwise).</summary>
    private DateTimeOffset? InstantUnder(PointerRoutedEventArgs args)
    {
        var cells = (_shown as Graphs)?.Cells ?? [];
        var band = cells.Select(cell => cell.Band).FirstOrDefault(b => Inside(b, args)) ?? (cells.Count > 0 ? cells[0].Band : _band);
        return band.InstantAt(args.GetCurrentPoint(band).Position.X, Period, DateTimeOffset.Now);
    }

    private static bool Inside(SparkBand band, PointerRoutedEventArgs args)
    {
        var at = args.GetCurrentPoint(band).Position;
        return at.X >= 0 && at.Y >= 0 && at.X <= band.ActualWidth && at.Y <= band.ActualHeight;
    }

    /// <summary>Another widget of the view points at a moment, or let go of it.</summary>
    private void OnInstant(InstantLink? moment)
    {
        if (_detached || _part.Atom != MeterAtom.Reading)
        {
            return;
        }

        if (moment is not null && moment.Subject == Subject && ValueAt(moment.At) is { } value)
        {
            // [S-12814] WR-12: the moment's value and time are drawn at the reading's scale, never a scale of their own.
            if (_shown.Fit is { } fit)
            {
                fit.Hold = true;
            }

            _standsAt = moment;
            ShowReading(_reading, value, true, null, ColourRole.TextPrimary, LinkInstants.Caption(moment.At, DateTimeOffset.Now));
        }
        else if (_standsAt is not null)
        {
            _standsAt = null;
            Present(_last ?? _part.Waiting);
            if (_shown.Fit is { } fit)
            {
                fit.Hold = false;
            }
        }
    }

    /// <summary>[S-11911] The hairline on every history this face draws.</summary>
    private void Mark(DateTimeOffset? at)
    {
        if (_detached)
        {
            return;
        }

        _band.Mark(at, Period);
        foreach (var cell in (_shown as Graphs)?.Cells ?? [])
        {
            cell.Band.Mark(at, Period);
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
