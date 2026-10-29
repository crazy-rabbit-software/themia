using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.Sdk;
using Windows.Foundation;
using Themia.Widget.Scratchpad;

namespace Themia.App.Widgets;

/// <summary>
/// A surface to draw on.
/// <para>
/// <b>The drawing is kept as strokes, not as a picture.</b> The original web widget serialised its
/// canvas to a base64 PNG and stored that in the widget's config; here that would be wrong three
/// times over. The state file is one document written atomically by a single writer, so a few
/// hundred kilobytes of base64 would be rewritten in full every time <i>any</i> widget saved —
/// dragging a clock across the desktop would rewrite the sketch. A bitmap is also pinned to the
/// size it was captured at, so every resize resamples it and the drawing goes soft, while strokes
/// are redrawn at whatever size the widget now is and stay sharp. And strokes can be undone, erased
/// one at a time and read in the file; a PNG can only be replaced. See <see cref="ScratchpadConfig"/>.
/// </para>
/// <para>
/// <b>What the eraser does changed with it.</b> The original cut pixels with
/// <c>destination-out</c>, which has no meaning for geometry. Here the eraser removes whole strokes
/// it touches, which is what a vector eraser does, is undoable, and never leaves a scratched-out
/// smear behind. That is a deliberate behaviour change and the only one.
/// </para>
/// <para>
/// <b>Pointer input.</b> The card above this view starts a drag on <c>PointerPressed</c> and
/// captures the pointer <i>only in edit mode</i>, and it never marks the press handled. So outside
/// edit mode nothing competes and this view draws; in edit mode the card takes capture on the press
/// and this view simply stops receiving the gesture — which is exactly the discrimination the pad
/// needs, with no new API between them. Every stroke is therefore committed on a release this view
/// actually saw: a press it never sees the end of was a widget being dragged, and is discarded.
/// </para>
/// </summary>
internal sealed partial class ScratchpadView : Grid, IWidgetView, IWidgetSurfaceAware, IWidgetHeaderContent, IWidgetSizeAware, IWidgetContextActions
{
    private const string Scope = "scratchpad";

    /// <summary>How many steps back the pad remembers. Enough to undo a bad minute, not a session.</summary>
    private const int UndoDepth = 32;

    /// <summary>[S-651] The header's two tools, Undo and Full screen (the fold drops Full screen first).</summary>
    private readonly HeaderParts _header = new();
    private readonly Border _undoButton;
    private readonly Border _fullButton;

    private readonly Canvas _pad = new();

    /// <summary>[S-6714] "Draw here": the Status row at the top of an empty pad.</summary>
    private readonly StatusBlock _hint = new();

    /// <summary>One visual per committed stroke, in the same order, so an erase can remove both together.</summary>
    private readonly List<UIElement> _visuals = [];

    private readonly List<Change> _undo = [];

    private ScratchpadConfig _config = new();
    private IWidgetSurface? _surface;

    /// <summary>The stroke being drawn, in pad units. Empty when nothing is being drawn.</summary>
    private readonly List<InkPoint> _current = [];

    private Polyline? _live;
    private Pointer? _captured;
    private Change? _erasing;

    private bool _pressed;
    private bool _detached;

    /// <summary>The gesture is a pen's eraser end: it erases whatever tool is chosen.</summary>
    private bool _penEraser;

    /// <summary>[S-1498] The eraser is the chosen tool (the ink bar's, or full screen's).</summary>
    private bool _eraserOn;

