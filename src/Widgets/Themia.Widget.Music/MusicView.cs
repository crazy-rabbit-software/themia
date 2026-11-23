using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.Music;
using Windows.Foundation;

namespace Themia.App.Widgets;

/// <summary>
/// [S-16500] Now playing: whatever Windows is playing, from Windows' own media session (one <see cref="NowPlayingFeed"/> for
/// the whole widget; no account, no token, no per-player integration), drawn by its own face (<see cref="Face"/>) where
/// <see cref="MusicPlan"/> lays it: the cover, the title, the artist and album, the progress line (the elapsed time, the
/// seek bar, the length) and the transport, each shown or hidden on its own, in the style chosen. Event-driven: it redraws
/// when the session says something changed; the one thing on a clock is the position, a second while a track plays.
/// [S-16509] The face is one item, edge to edge, that lights where a click opens the player (WR-13); a button or the bar
/// under the pointer lights alone.
/// </summary>
internal sealed partial class MusicView : Grid, IWidgetView, IWidgetOpens, IWidgetSurfaceAware, IWidgetKeys, IWidgetTopEdge, IWidgetCorners
{
    /// <summary>[S-5700] It owns its top edge: the plan lays the inset on every side itself (Full art's art reaches the edge).</summary>
    public bool OwnsTopEdge => true;

    /// <inheritdoc />
    public CardOpen? Opens => MusicModule.Opens(_feed.Now);

    /// <summary>[S-16509] The face's one item: the card from edge to edge.</summary>
    private readonly Border _item = new();
    private readonly Face _face;

    /// <summary>[S-16509] The item's fill: over the art, under the texts.</summary>
    private readonly Border _light = new() { IsHitTestVisible = false };

    private readonly Border _cover = new();
    private readonly FontIcon _placeholder;
    private readonly ImageBrush _art = new() { Stretch = Stretch.UniformToFill };
    private readonly Border _scrim = new() { IsHitTestVisible = false };
    private readonly LinearGradientBrush _scrimFill = new() { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };

    private readonly TextBlock _title = Family.Text(FamilyType.BodyStrong, ColourRole.TextPrimary);
    private readonly TextBlock _subtitle = Family.Text(FamilyType.Body, ColourRole.TextSecondary);
    private readonly TextBlock _line = Family.Text(FamilyType.Body, ColourRole.TextPrimary);
    private readonly Microsoft.UI.Xaml.Documents.Run _lineTitle = new();
    private readonly Microsoft.UI.Xaml.Documents.Run _lineRest = new();
    private readonly TextBlock _elapsed = Family.Text(FamilyType.Caption, ColourRole.TextTertiary);
    private readonly TextBlock _slash = Family.Text(FamilyType.Caption, ColourRole.TextTertiary, "/");
    private readonly TextBlock _length = Family.Text(FamilyType.Caption, ColourRole.TextTertiary);
    private readonly MusicSeekBar _bar = new();
    private readonly Button _previous;
    private readonly Button _playPause;
    private readonly Button _next;
    private readonly NowPlayingFeed _feed;

    private Ticker? _ticker;

    /// <summary>The ticker subscription at the cadence the view needs now (<see cref="Ticker.Slot"/>).</summary>
    private readonly Ticker.Slot _tick = new();
    private MusicConfig _config = new();
    private IWidgetSurface? _surface;
    private CancellationTokenSource? _artwork;
    private string _artKey = string.Empty;
    private CornerRadius _corners;
    private long _scrimColour;
    private (SolidColorBrush Brush, long Token)? _scrimWatch;
    private bool _preview;
    private bool _hidden;

    // [S-16509] The pointer: over the item, over one of its controls, pressing it; whether the item lights at all.
    private bool _pointed;
    private bool _onControl;
    private bool _pressed;
    private bool _lit;

