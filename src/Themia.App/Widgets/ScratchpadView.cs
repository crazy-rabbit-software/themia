using Microsoft.UI;
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
using Windows.Foundation;

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
internal sealed partial class ScratchpadView : Grid, IWidgetView, IWidgetSurfaceAware, IWidgetHeaderContent, IWidgetSizeAware
{
    private const string Scope = "scratchpad";

    /// <summary>How many steps back the pad remembers. Enough to undo a bad minute, not a session.</summary>
    private const int UndoDepth = 32;

    private readonly StackPanel _tools;
    private readonly ToggleButton _eraser;
    private readonly Button _undoButton;
    private readonly Button _clearButton;
    private readonly Button _fullButton;

    private readonly Canvas _pad = new();
    private readonly StackPanel _hint;
    private readonly TextBlock _hintText = new();

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

    public ScratchpadView()
    {
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        _eraser = ToggleCommand(Glyphs.EraseTool, Str.WidgetsC_Scratchpad_EraseStrokes.Text());
        _eraser.Checked += OnEraserToggled;
        _eraser.Unchecked += OnEraserToggled;

        _undoButton = CommandButton(Glyphs.Reset, Str.WidgetsC_Scratchpad_Undo.Text());
        _undoButton.Click += OnUndoClicked;

        _clearButton = CommandButton(Glyphs.Delete, Str.WidgetsC_Scratchpad_ClearDrawing.Text());
        _clearButton.Flyout = BuildClearConfirmation();

        _fullButton = CommandButton(Glyphs.FullScreen, Str.WidgetsC_Scratchpad_FullScreen.Text());
        _fullButton.Click += OnFullScreenClicked;

        _tools = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Fluent.Space1,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { _eraser, _undoButton, _clearButton, _fullButton },
        };

        // A transparent brush rather than no brush: a panel with a null background is not
        // hit-testable at all, and the pad would never see a pointer. The card does the same thing
        // for its own drag area.
        _pad.Background = FluentLayout.Themed("SubtleFillColorTransparentBrush", Colors.Transparent);

