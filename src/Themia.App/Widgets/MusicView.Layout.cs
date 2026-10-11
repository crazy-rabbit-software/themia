using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-1466]-style ladder for now playing ([S-1475]): which parts a tile this size shows,
/// and where. The parts sit on a canvas and are placed from one pass, so each class gets its own
/// arrangement rather than one layout scaled.
/// <list type="bullet">
/// <item>XS: the title (and the artist when it fits) over a play/pause button.</item>
/// <item>S, Wide (short strips): lines with the transport beside or under them, art at the left of a
/// wide strip, the seek bar along the bottom (with the times beside it on a wide one).</item>
/// <item>M: art, lines, transport, the seek bar with the times under it.</item>
/// <item>Tall: stacked — art, lines, seek bar, times, transport — like the Windows media flyout.</item>
/// <item>L, XL: large art beside a column of title, artist, album and a larger transport; the seek
/// bar and its times along the bottom. No "Playing · app" line (concise widgets).</item>
/// </list>
/// </summary>
internal sealed partial class MusicView
{
    /// <summary>The art's smallest useful size.</summary>
    private const double ArtMinimum = 40;

    /// <summary>Art beside lines on a row tile never grows past this: past it, it is the widget.</summary>
    private const double RowArtMaximum = 160;

    /// <summary>On the large tiles the art is the picture, up to this.</summary>
    private const double HeroArtMaximum = 480;

    /// <summary>Narrower than this, the title cannot be read beside the transport.</summary>
    private const double LinesMinimumWidth = 120;

    /// <summary>"1:02:30": the widest time label, so the bar beside it does not shift as the time changes.</summary>
    private const int TimeCharacters = 7;

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

        foreach (UIElement part in _canvas.Children)
        {
            part.Visibility = Visibility.Collapsed;
        }

        // The combined "artist · album" line everywhere but the large tiles, which split it.
        _subtitle.Text = _now.Subtitle;

        var shortTile = area.HeightClass == SizeClass.Compact;
        var padY = shortTile ? Fluent.Space2 : Fluent.Space4;
        var box = area.Deflate(Fluent.Space4, padY);
        var side = area.Least >= SizeClass.Expanded ? Fluent.RowHeight : Fluent.RowHeightCompact;

        SizeTransport(side);

