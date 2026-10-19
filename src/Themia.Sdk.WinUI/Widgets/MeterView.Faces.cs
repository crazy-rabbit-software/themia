using Microsoft.UI.Xaml;
using Themia.App.Widgets.Meters;

namespace Themia.App.Widgets;

/// <summary>
/// [S-6261]..[S-6266] The looks that draw the reading themselves (<see cref="MeterStyleFace"/>: the
/// network's numbers, the battery's shape and ring, the ping's signal bars). Built and dropped with
/// the look like every other look's part (<c>UseLook</c>); the readout and the history stand aside.
/// </summary>
public sealed partial class MeterView
{
    // The look's own face; null for every look that keeps the readout.
    private MeterStyleFace? _face;

    /// <summary>The face over the whole card, the readout hidden: the face is the reading.</summary>
    private void LayFace()
    {
        _reading.Visibility = Visibility.Collapsed;

        if (_face is null)
        {
            return;
        }

        ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
        ColumnDefinitions[1].Width = new GridLength(0);
        SetRow(_face, 0);
        SetRowSpan(_face, 2);
        SetColumn(_face, 0);
        SetColumnSpan(_face, 2);
        _face.Visibility = Visibility.Visible;

        // A face chosen between two samples draws the last one now, not at the next read.
        if (!_face.HasReading)
        {
            var reading = _watch?.Latest ?? _part.Waiting;
            _face.Present(reading, LevelBrush(reading), settle: true);
        }

        if (_area.IsMeasured)
        {
            _face.Lay(_area);
        }
    }
}