    public ScratchpadView()
    {
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        _undoButton = _header.Add(HeaderPartKind.Button, HeaderBlock.Button(Glyphs.Reset, ScratchpadStr.WidgetsC_Scratchpad_Undo.Text(), Undo));
        _fullButton = _header.Add(HeaderPartKind.Button, HeaderBlock.Button(Glyphs.FullScreen, ScratchpadStr.WidgetsC_Scratchpad_FullScreen.Text(), ToggleFullScreen));

        // The clear role rather than no brush: a panel with a null background is not hit-testable at
        // all, and the pad would never see a pointer. The card does the same thing for its own drag area.
        _pad.Background = Family.Brush(ColourRole.Clear);

        _hint.Show(StatusKind.Empty, ScratchpadModule.Glyph, ScratchpadStr.Scratchpad_DrawHere.Text());

        // [S-07] The hint never intercepts the pointer: the first stroke has to land on the pad
        // even when it starts on top of the words.
        _hint.IsHitTestVisible = false;

        Children.Add(_pad);
        Children.Add(_hint);

        _pad.PointerPressed += OnPointerPressed;
        _pad.PointerMoved += OnPointerMoved;
        _pad.PointerReleased += OnPointerReleased;
        _pad.PointerCaptureLost += OnPointerCaptureLost;
        _pad.PointerCanceled += OnPointerCaptureLost;

        _pad.SizeChanged += OnPadSizeChanged;

        // [S-1498] The eraser is the bar's tool (and full screen's, which shows the bar).
        _barEraser = GlyphTool(Glyphs.EraseTool, ScratchpadStr.WidgetsC_Scratchpad_EraseStrokes.Text(), () => SetEraser(!_eraserOn));
        BuildBar();
    }

    // -----------------------------------------------------------------------------------------
    // Chrome
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// Clearing throws away work that has no other copy, so it asks first, through the widgets' one
    /// confirmation ([S-1500]), beside <paramref name="anchor"/>: a popup of its own, so the surface's
    /// region does not clip it to the tile. The undo stack survives a clear, but only until the app
    /// closes, so this is still the last word on a drawing.
    /// </summary>
    private async Task AskToClearAsync(FrameworkElement anchor)
    {
        if (_config.Strokes.Count == 0)
        {
            return;
        }

        if (await ConfirmFlyout.AskAsync(anchor, ScratchpadStr.WidgetsC_Scratchpad_ClearConfirm.Text(), Str.Common_Clear.Text()).ConfigureAwait(true) && !_detached)
        {
            ClearAll();
        }
    }

    /// <summary>
    /// [S-1501] Clearing the drawing is the pad's own command, on its card menu while there is a
    /// drawing (and on full screen's toolbar): not a header tool, which holds two at most ([S-6710]).
    /// </summary>
    public IReadOnlyList<WidgetAction> ContextActions
        => _config.Strokes.Count > 0
            ? [new(Glyphs.Delete, ScratchpadStr.WidgetsC_Scratchpad_ClearDrawing.Text(), () => _ = AskToClearAsync(this))]
            : [];

    // -----------------------------------------------------------------------------------------
    // Lifecycle
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// [S-651] The pad's two header tools, Undo and Full screen. Built in the constructor because the
    /// card reads this once, when it builds that header.
    /// </summary>
    public FrameworkElement? HeaderTrailing => _header;