        if (area.WidthClass == SizeClass.Compact)
        {
            LayStack(area, box, padY, side, centred: false, playOnly: true);
        }
        else if (area.AtLeast(SizeClass.Expanded, SizeClass.Expanded))
        {
            LayHero(area, box, padY, side);
        }
        else if (area.HeightClass >= SizeClass.Expanded)
        {
            LayStack(area, box, padY, side, centred: true, playOnly: false);
        }
        else
        {
            LayRow(area, box, padY, side);
        }
    }

    /// <summary>A vertical stack: XS (left-aligned, play only) and Tall (centred, the media-flyout shape).</summary>
    private void LayStack(WidgetArea area, WidgetArea box, double padY, double side, bool centred, bool playOnly)
    {
        var gap = Fluent.Space2;
        var room = new Disclosure(box, gap);
        var transport = _config.ShowTransport;
        var transportWidth = TransportWidth(side, playOnly);

        room.Reserve(FluentType.BodyStrong.LineHeight);
        if (transport)
        {
            room.Reserve(side);
        }

        var subtitleStep = playOnly ? FluentType.Caption : FluentType.Body;
        var subtitle = _now.Subtitle.Length > 0 && room.Take(subtitleStep.LineHeight);
        var bar = _config.ShowProgress && (!playOnly || area.HeightClass >= SizeClass.Regular) && room.Take(MusicSeekBar.BarHeight);
        var times = bar && area.HeightClass >= SizeClass.Regular && room.Take(FluentType.Caption.LineHeight);

        var art = 0.0;
        if (_config.ShowAlbumArt && area.HeightClass >= SizeClass.Regular)
        {
            var wanted = Math.Min(box.Width, Math.Min(HeroArtMaximum, (room.Remaining - gap) * 0.75));
            if (wanted >= ArtMinimum && room.Take(wanted))
            {
                art = wanted;
            }
        }

        var title = FluentType.Strong(FluentType.Fit(box.Width, FluentType.BodyStrong.LineHeight + room.Remaining, 16, FluentType.Body, FluentType.Subtitle));
        _track.Apply(title);
        _subtitle.Apply(subtitleStep);

        var height = title.LineHeight
            + (art > 0 ? art + gap : 0)
            + (subtitle ? subtitleStep.LineHeight + Fluent.Space1 : 0)
            + (bar ? MusicSeekBar.BarHeight + gap : 0)
            + (times ? FluentType.Caption.LineHeight : 0)
            + (transport ? side + gap : 0);

        var x = Fluent.Space4;
        var y = padY + Math.Max(0, (box.Height - height) / 2);
        var alignment = centred ? TextAlignment.Center : TextAlignment.Left;

        if (art > 0)
        {
            Place(_artHost, x + ((box.Width - art) / 2), y, art, art);
            y += art + gap;
        }

        PlaceText(_track, x, y, box.Width, title.LineHeight, alignment);
        y += title.LineHeight;

        if (subtitle)
        {
            y += Fluent.Space1;
            PlaceText(_subtitle, x, y, box.Width, subtitleStep.LineHeight, alignment);
            y += subtitleStep.LineHeight;
        }

        y += gap;

        if (bar)
        {
            Place(_seek, x, y, box.Width, MusicSeekBar.BarHeight);
            y += MusicSeekBar.BarHeight;

            if (times)
            {
                PlaceTimesUnder(x, y, box.Width);
                y += FluentType.Caption.LineHeight;
            }

            y += gap;
        }

        if (transport)
        {
            var transportX = centred ? x + ((box.Width - transportWidth) / 2) : x;
            Place(_transport, transportX, y, transportWidth, side);
        }
    }

    /// <summary>S, M and Wide: [art] lines [transport] in a row, the seek bar under it.</summary>
    private void LayRow(WidgetArea area, WidgetArea box, double padY, double side)
    {
        var gap = Fluent.Space2;
        var x = Fluent.Space4;
        var bottom = padY + box.Height;
        var wide = area.WidthClass >= SizeClass.Expanded;

        // The footer first: it is fixed in size, and what is above it gets the rest.
        var footer = 0.0;
        if (_config.ShowProgress)
        {
            var timesUnder = !wide && area.HeightClass >= SizeClass.Regular;
            footer = MusicSeekBar.BarHeight + (timesUnder ? FluentType.Caption.LineHeight : 0);
            var top = bottom - footer;

            if (wide)
            {
                PlaceTimesBeside(x, top, box.Width);
            }
            else
            {
                Place(_seek, x, top, box.Width, MusicSeekBar.BarHeight);

                if (timesUnder)
                {
                    PlaceTimesUnder(x, top + MusicSeekBar.BarHeight, box.Width);
                }
            }

            footer += gap;
        }

        var bodyHeight = Math.Max(0, box.Height - footer);

        // The art is as tall as the body allows, within bounds; on a wide strip it is the whole body.
        var art = 0.0;
        if (_config.ShowAlbumArt)
        {
            art = Math.Min(RowArtMaximum, Math.Min(bodyHeight, box.Width * 0.35));

            if (art < ArtMinimum || area.WidthClass < SizeClass.Regular)
            {
                art = 0;
            }
        }

        if (art > 0)
        {
            Place(_artHost, x, padY + ((bodyHeight - art) / 2), art, art);
        }

        // Lines and transport: beside each other when the lines keep enough width, else stacked.
        var linesX = x + (art > 0 ? art + Fluent.Space3 : 0);
        var linesWidth = Math.Max(0, x + box.Width - linesX);
        var transport = _config.ShowTransport;
        var fullWidth = TransportWidth(side, playOnly: false);
        var beside = transport && linesWidth - fullWidth - Fluent.Space3 >= LinesMinimumWidth;
        var below = transport && !beside;

        var room = new Disclosure(linesWidth - (beside ? fullWidth + Fluent.Space3 : 0), bodyHeight, Fluent.Space1);
        room.Reserve(FluentType.BodyStrong.LineHeight);

        if (below && !room.Take(side + gap - Fluent.Space1))
        {
            // No row for the transport under the lines: play/pause alone, beside them.
            below = false;
            beside = true;
            fullWidth = TransportWidth(side, playOnly: true);
            room = new Disclosure(linesWidth - fullWidth - Fluent.Space3, bodyHeight, Fluent.Space1);
            room.Reserve(FluentType.BodyStrong.LineHeight);
        }

        var subtitle = _now.Subtitle.Length > 0 && room.Take(FluentType.Body.LineHeight);
        var textWidth = room.Width;
        var title = FluentType.Strong(FluentType.Fit(textWidth, FluentType.BodyStrong.LineHeight + Math.Min(room.Remaining, 12), 16, FluentType.Body, FluentType.Subtitle));
        _track.Apply(title);
        _subtitle.Apply(FluentType.Body);

        var linesHeight = title.LineHeight + (subtitle ? Fluent.Space1 + FluentType.Body.LineHeight : 0);
        var blockHeight = linesHeight + (below ? gap + side : 0);
        var y = padY + Math.Max(0, (bodyHeight - blockHeight) / 2);

        PlaceText(_track, linesX, y, textWidth, title.LineHeight, TextAlignment.Left);

        if (subtitle)
        {
            PlaceText(_subtitle, linesX, y + title.LineHeight + Fluent.Space1, textWidth, FluentType.Body.LineHeight, TextAlignment.Left);
        }

        if (beside)
        {
            Place(_transport, x + box.Width - fullWidth, padY + Math.Max(0, (bodyHeight - side) / 2), fullWidth, side);
            SetPlayOnly(fullWidth < TransportWidth(side, playOnly: false));
        }
        else if (below)
        {
            Place(_transport, linesX, y + linesHeight + gap, fullWidth, side);
        }
    }

    /// <summary>L and XL: the art as the picture, a column of detail beside it, the seek bar along the bottom.</summary>
    private void LayHero(WidgetArea area, WidgetArea box, double padY, double side)
    {
        var gap = Fluent.Space3;
        var x = Fluent.Space4;
        var bottom = padY + box.Height;

        var footer = 0.0;
        if (_config.ShowProgress)
        {
            footer = Math.Max(MusicSeekBar.BarHeight, FluentType.Caption.LineHeight);
            PlaceTimesBeside(x, bottom - footer, box.Width);
            footer += gap;
        }

        var bodyHeight = Math.Max(0, box.Height - footer);
        var art = _config.ShowAlbumArt ? Math.Min(HeroArtMaximum, Math.Min(bodyHeight, box.Width * 0.5)) : 0;

        if (art > 0)
        {
            Place(_artHost, x, padY + ((bodyHeight - art) / 2), art, art);
        }

        var colX = x + (art > 0 ? art + Fluent.Space5 : 0);
        var colWidth = Math.Max(0, x + box.Width - colX);
        var huge = area.Least >= SizeClass.Huge;

        var titleStep = FluentType.Strong(FluentType.Fit(colWidth, FluentType.TitleLarge.LineHeight, 18, FluentType.Subtitle, huge ? FluentType.TitleLarge : FluentType.Title));
        var artistStep = huge ? FluentType.Subtitle : FluentType.BodyLarge;
        var albumStep = huge ? FluentType.BodyLarge : FluentType.Body;

        var room = new Disclosure(colWidth, bodyHeight, Fluent.Space1);
        room.Reserve(titleStep.LineHeight);
        var transport = _config.ShowTransport && room.Take(side + gap);
        var artist = _now.Subtitle.Length > 0 && room.Take(artistStep.LineHeight);
        var album = _now.Album.Length > 0 && room.Take(albumStep.LineHeight);

        _track.Apply(titleStep);
        _subtitle.Apply(artistStep);
        _album.Apply(albumStep);

        // The artist alone on its own line here; the album has its own line under it.
        _subtitle.Text = _now.Artist.Length > 0 ? _now.Artist : _now.Subtitle;

        var height = titleStep.LineHeight
            + (artist ? Fluent.Space1 + artistStep.LineHeight : 0)
            + (album ? Fluent.Space1 + albumStep.LineHeight : 0)
            + (transport ? gap + side : 0);

        var y = padY + Math.Max(0, (bodyHeight - height) / 2);

        PlaceText(_track, colX, y, colWidth, titleStep.LineHeight, TextAlignment.Left);
        y += titleStep.LineHeight;

        if (artist)
        {
            y += Fluent.Space1;
            PlaceText(_subtitle, colX, y, colWidth, artistStep.LineHeight, TextAlignment.Left);
            y += artistStep.LineHeight;
        }

        if (album)
        {
            y += Fluent.Space1;
            PlaceText(_album, colX, y, colWidth, albumStep.LineHeight, TextAlignment.Left);
            y += albumStep.LineHeight;
        }

        if (transport)
        {
            y += gap;
            var width = TransportWidth(side, playOnly: false);
            Place(_transport, colX, y, width, side);
        }
    }

    // ---- placing ---------------------------------------------------------------------------------

    /// <summary>The seek bar with the elapsed time left of it and the total right of it, in one row.</summary>
    private void PlaceTimesBeside(double x, double y, double width)
    {
        var label = FluentType.EstimateWidth(FluentType.Caption, TimeCharacters);
        var row = Math.Max(MusicSeekBar.BarHeight, FluentType.Caption.LineHeight);

        _elapsed.TextAlignment = TextAlignment.Left;
        PlaceText(_elapsed, x, y + ((row - FluentType.Caption.LineHeight) / 2), label, FluentType.Caption.LineHeight, TextAlignment.Left);
        PlaceText(_total, x + width - label, y + ((row - FluentType.Caption.LineHeight) / 2), label, FluentType.Caption.LineHeight, TextAlignment.Right);
        Place(_seek, x + label + Fluent.Space2, y + ((row - MusicSeekBar.BarHeight) / 2), Math.Max(0, width - (2 * (label + Fluent.Space2))), MusicSeekBar.BarHeight);
    }

    /// <summary>The two times under the bar, at its ends — the media flyout's arrangement.</summary>
    private void PlaceTimesUnder(double x, double y, double width)
    {
        var label = Math.Min(width / 2, FluentType.EstimateWidth(FluentType.Caption, TimeCharacters));
        PlaceText(_elapsed, x, y, label, FluentType.Caption.LineHeight, TextAlignment.Left);
        PlaceText(_total, x + width - label, y, label, FluentType.Caption.LineHeight, TextAlignment.Right);
    }

    /// <summary>Square buttons on the 4px grid; a 20px glyph on the 40px buttons of the large tiles.</summary>
    private void SizeTransport(double side)
    {
        foreach (var button in new[] { _previous, _playPause, _next })
        {
            button.Width = side;
            button.Height = side;
        }

        foreach (var icon in _transportIcons)
        {
            icon.FontSize = side >= Fluent.RowHeight ? Fluent.Space5 : Fluent.IconSize;
        }

        SetPlayOnly(false);
    }

    private void SetPlayOnly(bool playOnly)
    {
        _previous.Visibility = playOnly ? Visibility.Collapsed : Visibility.Visible;
        _next.Visibility = playOnly ? Visibility.Collapsed : Visibility.Visible;
    }

    private double TransportWidth(double side, bool playOnly)
    {
        SetPlayOnly(playOnly);
        return playOnly ? side : (3 * side) + (2 * Fluent.Space1);
    }

    private static void PlaceText(TextBlock text, double x, double y, double width, double height, TextAlignment alignment)
    {
        text.TextAlignment = alignment;
        Place(text, x, y, width, height);
    }

    private static void Place(FrameworkElement element, double x, double y, double width, double height)
    {
        Canvas.SetLeft(element, x);
        Canvas.SetTop(element, y);
        element.Width = Math.Max(0, width);
        element.Height = Math.Max(0, height);
        element.Visibility = Visibility.Visible;
    }
}
