using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// The gallery's ladder and drawing. [S-767]
/// <para>
/// Every picture-bearing part sits on one <see cref="Canvas"/> at a position the plan worked out —
/// the main picture or the grid / scatter / mosaic, and the hover bar — so one
/// layout pass places everything and nothing depends on a nested panel's measure
/// settling first. Picture frames are reused by position: a resize, or a slide moving on, re-points
/// the frames it has rather than throwing away decoded bitmaps and asking for them all again.
/// </para>
/// </summary>
internal sealed partial class GalleryView
{
    /// <summary>The gap between tiles.</summary>
    private const double Gap = Fluent.Space1;

    private readonly TextBlock _counter = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
    private readonly Canvas _canvas = new();
    private readonly RectangleGeometry _clip = new();
    private readonly List<GalleryFrame> _frames = [];
    private Border _messageHost = null!;
    private TextBlock _message = null!;
    private FontIcon _messageIcon = null!;

    private WidgetArea _area;
    private GalleryPlan? _plan;

    private void BuildParts()
    {
        _counter.Margin = new Thickness(Fluent.Space2, 0, 0, 0);
        Microsoft.UI.Xaml.Documents.Typography.SetNumeralAlignment(_counter, FontNumeralAlignment.Tabular);

        // Scatter prints are dealt near the edges and tilted; the clip keeps their corners on the card.
        _canvas.Clip = _clip;

        // [S-301] Every outcome with no picture in it names itself. A photo frame showing a blank
        // card is indistinguishable from a broken one, and "no folder chosen", "that folder is
        // gone" and "Windows will not let me read it" each need a different thing from the reader.
        _message = Fluent.Text(FluentType.Body);
        var empty = Fluent.EmptyState(Glyphs.Gallery, _message, out _messageIcon);

        _messageHost = new Border
        {
            Child = empty,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
        };

        Children.Add(_canvas);
        Children.Add(_messageHost);

        BuildCommands();
    }

    /// <inheritdoc />
    public void ApplySize(WidgetArea area)
    {
        _area = area;
        _clip.Rect = new Windows.Foundation.Rect(0, 0, area.Width, area.Height);
        Lay();
    }

    /// <summary>
    /// Work out the ladder for the current size and draw what is in hand. Called on a resize, a
    /// config change, a new scan and each slide.
    /// </summary>
    private void Lay()
    {
        if (_detached)
        {
            return;
        }

        if (_scan.Status != GalleryStatus.Ok || _pictures.Count == 0)
        {
            ShowMessage(GalleryFormatting.StatusMessage(_scan.Status, _resolved.Problem, _config.Source));
            return;
        }

        _messageHost.Visibility = Visibility.Collapsed;
        _canvas.Visibility = Visibility.Visible;

        if (!_area.IsMeasured)
        {
            return; // Not arranged yet; ApplySize brings us straight back.
        }

        var plan = GalleryPlan.For(_area, _config.Layout, _config.IsSingleSource, _pictures.Count, Gap);
        _plan = plan;

        _index = Math.Clamp(_index, 0, _pictures.Count - 1);

        switch (plan.Mode)
        {
            case GalleryMode.Grid:
                DrawGrid(plan);
                break;

            case GalleryMode.Scatter:
                DrawScatter(plan);
                break;

            case GalleryMode.Mosaic:
                DrawMosaic(plan);
                break;

            default:
                DrawMain(plan);
                break;
        }

        PlaceControls(plan);

        _counter.Text = plan.Counter && plan.Mode == GalleryMode.Slideshow
            ? Str.WidgetsB_Gallery_Counter.Format(("index", _index + 1), ("count", _pictures.Count))
            : string.Empty;
    }

    private Stretch PictureStretch => _config.Fit == GalleryFit.Fill ? Stretch.UniformToFill : Stretch.Uniform;

    /// <summary>One picture filling the main area: the slide, the file, the photo of the day.</summary>
    private void DrawMain(GalleryPlan plan)
    {
        var frames = Frames(_frames, 1);
        var path = _pictures[_index];
        Place(frames[0], new GalleryRect(0, 0, plan.MainWidth, plan.MainHeight), radius: 0, PictureStretch);
        Point(frames[0], path);
    }

    /// <summary>[S-766] Tiles as close to square as the count allows.</summary>
    private void DrawGrid(GalleryPlan plan)
    {
        var frames = Frames(_frames, plan.Pictures);
        var (columns, rows) = GalleryArrangement.GridShape(plan.MainWidth, plan.MainHeight, plan.Pictures, Gap);
        var width = (plan.MainWidth - ((columns - 1) * Gap)) / columns;
        var height = (plan.MainHeight - ((rows - 1) * Gap)) / rows;

        for (var i = 0; i < frames.Count; i++)
        {
            var x = (i % columns) * (width + Gap);
            var y = (i / columns) * (height + Gap);
            Place(frames[i], new GalleryRect(x, y, width, height), Fluent.ControlRadius, PictureStretch);
            Point(frames[i], _pictures[i]);
        }
    }