    public MusicView(IWidgetHost host)
    {
        _feed = new NowPlayingFeed(host.Media, Present);

        // [S-1113] Media transport and its timeline keep their order and direction in every language.
        FlowDirection = FlowDirection.LeftToRight;

        _placeholder = Family.Glyph(Glyphs.Music, FamilyShape.IconLarge, ColourRole.TextTertiary);
        _placeholder.HorizontalAlignment = HorizontalAlignment.Center;
        _placeholder.VerticalAlignment = VerticalAlignment.Center;
        _cover.Child = _placeholder;
        _cover.Background = Family.Brush(ColourRole.Control);
        _scrim.Background = _scrimFill;

        foreach (var text in new[] { _title, _subtitle, _line })
        {
            text.TextTrimming = TextTrimming.CharacterEllipsis;
            text.TextWrapping = TextWrapping.NoWrap;
        }

        _lineTitle.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        _lineRest.Foreground = Family.Brush(ColourRole.TextSecondary);
        _line.Inlines.Add(_lineTitle);
        _line.Inlines.Add(_lineRest);

        // [S-16510] The times stand in their template's box (WR-15): the elapsed time at its start, the length at its end.
        _elapsed.TextAlignment = TextAlignment.Left;
        _length.TextAlignment = TextAlignment.Right;
        HoldMark.Mark(_elapsed);
        HoldMark.Mark(_length);

        _previous = ControlsBlock.IconButton(Glyphs.Previous, MusicStr.WidgetsB_Music_Previous.Text(), () => _feed.Press(MediaTransport.Previous));
        _playPause = ControlsBlock.IconButton(Glyphs.Play, MusicStr.WidgetsB_Music_Play.Text(), () => _feed.Press(MediaTransport.PlayPause), main: true);
        _next = ControlsBlock.IconButton(Glyphs.Next, MusicStr.WidgetsB_Music_Next.Text(), () => _feed.Press(MediaTransport.Next));

        _face = new Face(this);
        _face.Add(MusicPart.Cover, _cover, scaled: false);
        _face.Add(MusicPart.Scrim, _scrim, scaled: false);
        _face.Children.Add(_light);
        _face.Add(MusicPart.Title, _title);
        _face.Add(MusicPart.Subtitle, _subtitle);
        _face.Add(MusicPart.Line, _line);
        _face.Add(MusicPart.Elapsed, _elapsed);
        _face.Add(MusicPart.Slash, _slash);
        _face.Add(MusicPart.Length, _length);
        _face.Add(MusicPart.Bar, _bar);
        _face.Add(MusicPart.Previous, _previous);
        _face.Add(MusicPart.PlayPause, _playPause);
        _face.Add(MusicPart.Next, _next);

        // [S-16509] The face is the item, the card edge to edge; its light follows the pointer, its controls light alone.
        ItemMark.Mark(_item);
        _item.Background = Family.Brush(ColourRole.Clear);
        _item.Child = _face;
        _item.PointerEntered += (_, _) => Point(over: true);
        _item.PointerExited += (_, _) => Point(over: false);
        _item.PointerCaptureLost += (_, _) => Point(over: false);
        _item.PointerCanceled += (_, _) => Point(over: false);
        _item.AddHandler(PointerPressedEvent, new PointerEventHandler((_, _) => Press(true)), handledEventsToo: true);
        _item.AddHandler(PointerReleasedEvent, new PointerEventHandler((_, _) => Press(false)), handledEventsToo: true);
        foreach (var control in new FrameworkElement[] { _previous, _playPause, _next, _bar })
        {
            control.PointerEntered += (_, _) => OnControl(true);
            control.PointerExited += (_, _) => OnControl(false);
        }

        // A render's --hover on the bar shows its thumb, as the pointer does.
        // (A preview's bar seeks nothing of the machine's; under a render's pointer it shows its thumb as a live one would.)
        PointerPreview.Register(_bar, _ =>
        {
            _bar.CanSeek = _feed.Now.HasTrack && _feed.Now.CanSeek;
            _bar.Point(true);
        });
        Children.Add(_item);

        _bar.Scrubbing += OnScrubbing;
        _bar.Committed += fraction => _feed.Seek(MusicRules.SeekTarget(fraction, _feed.Now.Timeline.Duration));
    }

    public void Bind(IWidgetSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        _surface = surface;
        _preview = surface.IsPreview;
        _feed.Preview = surface.IsPreview;
    }

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _ticker = ticker;
        _feed.Attach(DispatcherQueue);

        // [S-16505] The scrim follows its role's colour when the theme recolours it in place.
        if (_scrimWatch is null && Family.Brush(ColourRole.Solid) is SolidColorBrush ground)
        {
            _scrimWatch = (ground, ground.RegisterPropertyChangedCallback(SolidColorBrush.ColorProperty, (_, _) => Scrim()));
        }

