using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Shell;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.Music;

namespace Themia.App.Widgets;

/// <summary>
/// [S-1475] Now playing's parts placed by <see cref="MusicFace"/>: the lines at the content's
/// top, the progress row 8 over the transport, the transport (the Controls row) at the bottom inset,
/// the art beside the lines, over them, at the left of them all ([S-2155]) or, in the Cover style, as a
/// band over everything else. One
/// pass from the plan; nothing is positioned by a computed offset (rows of a grid and the scale's
/// margins only).
/// </summary>
internal sealed partial class MusicView
{
    private WidgetArea _area;

    private void Lay()
    {
        if (!_now.HasTrack)
        {
            return;
        }

        var area = _area.IsMeasured ? _area : new WidgetArea(ActualWidth, ActualHeight);
        if (!area.IsMeasured)
        {
            return;
        }

        var width = Math.Max(0, area.Width - (2 * FamilySpace.Inset));
        var height = Math.Max(0, area.Height - FamilySpace.Inset);
        var plan = MusicFace.Plan(area, width, height, _config.Style, _config.ShowTransport, _config.ShowProgress, _now.Subtitle.Length > 0);

        Family.Step(_track, plan.Title);
        _subtitle.Visibility = Visible(plan.Subtitle);
        _times.Visibility = Visible(plan.Progress);
        _controls.Visibility = Visible(plan.Controls);

        // The progress row stands 8 over the transport, or on the bottom inset without it.
        _times.Margin = new Thickness(0, 0, 0, plan.Controls ? FamilySpace.S2 : 0);

        _coverArt.Visibility = Visible(plan.Art == MusicArt.Cover);
        _overArt.Visibility = Visible(plan.Art == MusicArt.Over);
        _leadArt.Visibility = Visible(plan.Art == MusicArt.Lead);
        _sideArt.Visibility = Visible(plan.Art == MusicArt.Side);
        _content.Margin = new Thickness(FamilySpace.Inset, plan.Art == MusicArt.Cover ? FamilySpace.S2 : 0, FamilySpace.Inset, FamilySpace.Inset);

        switch (plan.Art)
        {
            case MusicArt.Cover:
                _coverArt.Height = plan.ArtSize;
                break;
            case MusicArt.Over:
                _overArt.Width = plan.ArtSize;
                _overArt.Height = plan.ArtSize;
                break;
            case MusicArt.Side:
                _sideArt.Width = plan.ArtSize;
                _sideArt.Height = plan.ArtSize;
                break;
        }

        // [S-6407] The Cover style puts the lines with the progress and the transport, under the band;
        // the other styles keep them at the content's top.
        Grid.SetRow(_top, plan.Art == MusicArt.Cover ? 1 : 0);
        _top.VerticalAlignment = plan.Art == MusicArt.Cover ? VerticalAlignment.Bottom : VerticalAlignment.Top;
        _top.Margin = new Thickness(0, 0, 0, plan.Art == MusicArt.Cover && (plan.Progress || plan.Controls) ? FamilySpace.S2 : 0);

        var time = MusicFace.TimeWidth(_total.Text);
        _times.ColumnDefinitions[0].Width = new GridLength(time);
        _times.ColumnDefinitions[2].Width = new GridLength(time);
    }

    private static Visibility Visible(bool shown) => shown ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>[S-1475] The art's frame: the one radius (none for the cover band, edge to edge), its picture cropped to fill, the music glyph on the control fill without one.</summary>
    private static (Border Frame, Image Picture, FontIcon Placeholder) ArtFrame(string glyph, double glyphSize, bool square)
    {
        var picture = new Image { Stretch = Microsoft.UI.Xaml.Media.Stretch.UniformToFill, Visibility = Visibility.Collapsed };
        var placeholder = Family.Glyph(glyph, glyphSize, ColourRole.TextTertiary);
        placeholder.HorizontalAlignment = HorizontalAlignment.Center;
        var frame = new Border
        {
            Child = new Grid { Children = { placeholder, picture } },
            Background = Family.Brush(ColourRole.Control),
            CornerRadius = square ? Family.Radius : new CornerRadius(0),
            HorizontalAlignment = square ? HorizontalAlignment.Left : HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            Visibility = Visibility.Collapsed,
        };

        return (frame, picture, placeholder);
    }
}