    /// <inheritdoc />
    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        // Nothing to tick: a drawing changes when it is drawn on.
        Strings.Changed += OnLanguage;
        ApplyConfig(config);
    }

    /// <summary>[S-1110] Re-read the texts set once at construction; the drawing is untouched.</summary>
    private void OnLanguage(Locale locale)
    {
        Tip(_undoButton, ScratchpadStr.WidgetsC_Scratchpad_Undo.Text());
        Tip(_fullButton, FullScreenTip());
        _hint.Show(StatusKind.Empty, ScratchpadModule.Glyph, ScratchpadStr.Scratchpad_DrawHere.Text());
        ApplyBarTips();
        UpdateCommands();
    }

    public void ApplyConfig(WidgetConfig config)
    {
        var incoming = config as ScratchpadConfig ?? new ScratchpadConfig();

        if (!ReferenceEquals(incoming, _config))
        {
            // A different config object means a different widget's drawing — a paste, a duplicate,
            // a view switch. Whatever is half-drawn here belongs to the old one, and the undo stack
            // refers to strokes that are no longer on this pad.
            AbandonStroke();
            _undo.Clear();
            _config = incoming;
        }

        // [S-1501] Show tools: the header's two and the ink bar. Full screen keeps its own toolbar.
        _header.Want(_undoButton, _config.ShowTools);
        _header.Want(_fullButton, _config.ShowTools);

        RenderAll();
        UpdateCommands();
        LayBar();
    }

    public void Detach()
    {
        if (_detached)
        {
            return;
        }

        // The last stroke has no other copy: commit it before anything is unhooked.
        CommitStroke();

        // [S-1499] A view switch or a removed widget takes its full-screen pad with it.
        ExitFullScreen();
        _detached = true;
        Strings.Changed -= OnLanguage;

        ReleaseCapture();

        _pad.PointerPressed -= OnPointerPressed;
        _pad.PointerMoved -= OnPointerMoved;
        _pad.PointerReleased -= OnPointerReleased;
        _pad.PointerCaptureLost -= OnPointerCaptureLost;
        _pad.PointerCanceled -= OnPointerCaptureLost;

        _pad.SizeChanged -= OnPadSizeChanged;
    }

    // -----------------------------------------------------------------------------------------
    // Input
    // -----------------------------------------------------------------------------------------

    private void OnPointerPressed(object sender, PointerRoutedEventArgs args)
    {
        // Left button (or a pen's tip or eraser end) only: a right press opens the card's context
        // menu and must not paint.
        var properties = args.GetCurrentPoint(_pad).Properties;
        if (_detached || !(properties.IsLeftButtonPressed || properties.IsEraser))
        {
            return;
        }

        AbandonStroke();
        _pressed = true;

        // [S-1498] The pen's eraser end erases, whichever tool is chosen, as it does in Windows Ink.
        _penEraser = properties.IsEraser;

        if (!Erasing)
        {
            _current.Add(PadPoint(args));
        }

        // Deliberately NOT handled, and deliberately no capture yet. In edit mode the card's own
        // handler runs next and takes the pointer for a drag; taking capture here would either fail
        // or steal the drag, and marking the press handled would stop the card from starting one at
        // all.
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs args)
    {
        if (_detached || !_pressed)
        {
            return;
        }

        var properties = args.GetCurrentPoint(_pad).Properties;
        if (!(properties.IsLeftButtonPressed || properties.IsEraser))
        {
            // The button came up somewhere this view never saw — in edit mode, under the card's
            // capture. Whatever was pressed was not a stroke.
            AbandonStroke();
            return;
        }

        // The first move is where this becomes a gesture of ours rather than one the card might
        // take, so this is where the pad asks for the pointer. In edit mode the card already has it
        // and this line is never reached, because a captured pointer stops raising events here.
        if (_captured is null && _pad.CapturePointer(args.Pointer))
        {
            _captured = args.Pointer;
        }

        var point = PadPoint(args);

        if (Erasing)
        {
            Erase(point);
        }
        else
        {
            Extend(point);
        }

        args.Handled = true;
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs args)
    {
        if (_detached || !_pressed)
        {
            return;
        }

        // A tap with the eraser, which never raised a move. Safe to do here and nowhere else: this
        // view only sees a release when the card did NOT take the pointer, so a press that was a
        // widget drag can never rub anything out.
        if (Erasing && _erasing is null)
        {
            Erase(PadPoint(args));
        }

        CommitStroke();
        ReleaseCapture();

        // Handled so the gesture recogniser does not also read this as a tap. Without it a pair of
        // quick dots would reach the card as a double tap and flip the desktop into edit mode in
        // the middle of drawing.
        args.Handled = true;
    }

    private void OnPointerCaptureLost(object sender, PointerRoutedEventArgs args)
    {
        // Capture can go without a release — a display change, another window taking input. What is
        // drawn so far is still what the user drew, so it is kept rather than thrown away.
        if (_pressed)
        {
            CommitStroke();
        }

        _captured = null;
    }

    /// <summary>[S-1498] This gesture erases: the eraser is chosen, or it is a pen's eraser end.</summary>
    private bool Erasing => _eraserOn || _penEraser;

    /// <summary>Where the pointer is, in the pad's unit square, clamped to it.</summary>
    private InkPoint PadPoint(PointerRoutedEventArgs args)
    {
        var position = args.GetCurrentPoint(_pad).Position;
        var width = _pad.ActualWidth;
        var height = _pad.ActualHeight;

        if (!(width > 0) || !(height > 0))
        {
            return new InkPoint(0, 0);
        }

        return InkGeometry.Clamp(new InkPoint(position.X / width, position.Y / height));
    }

    // -----------------------------------------------------------------------------------------
    // Drawing
    // -----------------------------------------------------------------------------------------

    private void Extend(InkPoint point)
    {
        if (_current.Count > 0 && !InkGeometry.ShouldKeep(_current[^1], point))
        {
            return;
        }

        if (_current.Count >= InkGeometry.MaxPointsPerStroke)
        {
            return;
        }

        _current.Add(point);

        _live ??= NewLine(InkBrush(_config.EffectivePenColor), _config.EffectivePenWidth);

        if (!_pad.Children.Contains(_live))
        {
            _pad.Children.Add(_live);
        }

        _live.Points.Add(ToPixels(point));
        _hint.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Cut every stroke the eraser is over.
    /// <para>
    /// Measured in <b>pixels</b>, not in pad units: the eraser should be the size it looks on
    /// screen, and a pad dragged into a long thin shape would otherwise erase in an ellipse.
    /// </para>
    /// </summary>
    private void Erase(InkPoint probe)
    {
        var width = _pad.ActualWidth;
        var height = _pad.ActualHeight;

        if (!(width > 0) || !(height > 0))
        {
            return;
        }

        var radius = _config.EffectivePenWidth * InkGeometry.EraserScale / 2;
        var target = new InkPoint(probe.X * width, probe.Y * height);

        for (var i = _config.Strokes.Count - 1; i >= 0; i--)
        {
            var points = InkGeometry.Unpack(_config.Strokes[i].Points);
            var scaled = new List<InkPoint>(points.Count);

            foreach (var point in points)
            {
                scaled.Add(new InkPoint(point.X * width, point.Y * height));
            }

            if (!InkGeometry.Touches(scaled, target, radius))
            {
                continue;
            }

            // One undo entry for the whole sweep, not one per stroke: an eraser drag is a single
            // thing the user did, and undoing it twenty times is not undo.
            _erasing ??= new Change(ChangeKind.Erase);
            _erasing.Removed.Add((i, _config.Strokes[i]));

            _config.Strokes.RemoveAt(i);

            if (i < _visuals.Count)
            {
                _pad.Children.Remove(_visuals[i]);
                _visuals.RemoveAt(i);
            }
        }
    }

    /// <summary>
    /// End the gesture: keep what was drawn or erased, and remember how to take it back.
    /// Idempotent, so calling it from the release, the capture loss and <see cref="Detach"/> costs
    /// nothing.
    /// </summary>
    private void CommitStroke()
    {
        var changed = false;

        if (_erasing is { Removed.Count: > 0 })
        {
            Remember(_erasing);
            _erasing = null;
            changed = true;
        }

        if (_current.Count > 0)
        {
            if (_config.Strokes.Count >= InkGeometry.MaxStrokes)
            {
                // Refusing beats dropping the oldest stroke: the pad is the user's, and silently
                // deleting part of their drawing to make room for more of it is not a trade they
                // were offered.
                Log.Warn(Scope, $"the pad is full at {InkGeometry.MaxStrokes} strokes; the last one was not kept");
                AbandonStroke();
                UpdateCommands();
                return;
            }

            var stroke = new InkStroke
            {
                Color = _config.EffectivePenColor,
                Width = _config.EffectivePenWidth,
                Points = InkGeometry.Pack(_current),
            };

            _config.Strokes.Add(stroke);
            Remember(new Change(ChangeKind.Add));

            // The line that was drawn live IS the stroke's visual once it is a single point or more
            // than one; a lone point has to become a dot, which a polyline cannot draw.
            RemoveLive();
            _visuals.Add(Draw(stroke));
            changed = true;
        }

        _current.Clear();
        _pressed = false;
        _erasing = null;
        RemoveLive();

        if (changed)
        {
            // The store coalesces; there is no second debounce here, and no second writer.
            _surface?.RequestSave();
        }

        UpdateHint();
        UpdateCommands();
    }

    /// <summary>Throw away a gesture that was never ours — a widget drag, a press with the wrong button.</summary>
    private void AbandonStroke()
    {
        _current.Clear();
        _pressed = false;
        _erasing = null;
        RemoveLive();
    }

    private void RemoveLive()
    {
        if (_live is not null)
        {
            _pad.Children.Remove(_live);
            _live = null;
        }
    }

    private void ReleaseCapture()
    {
        if (_captured is null)
        {
            return;
        }

        _pad.ReleasePointerCapture(_captured);
        _captured = null;
    }

    // -----------------------------------------------------------------------------------------
    // Undo and clear
    // -----------------------------------------------------------------------------------------

    private void Remember(Change change)
    {
        if (change.Kind == ChangeKind.Clear)
        {
            change.Removed.Clear();

            for (var i = 0; i < _config.Strokes.Count; i++)
            {
                change.Removed.Add((i, _config.Strokes[i]));
            }
        }

        _undo.Add(change);

        while (_undo.Count > UndoDepth)
        {
            _undo.RemoveAt(0);
        }
    }

    private void Undo()
    {
        if (_undo.Count == 0)
        {
            return;
        }

        var change = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);

        switch (change.Kind)
        {
            case ChangeKind.Add when _config.Strokes.Count > 0:
                _config.Strokes.RemoveAt(_config.Strokes.Count - 1);
                break;

            case ChangeKind.Clear:
            case ChangeKind.Erase:
                // Ascending, so each stroke lands back at the index it was taken from.
                foreach (var (index, stroke) in change.Removed)
                {
                    _config.Strokes.Insert(Math.Clamp(index, 0, _config.Strokes.Count), stroke);
                }

                break;

            default:
                break;
        }

        RenderAll();
        UpdateCommands();
        _surface?.RequestSave();
    }

    private void ClearAll()
    {
        if (_config.Strokes.Count == 0)
        {
            return;
        }

        Remember(new Change(ChangeKind.Clear));
        _config.Strokes.Clear();

        RenderAll();
        UpdateCommands();
        _surface?.RequestSave();
    }

    /// <summary>[S-1498] Choose the eraser, or the pen again.</summary>
    private void SetEraser(bool on)
    {
        if (on == _eraserOn)
        {
            return;
        }

        // Switching tool mid-gesture would otherwise finish a pen stroke as an erase.
        AbandonStroke();
        _eraserOn = on;
        UpdateCommands();
    }

    private void UpdateCommands()
    {
        // A tool with nothing to do now is drawn in the disabled role and ignores a tap.
        Enable(_undoButton, _undo.Count > 0, ColourRole.TextSecondary);
        UpdateFullScreenTools();
        Tip(_barEraser, _eraserOn ? ScratchpadStr.WidgetsC_Scratchpad_Draw.Text() : ScratchpadStr.WidgetsC_Scratchpad_EraseStrokes.Text());
        UpdateBar();
    }

    // -----------------------------------------------------------------------------------------
    // Rendering
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// The pad is an inner drawing host, so it re-plots on its own size: the drawing is in pad units,
    /// so a resize is a re-plot, not a resample — the whole reason the strokes are geometry. The
    /// widget's own layout (header tools, the ink bar) comes from <see cref="ApplySize"/>.
    /// </summary>
    private void OnPadSizeChanged(object sender, SizeChangedEventArgs args) => RenderAll();

    /// <summary>
    /// Redraw every stroke at the pad's current size. Called on a resize and after anything that
    /// changes the list — never per frame while drawing, where only the live line is touched.
    /// </summary>
    private void RenderAll()
    {
        if (_detached)
        {
            return;
        }

        var width = _pad.ActualWidth;
        var height = _pad.ActualHeight;

        // Half a stroke width can sit outside the pad at the very edge; the clip keeps it off the
        // card's border rather than relying on the card to catch it.
        _pad.Clip = width > 0 && height > 0
            ? new RectangleGeometry { Rect = new Rect(0, 0, width, height) }
            : null;

        _pad.Children.Clear();
        _visuals.Clear();
        _live = null;

        foreach (var stroke in _config.Strokes)
        {
            _visuals.Add(Draw(stroke));
        }

        UpdateHint();
    }

    /// <summary>Put one stroke on the pad and hand back its visual, so an erase can take it off again.</summary>
    private UIElement Draw(InkStroke stroke)
    {
        var points = InkGeometry.Unpack(stroke.Points);
        var width = InkGeometry.ClampWidth(stroke.Width);

        var brush = InkBrush(stroke.Color);

        if (points.Count == 1)
        {
            // A tap. A polyline of one point draws nothing at all, so a dot is a dot.
            var dot = new Ellipse
            {
                Width = width,
                Height = width,
                Fill = brush,
            };

            var centre = ToPixels(points[0]);
            Canvas.SetLeft(dot, centre.X - (width / 2));
            Canvas.SetTop(dot, centre.Y - (width / 2));
            _pad.Children.Add(dot);
            return dot;
        }

        var line = NewLine(brush, width);

        foreach (var point in points)
        {
            line.Points.Add(ToPixels(point));
        }

        _pad.Children.Add(line);
        return line;
    }

    private static Polyline NewLine(Brush brush, double width) => new()
    {
        Stroke = brush,
        StrokeThickness = width,
        StrokeLineJoin = PenLineJoin.Round,
        StrokeStartLineCap = PenLineCap.Round,
        StrokeEndLineCap = PenLineCap.Round,
    };

    /// <summary>
    /// The ink, as a brush: a data colour ([S-6705], <see cref="Family.Data"/>), what was drawn. A
    /// colour that no longer parses falls back to the default pen rather than reaching a brush as nothing.
    /// </summary>
    private static Brush InkBrush(string? hex)
    {
        var normalised = HexColor.Normalise(hex);
        return Family.Data(normalised.Length > 0 && HexColor.TryParse(normalised, out _, out _, out _) ? normalised : ScratchpadConfig.DefaultPenColor);
    }

    private Point ToPixels(InkPoint point) => new(point.X * _pad.ActualWidth, point.Y * _pad.ActualHeight);

    /// <summary>[S-07] The hint is shown only on a pad with nothing on it.</summary>
    private void UpdateHint()
        => _hint.Visibility = _config.Strokes.Count == 0 && _current.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;

    // -----------------------------------------------------------------------------------------

    private enum ChangeKind
    {
        Add,
        Erase,
        Clear,
    }

    /// <summary>One undoable thing the user did, and what it takes to put it back.</summary>
    private sealed class Change(ChangeKind kind)
    {
        public ChangeKind Kind { get; } = kind;

        /// <summary>The strokes taken away, with the index each came from. Empty for an add.</summary>
        public List<(int Index, InkStroke Stroke)> Removed { get; } = [];
    }
}