    /// <summary>[S-764] Square prints, tilted, the later ones on top. Always cropped to fill, as in the original.</summary>
    private void DrawScatter(GalleryPlan plan)
    {
        var frames = Frames(_frames, plan.Pictures);
        var prints = GalleryArrangement.Scatter(plan.MainWidth, plan.MainHeight, plan.Pictures, _seed);

        for (var i = 0; i < frames.Count; i++)
        {
            var print = prints[i];
            var frame = frames[i];
            var rect = new GalleryRect(print.CenterX - (print.Side / 2), print.CenterY - (print.Side / 2), print.Side, print.Side);
            Place(frame, rect, radius: 0, Stretch.UniformToFill);

            frame.Host.BorderThickness = new Thickness(1);
            frame.Host.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
            frame.Host.RenderTransform = new RotateTransform { Angle = print.Rotation };
            Canvas.SetZIndex(frame.Host, i);
            Point(frame, _pictures[i]);
        }
    }

    /// <summary>[S-765]/[S-17] Cells that tile the card exactly, full-bleed, no gaps, always cropped to fill.</summary>
    private void DrawMosaic(GalleryPlan plan)
    {
        var frames = Frames(_frames, plan.Pictures);
        var cells = GalleryArrangement.Mosaic(plan.MainWidth, plan.MainHeight, plan.Pictures, _seed);

        for (var i = 0; i < frames.Count; i++)
        {
            Place(frames[i], cells[i], radius: 0, Stretch.UniformToFill);
            Point(frames[i], _pictures[i]);
        }
    }