        ApplyConfig(config);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as MusicConfig ?? new MusicConfig();
        _face.Shows(_config);
        _feed.Reload();
        Present();
    }

    /// <summary>[S-11883] The light and the art band reach the card's edges: they take its corners.</summary>
    public void ApplyCorners(CornerRadius corners)
    {
        _corners = corners;
        _item.CornerRadius = corners;
        _light.CornerRadius = corners;
        _face.InvalidateArrange();
    }

    /// <summary>Put the snapshot in hand on screen and hide or show the card.</summary>
    private void Present()
    {
        var now = _feed.Now;

        // [S-1464] Paint nothing while idle, if asked to. Never in a preview card: it always draws.
        var hide = !_preview && MusicRules.Hides(_config.HideWhenIdle, now.State);
        if (hide != _hidden)
        {
            _hidden = hide;
            _surface?.SetHidden(hide);
        }

        // [S-16508] The face stays with nothing to play (WR-10): "--" and why, every time "-:--", every button there, disabled.
        var track = now.HasTrack;
        var subtitleShown = _config.Has(MusicElement.Artist) || _config.Has(MusicElement.Album);
        var (title, subtitle) = MusicWords.Lines(now, _config.Has(MusicElement.Artist), _config.Has(MusicElement.Album), subtitleShown);
        _title.Text = title;
        _title.Foreground = Family.Brush(track ? ColourRole.TextPrimary : ColourRole.TextDisabled);
        _subtitle.Text = subtitle;

        // [S-16506] The strip's one line: the title, then the artist and album after a dot, whichever show.
        var head = _config.Has(MusicElement.Title) || !subtitleShown ? title : string.Empty;
        var rest = subtitleShown ? subtitle : string.Empty;
        _lineTitle.Text = head;
        _lineTitle.Foreground = Family.Brush(track ? ColourRole.TextPrimary : ColourRole.TextDisabled);
        _lineRest.Text = head.Length > 0 && rest.Length > 0 ? $" · {rest}" : rest;
        ToolTipService.SetToolTip(_item, track ? $"{now.Title}\n{now.Subtitle}".Trim() : MusicRules.StatusMessage(now.State));

        _face.Template(MusicPlan.TimeTemplate(now.Timeline.Duration));
        _bar.CanSeek = track && now.CanSeek && !_preview;
        UpdatePosition();
        Transport(now);
        Light(Opens is not null);
        Artwork(now);
        Resubscribe(now.State == MusicState.Playing && !_hidden && (_config.Has(MusicElement.Elapsed) || _config.Has(MusicElement.Bar)));
    }

    /// <summary>
    /// The transport as the session allows it. Disabled rather than hidden: a transport whose buttons come and go moves the
    /// two that remain, and the one under the pointer is then a different button than it was.
    /// </summary>
    private void Transport(NowPlaying now)
    {
        var track = now.HasTrack;
        var playing = now.State == MusicState.Playing;
        ControlsBlock.Reglyph(_playPause, playing ? Glyphs.Pause : Glyphs.Play, playing ? MusicStr.WidgetsB_Music_Pause.Text() : MusicStr.WidgetsB_Music_Play.Text());
        Enable(_previous, track && now.CanPrevious);
        _playPause.IsEnabled = track && now.CanPlayPause;
        Enable(_next, track && now.CanNext);
    }

    /// <summary>An icon button enabled or not, its glyph in the role that says so (its glyph names its own brush).</summary>
    private static void Enable(Button button, bool enabled)
    {
        button.IsEnabled = enabled;
        if (button.Content is FontIcon icon)
        {
            icon.Foreground = Family.Brush(enabled ? ColourRole.TextPrimary : ColourRole.TextDisabled);
        }
    }

    /// <summary>The second's tick only while a track plays and a time or the bar shows; nothing otherwise.</summary>
    private void Resubscribe(bool wanted) => _ = _tick.Set(_ticker, wanted ? TimeSpan.FromSeconds(1) : TimeSpan.Zero, UpdatePosition);

    /// <summary>Advance the bar and the times from the anchor. Display only: the next snapshot overwrites it.</summary>
    private void UpdatePosition()
    {
        var now = _feed.Now;
        var position = _feed.Position();
        var (elapsed, length) = MusicWords.Times(now, position);
        if (!_bar.IsScrubbing)
        {
            _bar.Value = now.HasTrack ? MusicRules.Progress(position, now.Timeline.Duration) : 0;
            _elapsed.Text = elapsed;
        }

        _length.Text = length;
    }

    /// <summary>[S-1465] The elapsed time follows the pointer while scrubbing; -1 means the scrub was abandoned.</summary>
    private void OnScrubbing(double fraction)
        => _elapsed.Text = fraction < 0
            ? MusicRules.Clock(_feed.Position())
            : MusicRules.Clock(MusicRules.SeekTarget(fraction, _feed.Now.Timeline.Duration));

    /// <summary>[S-11321] The cover's picture: read once per track and per reading of its media properties, where a cover shows.</summary>
    private void Artwork(NowPlaying now)
    {
        if (!_face.ShowsCover || !now.HasTrack)
        {
            _artwork?.Cancel();
            ShowArt(null, string.Empty);
            return;
        }

        // A preview card (the add page's picture, a render) reads no picture of the machine's: its sample brings its own.
        var key = MusicWords.ArtKey(now);
        if (key != _artKey && !_feed.Frozen && !_preview)
        {
            _ = LoadAsync(key);
        }
    }

    private async Task LoadAsync(string key)
    {
        _artwork?.Cancel();
        var cts = new CancellationTokenSource();
        _artwork = cts;
        try
        {
            var image = await _feed.ArtworkAsync(cts.Token).ConfigureAwait(true);

            // A sample shown meanwhile owns the face: the session's picture read before it never lands over it.
            if (!cts.IsCancellationRequested && !_feed.Frozen)
            {
                // Remembered even when there is none, so a track with no cover is not fetched again on every push.
                ShowArt(image, key);
            }
        }
        catch (OperationCanceledException)
        {
            // ignore: a newer track's art is already on its way.
        }
        finally
        {
            if (ReferenceEquals(_artwork, cts))
            {
                _artwork = null;
            }

            cts.Dispose();
        }
    }

    /// <summary>The picture, or the placeholder (the music glyph on the control fill) without one.</summary>
    private void ShowArt(ImageSource? image, string key)
    {
        _art.ImageSource = image;
        _cover.Background = image is null ? Family.Brush(ColourRole.Control) : _art;
        _placeholder.Visibility = image is null ? Visibility.Visible : Visibility.Collapsed;
        _artKey = key;
    }

    /// <summary>[S-16505] The scrim: the card's ground (the Solid role, its theme's) fading in from clear over the column.</summary>
    private void Scrim()
    {
        if (Family.Brush(ColourRole.Solid) is not SolidColorBrush solid)
        {
            return;
        }

        var ground = solid.Color;
        var key = ((long)ground.A << 24) | ((long)ground.R << 16) | ((long)ground.G << 8) | ground.B;
        if (key == _scrimColour && _scrimFill.GradientStops.Count > 0)
        {
            return;
        }

        _scrimColour = key;
        var strong = ground;
        strong.A = (byte)Math.Round(ground.A * MusicPlan.ScrimStrength);
        var clear = ground;
        clear.A = 0;
        _scrimFill.GradientStops.Clear();
        _scrimFill.GradientStops.Add(new GradientStop { Color = clear, Offset = 0 });
        _scrimFill.GradientStops.Add(new GradientStop { Color = strong, Offset = 0 });
        _scrimFill.GradientStops.Add(new GradientStop { Color = strong, Offset = 1 });
    }

    /// <summary>[S-16505] Where the scrim is fully in: the lead over the column, as a fraction of the scrim's height.</summary>
    private void ScrimLead(double fraction)
    {
        Scrim();
        if (_scrimFill.GradientStops.Count == 3 && !_scrimFill.GradientStops[1].Offset.Equals(fraction))
        {
            _scrimFill.GradientStops[1].Offset = fraction;
        }
    }

    /// <summary>[S-16509] Whether the face lights at all: only where a click opens the player.</summary>
    private void Light(bool on)
    {
        if (on != _lit)
        {
            _lit = on;
            if (on)
            {
                PointerPreview.Register(_item, pressed => ItemFill.Paint(_light, ItemRole.Row, pressed ? ItemState.Pressed : ItemState.Hover));
            }
            else
            {
                PointerPreview.Forget(_item);
            }
        }

        Paint();
    }

    private void Point(bool over)
    {
        _pointed = over;
        if (!over)
        {
            _onControl = false;
            _pressed = false;
        }

        Paint();
    }

    private void OnControl(bool over)
    {
        _onControl = over;
        Paint();
    }

    private void Press(bool down)
    {
        _pressed = down && !_onControl;
        Paint();
    }

    /// <summary>[S-16509] The light's fill: pressed, under the pointer (not on a control: a control lights alone) or at rest.</summary>
    private void Paint()
    {
        if (!_lit)
        {
            _light.Background = Family.Brush(ColourRole.Clear);
            return;
        }

        var state = _pressed ? ItemState.Pressed : _pointed && !_onControl ? ItemState.Hover : ItemState.Rest;
        ItemFill.Paint(_light, ItemRole.Row, state);
    }

    /// <summary>[S-1468] Only a tile with a track takes the keyboard on a click.</summary>
    public bool WantsKeys => _feed.Now.HasTrack && !_hidden;

    /// <summary>
    /// [S-1468] The original's keys: Space or Enter play/pause, Left/Right previous/next track,
    /// Shift+Left/Right five seconds back/forward.
    /// </summary>
    public bool HandleKey(WidgetKey key)
    {
        var now = _feed.Now;
        if (!now.HasTrack)
        {
            return false;
        }

        switch (key)
        {
            case WidgetKey.Space or WidgetKey.Enter when now.CanPlayPause:
                _feed.Press(MediaTransport.PlayPause);
                return true;
            case WidgetKey.Left when now.CanPrevious:
                _feed.Press(MediaTransport.Previous);
                return true;
            case WidgetKey.Right when now.CanNext:
                _feed.Press(MediaTransport.Next);
                return true;
            case WidgetKey.ShiftLeft or WidgetKey.ShiftRight when now.CanSeek:
                var delta = key == WidgetKey.ShiftLeft ? -MusicRules.NudgeStep : MusicRules.NudgeStep;
                _feed.Seek(MusicRules.Nudge(_feed.Position(), delta, now.Timeline.Duration));
                return true;
            default:
                return false;
        }
    }

    public void Detach()
    {
        _tick.Dispose();
        _ticker = null;
        _artwork?.Cancel();
        _artwork = null;
        _feed.Dispose();
        _art.ImageSource = null;
        _artKey = string.Empty;
        PointerPreview.Forget(_item);
        _lit = false;
        if (_scrimWatch is { } watch)
        {
            watch.Brush.UnregisterPropertyChangedCallback(SolidColorBrush.ColorProperty, watch.Token);
            _scrimWatch = null;
        }
    }

    /// <summary>
    /// [S-16502] The face: each part where <see cref="MusicPlan.Lay"/> puts it in the box the card gives, its texts, times, bar
    /// and buttons at their own size under the plan's one scale, the cover and the scrim at their box. The plan is asked
    /// again on every measure and arrange (a new box, a new style or set, a new time template), never by the pointer: a
    /// hover or a press changes no part's place or size (WR-12).
    /// </summary>
    private sealed partial class Face : Panel
    {
        private readonly MusicView _view;
        private readonly Dictionary<MusicPart, (FrameworkElement Element, ScaleTransform? Scale)> _parts = [];
        private MusicStyle _style;
        private IReadOnlyCollection<MusicElement> _shown = MusicElements.All;
        private string _template = MusicPlan.TimeTemplate(TimeSpan.Zero);

        /// <summary>The box the last measure planned the face for, and that plan.</summary>
        private (Size Box, MusicLayout Laid) _measured = (Size.Empty, new MusicLayout(1, new Dictionary<MusicPart, MusicBox>(), false));

        public Face(MusicView view) => _view = view;

        /// <summary>Whether the plan draws a cover in this style and set.</summary>
        public bool ShowsCover { get; private set; }

        public void Add(MusicPart part, FrameworkElement element, bool scaled = true)
        {
            ScaleTransform? scale = null;
            if (scaled)
            {
                // Drawn at its own size under the face's scale; laid out unrounded, the face on the card's pixels.
                scale = new ScaleTransform();
                element.RenderTransform = scale;
                element.UseLayoutRounding = false;
            }

            _parts[part] = (element, scale);
            Children.Add(element);
        }

        /// <summary>[S-16501] The style and the set: which parts show (the same at every size).</summary>
        public void Shows(MusicConfig config)
        {
            _style = config.Style;
            _shown = [.. config.Shows];
            var natural = MusicPlan.Sizing(_style, _shown).Natural;
            var laid = MusicPlan.Lay(_style, _shown, natural.Width, natural.Height, _template);
            foreach (var (part, (element, _)) in _parts)
            {
                element.Visibility = laid.Has(part) ? Visibility.Visible : Visibility.Collapsed;
            }

            ShowsCover = laid.Has(MusicPart.Cover);
            InvalidateMeasure();
        }

        /// <summary>[S-16510] The times' template: a new one (a track past ten minutes) lays them again; a second never does.</summary>
        public void Template(string template)
        {
            if (template != _template)
            {
                _template = template;
                InvalidateMeasure();
            }
        }

        private MusicLayout Lay(Size size) => MusicPlan.Lay(_style, _shown, size.Width, size.Height, _template);

        protected override Size MeasureOverride(Size availableSize)
        {
            var natural = MusicPlan.Sizing(_style, _shown).Natural;
            var size = new Size(double.IsFinite(availableSize.Width) ? availableSize.Width : natural.Width,
                double.IsFinite(availableSize.Height) ? availableSize.Height : natural.Height);
            var laid = Lay(size);
            _measured = (size, laid);
            _view._light.Measure(size);
            foreach (var (part, (element, scale)) in _parts)
            {
                element.Measure(laid.Parts.TryGetValue(part, out var box) ? Own(box, scale is null ? 1 : laid.Scale) : default);
            }

            return size;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            // The measure's face where the box is the measure's but for the card's pixel rounding: each part is laid in the
            // very box it was measured in. A face planned again for a box a fraction of a DIP off scaled a hair differently,
            // measured its parts again, asked for another measure and was planned for the first box again: a layout cycle
            // (the strip at its least) or a picture that never settled (a cut line, stacked).
            var (measured, laid) = _measured;
            var remeasure = Math.Abs(measured.Width - finalSize.Width) >= 1 || Math.Abs(measured.Height - finalSize.Height) >= 1;
            if (remeasure)
            {
                laid = Lay(finalSize);
            }

            _view._light.Arrange(new Rect(0, 0, finalSize.Width, finalSize.Height));
            foreach (var (part, (element, scale)) in _parts)
            {
                if (!laid.Parts.TryGetValue(part, out var box))
                {
                    element.Arrange(default);
                    continue;
                }

                if (scale is not null)
                {
                    scale.ScaleX = laid.Scale;
                    scale.ScaleY = laid.Scale;
                }

                var own = Own(box, scale is null ? 1 : laid.Scale);
                if (remeasure)
                {
                    element.Measure(own);
                }

                element.Arrange(new Rect(box.X, box.Y, own.Width, own.Height));
            }

            Dress(laid);
            return finalSize;
        }

        /// <summary>A part's own size: its box at the scale it is drawn under.</summary>
        private static Size Own(MusicBox box, double scale)
            => scale > 0 ? new Size(Math.Max(0, box.Width / scale), Math.Max(0, box.Height / scale)) : default;

        /// <summary>The cover's corners (the card's as a band, the one radius as a square), its glyph's size, the scrim's lead.</summary>
        private void Dress(MusicLayout laid)
        {
            var view = _view;
            if (laid.Parts.TryGetValue(MusicPart.Cover, out var cover))
            {
                var corners = laid.Band ? view._corners : Family.Radius;
                if (view._cover.CornerRadius != corners)
                {
                    view._cover.CornerRadius = corners;
                }

                var side = Math.Min(cover.Width, cover.Height);
                var glyph = side >= 2 * FamilyShape.IconHuge ? FamilyShape.IconHuge : side >= 2 * FamilyShape.IconLarge ? FamilyShape.IconLarge : FamilyShape.Icon;
                if (!view._placeholder.FontSize.Equals(glyph))
                {
                    // Its box with it: a glyph larger than its box is cut.
                    view._placeholder.FontSize = glyph;
                    view._placeholder.Width = glyph;
                    view._placeholder.Height = glyph;
                }
            }

            if (laid.Parts.TryGetValue(MusicPart.Scrim, out var scrim) && scrim.Height > 0)
            {
                view.ScrimLead(Math.Clamp(MusicPlan.ScrimLead * laid.Scale / scrim.Height, 0, 1));
            }
        }
    }
}