        _hintText.Text = Str.Scratchpad_DrawHere.Text();
        _hint = Fluent.EmptyState(
            WidgetCatalog.Definition(WidgetKind.Scratchpad).Glyph,
            _hintText,
            out _);

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
        BuildBar();
    }

    // -----------------------------------------------------------------------------------------
    // Chrome
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// A header command: a glyph with the stock subtle fill behind it and nothing at rest.
    /// <para>
    /// The brushes go in through the template's own resource keys rather than onto
    /// <c>Background</c>, because the stock button's visual states assign the pointer-over and
    /// pressed fills straight onto the border element and would outrank a local value — the same
    /// reason the note editor does it, see <c>NotesView.BuildEditor</c>.
    /// </para>
    /// </summary>
    private static Button CommandButton(string glyph, string tooltip)
    {
        var button = new Button
        {
            Content = Fluent.Icon(glyph),
            Padding = new Thickness(Fluent.Space1),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(Fluent.ControlRadius),
            MinWidth = 0,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var clear = FluentLayout.Themed("SubtleFillColorTransparentBrush", Colors.Transparent);
        var hover = FluentLayout.Themed("SubtleFillColorSecondaryBrush", Colors.Gray);
        var pressed = FluentLayout.Themed("SubtleFillColorTertiaryBrush", Colors.DimGray);

        button.Resources["ButtonBackground"] = clear;
        button.Resources["ButtonBackgroundPointerOver"] = hover;
        button.Resources["ButtonBackgroundPressed"] = pressed;
        button.Resources["ButtonBackgroundDisabled"] = clear;
        button.Resources["ButtonBorderBrush"] = clear;
        button.Resources["ButtonBorderBrushPointerOver"] = clear;
        button.Resources["ButtonBorderBrushPressed"] = clear;
        button.Resources["ButtonBorderBrushDisabled"] = clear;

        ToolTipService.SetToolTip(button, tooltip);
        return button;
    }

    /// <summary>The same command, as a toggle: Windows lights a chosen tool with the accent fill.</summary>
    private static ToggleButton ToggleCommand(string glyph, string tooltip)
    {
        var button = new ToggleButton
        {
            Content = Fluent.Icon(glyph),
            Padding = new Thickness(Fluent.Space1),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(Fluent.ControlRadius),
            MinWidth = 0,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var clear = FluentLayout.Themed("SubtleFillColorTransparentBrush", Colors.Transparent);
        var hover = FluentLayout.Themed("SubtleFillColorSecondaryBrush", Colors.Gray);
        var pressed = FluentLayout.Themed("SubtleFillColorTertiaryBrush", Colors.DimGray);
        var accent = FluentLayout.Themed("AccentFillColorDefaultBrush", Colors.DodgerBlue);
        var accentHover = FluentLayout.Themed("AccentFillColorSecondaryBrush", Colors.DodgerBlue);

        button.Resources["ToggleButtonBackground"] = clear;
        button.Resources["ToggleButtonBackgroundPointerOver"] = hover;
        button.Resources["ToggleButtonBackgroundPressed"] = pressed;
        button.Resources["ToggleButtonBackgroundChecked"] = accent;
        button.Resources["ToggleButtonBackgroundCheckedPointerOver"] = accentHover;
        button.Resources["ToggleButtonBackgroundCheckedPressed"] = accentHover;
        button.Resources["ToggleButtonBorderBrush"] = clear;
        button.Resources["ToggleButtonBorderBrushPointerOver"] = clear;
        button.Resources["ToggleButtonBorderBrushPressed"] = clear;
        button.Resources["ToggleButtonBorderBrushChecked"] = accent;
        button.Resources["ToggleButtonBorderBrushCheckedPointerOver"] = accentHover;
        button.Resources["ToggleButtonBorderBrushCheckedPressed"] = accentHover;

        ToolTipService.SetToolTip(button, tooltip);
        return button;
    }

    /// <summary>
    /// Clearing throws away work that has no other copy, so it asks first.
    /// <para>
    /// A flyout rather than a <c>ContentDialog</c>: the widget surface is a
    /// <c>WS_EX_NOACTIVATE</c> window that never becomes foreground, and a flyout is the popup the
    /// card already opens its context menu with there. The undo stack survives a clear, but only
    /// until the app closes, so this is still the last word on a drawing.
    /// </para>
    /// </summary>
    private Flyout BuildClearConfirmation()
    {
        var message = Fluent.Text(FluentType.Body);
        message.Text = Str.WidgetsC_Scratchpad_ClearConfirm.Text();
        message.TextWrapping = TextWrapping.Wrap;
        message.TextTrimming = TextTrimming.None;

        var confirm = new Button { Content = Str.Common_Clear.Text() };
        var cancel = new Button { Content = Str.Common_Cancel.Text() };

        var flyout = new Flyout
        {
            Content = new StackPanel
            {
                Spacing = Fluent.Space3,
                MaxWidth = 240,
                Children =
                {
                    message,
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = Fluent.Space2,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Children = { confirm, cancel },
                    },
                },
            },
        };

        confirm.Click += (_, _) =>
        {
            flyout.Hide();
            ClearAll();
        };

        cancel.Click += (_, _) => flyout.Hide();

        return flyout;
    }

    // -----------------------------------------------------------------------------------------
    // Lifecycle
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// The pad's own commands — erase, undo, clear, full screen — in the card's header. Built in the
    /// constructor because the card reads this once, when it builds that header.
    /// </summary>
    public FrameworkElement? HeaderTrailing => _tools;

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
        ToolTipService.SetToolTip(_undoButton, Str.WidgetsC_Scratchpad_Undo.Text());
        ToolTipService.SetToolTip(_clearButton, Str.WidgetsC_Scratchpad_ClearDrawing.Text());
        ToolTipService.SetToolTip(_fullButton, FullScreenTip());
        _clearButton.Flyout = BuildClearConfirmation();
        _hintText.Text = Str.Scratchpad_DrawHere.Text();
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

        // [S-1499] Full screen always keeps its tools: they hold the way back.
        _tools.Visibility = _config.ShowTools || IsFullScreen ? Visibility.Visible : Visibility.Collapsed;

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

        _eraser.Checked -= OnEraserToggled;
        _eraser.Unchecked -= OnEraserToggled;
        _undoButton.Click -= OnUndoClicked;
        _fullButton.Click -= OnFullScreenClicked;

        _pad.PointerPressed -= OnPointerPressed;
        _pad.PointerMoved -= OnPointerMoved;
        _pad.PointerReleased -= OnPointerReleased;
        _pad.PointerCaptureLost -= OnPointerCaptureLost;
        _pad.PointerCanceled -= OnPointerCaptureLost;

        _pad.SizeChanged -= OnPadSizeChanged;
        DetachBar();
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
    private bool Erasing => _eraser.IsChecked == true || _penEraser;

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

    private void OnUndoClicked(object sender, RoutedEventArgs args) => Undo();

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

    private void OnEraserToggled(object sender, RoutedEventArgs args)
    {
        // Switching tool mid-gesture would otherwise finish a pen stroke as an erase.
        AbandonStroke();

        // [S-1498] The bar's eraser and the header's are one tool.
        _barEraser.IsChecked = _eraser.IsChecked;
        UpdateCommands();
    }

    private void UpdateCommands()
    {
        _undoButton.IsEnabled = _undo.Count > 0;
        _clearButton.IsEnabled = _config.Strokes.Count > 0;
        var eraserTip = _eraser.IsChecked == true ? Str.WidgetsC_Scratchpad_Draw.Text() : Str.WidgetsC_Scratchpad_EraseStrokes.Text();
        ToolTipService.SetToolTip(_eraser, eraserTip);
        ToolTipService.SetToolTip(_barEraser, eraserTip);
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
    /// [S-651] The tools ride on the card's header, beside its icon and title, so a narrow tile can
    /// only hold as many as leave the title a few letters. Three 36px buttons on a 120px tile ran
    /// past the card edge and pushed the title out entirely. They go in reverse order of use:
    /// the eraser first, then clear, then full screen; undo stays, because it is the one that
    /// rescues a slip. [S-1498] Where the ink bar shows, the eraser stands in it instead
    /// (<see cref="LayBar"/>).
    /// </summary>
    private void ShedTools(double width)
    {
        if (!(width > 0) || IsFullScreen)
        {
            return;
        }

        // The header's insets and icon, and enough title to say what the widget is.
        const double Taken = (Fluent.Space4 * 2) + Fluent.IconSize + Fluent.Space2 + Fluent.Space6 * 2;
        const double PerTool = Fluent.Space6 + Fluent.Space3 + Fluent.Space1;

        var room = (int)Math.Floor((width - Taken) / PerTool);
        var fit = Math.Clamp(room, 1, 4);

        _fullButton.Visibility = fit >= 2 ? Visibility.Visible : Visibility.Collapsed;
        _clearButton.Visibility = fit >= 3 ? Visibility.Visible : Visibility.Collapsed;
        _eraserFits = fit >= 4;
    }

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
    /// The ink, as a brush. This is the one literal colour the widget draws with, and the right
    /// one: it is what was drawn. A colour that no longer parses falls back to the default pen
    /// rather than reaching a brush as nothing.
    /// </summary>
    private static SolidColorBrush InkBrush(string? hex)
    {
        var normalised = HexColor.Normalise(hex);

        if (normalised.Length == 0 || !HexColor.TryParse(normalised, out var r, out var g, out var b))
        {
            HexColor.TryParse(ScratchpadConfig.DefaultPenColor, out r, out g, out b);
        }

        return new SolidColorBrush(Windows.UI.Color.FromArgb(255, r, g, b));
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