    private void ShowMessage(string text)
    {
        _plan = null;

        foreach (var frame in _frames)
        {
            frame.Release();
            _canvas.Children.Remove(frame.Host);
        }

        _frames.Clear();

        // [S-767] On a compact card the sentence keeps only its first line ("No folder chosen."):
        // the advice after it would be cut off mid-word anyway.
        var newline = text.IndexOf('\n', StringComparison.Ordinal);
        _message.Text = _area.WidthClass == SizeClass.Compact && newline > 0 ? text[..newline] : text;
        _counter.Text = string.Empty;
        _messageHost.Visibility = Visibility.Visible;
        _canvas.Visibility = Visibility.Collapsed;
        HideControls();

        // The glyph is dropped on a card too short to carry it: a tile showing only an icon says
        // less than one showing only the sentence.
        _messageIcon.Visibility = _area.HeightClass >= SizeClass.Regular
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    /// <summary>
    /// Grow or shrink a frame list to <paramref name="count"/>, reusing the frames already there.
    /// </summary>
    private List<GalleryFrame> Frames(List<GalleryFrame> frames, int count)
    {
        while (frames.Count > count)
        {
            var last = frames[^1];
            last.Release();
            _canvas.Children.Remove(last.Host);
            frames.RemoveAt(frames.Count - 1);
        }

        while (frames.Count < count)
        {
            var frame = new GalleryFrame();
            WireFrame(frame);
            _canvas.Children.Insert(0, frame.Host);
            frames.Add(frame);
        }

        return frames;
    }

    private static void Place(GalleryFrame frame, GalleryRect rect, double radius, Stretch stretch)
    {
        frame.Host.Width = Math.Max(0, rect.Width);
        frame.Host.Height = Math.Max(0, rect.Height);
        frame.Host.CornerRadius = new CornerRadius(radius);
        frame.Host.BorderThickness = new Thickness(0);
        frame.Host.RenderTransform = null;
        frame.Image.Stretch = stretch;
        Canvas.SetLeft(frame.Host, rect.X);
        Canvas.SetTop(frame.Host, rect.Y);
        Canvas.SetZIndex(frame.Host, 0);
    }

    /// <summary>
    /// Show <paramref name="path"/> in a frame, decoding only when the picture changed or the frame
    /// grew past what was decoded. A cropped picture needs more pixels than its frame is wide (a
    /// landscape photo filling a square shows only its middle), so a filled frame asks for its
    /// longer side and a half.
    /// </summary>
    private void Point(GalleryFrame frame, string path)
    {
        var fill = frame.Image.Stretch == Stretch.UniformToFill;
        var wanted = fill ? Math.Max(frame.Host.Width, frame.Host.Height) * 1.5 : frame.Host.Width;

        // [S-2760] The name is for a screen reader only: no caption and no hover tooltip shows it.
        AutomationProperties.SetName(frame.Host, Picture.Name(path));

        // [S-3112] The photo of the day's credit is its only tooltip; a local picture has none.
        // Frames are reused across sources, so a frame that showed Bing's photo is cleared here.
        var credit = _config.Source == GallerySource.Bing && !Picture.IsLocal(path) ? _scan.Credit?.Text : null;
        ToolTipService.SetToolTip(frame.Host, credit);

        if (string.Equals(frame.Path, path, StringComparison.Ordinal) && wanted <= frame.Decoded + 1)
        {
            return;
        }

        if (!string.Equals(frame.Path, path, StringComparison.Ordinal))
        {
            // A different picture: the old one must not linger while the new one decodes, or a
            // slide that moved on shows the previous picture under the new counter. A picture the
            // shared cache holds ([S-3411]) is put in below in this same pass, so nothing blanks.
            frame.Show(null);
        }

        frame.Cancel();
        var cts = new CancellationTokenSource();
        frame.Pending = cts;
        frame.Path = path;
        frame.Decoded = wanted;

        _ = ShowAsync(frame, path, wanted, cts);
    }

    /// <summary>
    /// Decode one picture and put it in its frame, unless the frame has been re-pointed or the widget
    /// torn down in the meantime.
    /// </summary>
    private async Task ShowAsync(GalleryFrame frame, string path, double logicalWidth, CancellationTokenSource cts)
    {
        try
        {
            // The decode is in physical pixels, so the frame's logical width is multiplied by the
            // monitor's scale. Getting this wrong is a picture that is crisp on one display and
            // soft on the one beside it.
            if (Service is not { } service)
            {
                return;
            }

            var scale = XamlRoot?.RasterizationScale ?? 1;
            var handle = await service.Pictures.LoadAsync(path, logicalWidth * scale, cts.Token).ConfigureAwait(true);

            if (handle is null)
            {
                return;
            }

            if (_detached || cts.IsCancellationRequested || !ReferenceEquals(frame.Pending, cts))
            {
                // The frame moved on: the picture stays cached for the next ask, but not pinned.
                handle.Dispose();
                return;
            }

            frame.Show(handle);
        }
        catch (OperationCanceledException)
        {
            // ignore: the frame was re-pointed; the decode that replaced this one draws instead.
        }
    }

    private void DetachParts()
    {
        DetachCommands();

        foreach (var frame in _frames)
        {
            frame.Release();
        }

        _frames.Clear();
        _canvas.Children.Clear();
    }

    /// <summary>One picture on the canvas: its frame, what it shows, and the decode in flight for it.</summary>
    private sealed class GalleryFrame
    {
        public GalleryFrame()
        {
            Image = new Image { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
            Host = new Border
            {
                Child = Image,
                Tag = this,
                BorderBrush = FluentLayout.Themed("CardStrokeColorDefaultBrush", Microsoft.UI.Colors.Gray),
                Background = FluentLayout.Themed("ControlAltFillColorSecondaryBrush", Microsoft.UI.Colors.Gray),
            };
        }

        public Border Host { get; }

        public Image Image { get; }

        public string? Path { get; set; }

        /// <summary>The logical width last asked of the decoder.</summary>
        public double Decoded { get; set; }

        /// <summary>
        /// The decode in flight. Cancelled but not disposed when replaced: a disposed source's token
        /// throws the moment a waiter queued behind the loader's gate touches it, and this holds no
        /// timer or handle, so letting it go is the correct end of its life.
        /// </summary>
        public CancellationTokenSource? Pending { get; set; }

        public void Cancel()
        {
            var pending = Pending;
            Pending = null;
            pending?.Cancel();
        }

        /// <summary>[S-3411] The picture on show, pinned in the shared cache while this frame holds it.</summary>
        private PictureHandle? Handle { get; set; }

        /// <summary>Show a picture (or none), letting go of the one before.</summary>
        public void Show(PictureHandle? handle)
        {
            var previous = Handle;
            Handle = handle;
            Image.Source = handle?.Image;
            previous?.Dispose();
        }

        public void Release()
        {
            Cancel();
            Show(null);
            Path = null;
            Decoded = 0;
        }
    }

    /// <summary>What a picture entry is: a local file, or the photo of the day's address.</summary>
    private static class Picture
    {
        public static bool IsLocal(string path)
            => !(Uri.TryCreate(path, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps);

        public static string Name(string path)
        {
            if (!IsLocal(path))
            {
                return Str.WidgetsB_Gallery_PhotoOfTheDay.Text();
            }

            try
            {
                return System.IO.Path.GetFileName(path);
            }
            catch (ArgumentException)
            {
                return path; // ignore: a path the scan accepted always has a name; this is belt and braces.
            }
        }
    }
}
