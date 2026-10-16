using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-1910] One power action as a whole widget: shut down, restart, sleep, hibernate, lock, sign out
/// or turn off the display. One view for all seven, parameterised by the action its kind maps to.
/// <para>
/// <b>The tile is the button</b> ([S-1911]): an icon centred with the name underneath where there is
/// room ([S-1912]), and the Windows 11 button's hover and pressed fills across the whole card. It is
/// not a <see cref="Button"/>: a button takes the press for itself, and in edit mode the press must
/// reach the card so the widget is selected and moved, never run.
/// </para>
/// <para>
/// <b>Safety</b> lives in <see cref="PowerButton"/> ([S-1913]): shut down, restart, sign out and
/// hibernate arm on the first click ("Click again to shut down", a ring draining over five seconds)
/// and run on the second. The machine is touched only through <see cref="IPowerBackend"/>, which is
/// inert until the card says this is a real widget on the desktop ([S-1251]).
/// </para>
/// <para>
/// <b>Styles</b> ([S-1919]): the tile, or a round accent button (the part the toggles' Round style
/// uses too). Only the drawing differs; the whole slot stays the hit target, so arming, edit mode
/// and the click are one path.
/// </para>
/// </summary>
internal sealed partial class PowerButtonView : Grid, IWidgetView, IWidgetSizeAware, IWidgetSurfaceAware
{
    private const string Scope = "power";

    /// <summary>How often the arm ring is redrawn while it drains. Cosmetic: safety reads timestamps.</summary>
    private static readonly TimeSpan RingFrame = TimeSpan.FromMilliseconds(50);

    private readonly PowerAction _action;
    private readonly Grid _face = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly Grid _iconBox = new() { HorizontalAlignment = HorizontalAlignment.Center };
    private readonly FontIcon _icon;
    private readonly ProgressRing _ring = ArmRing();
    private readonly ProgressRing _roundRing = ArmRing();
    private readonly RoundButtonPart _round;

    private readonly TextBlock _label = Fluent.Text(FluentType.Body);
    private readonly DispatcherQueueTimer? _ringTimer;

    private readonly Brush _rest = new SolidColorBrush(Colors.Transparent);
    private readonly Brush _hoverFill = FluentLayout.Themed("SubtleFillColorSecondaryBrush", Colors.Gray);
    private readonly Brush _pressedFill = FluentLayout.Themed("SubtleFillColorTertiaryBrush", Colors.DimGray);
    private readonly Brush _armedFill = FluentLayout.Themed("SystemFillColorCriticalBackgroundBrush", Colors.MistyRose);
    private readonly Brush _critical = FluentLayout.Themed("SystemFillColorCriticalBrush", Colors.Red);
    private readonly Brush _primary = FluentLayout.Themed("TextFillColorPrimaryBrush", Colors.White);
    private readonly Brush _secondary = FluentLayout.Themed("TextFillColorSecondaryBrush", Colors.Gray);
    private readonly Brush _disabled = FluentLayout.Themed("TextFillColorDisabledBrush", Colors.Gray);
    private readonly Brush _accent = FluentLayout.Themed("AccentFillColorDefaultBrush", Colors.DodgerBlue);
    private readonly Brush _accentHover = FluentLayout.Themed("AccentFillColorSecondaryBrush", Colors.DodgerBlue);
    private readonly Brush _accentPressed = FluentLayout.Themed("AccentFillColorTertiaryBrush", Colors.DodgerBlue);
    private readonly Brush _accentDisabled = FluentLayout.Themed("AccentFillColorDisabledBrush", Colors.Gray);
    private readonly Brush _onAccent = FluentLayout.Themed("TextOnAccentFillColorPrimaryBrush", Colors.White);
    private readonly Brush _onAccentDisabled = FluentLayout.Themed("TextOnAccentFillColorDisabledBrush", Colors.White);
    private readonly Brush _control = FluentLayout.Themed("ControlFillColorDefaultBrush", Colors.Gray);
    private readonly Brush _controlEdge = FluentLayout.Themed("ControlStrokeColorDefaultBrush", Colors.Gray);

    private IWidgetSurface? _surface;
    private PowerButton? _button;
    private WidgetArea _area;
    private PowerFacePlan _plan;
    private PowerButtonStyle _style = PowerButtonStyle.Tile;
    private bool _hover;
    private bool _pressed;
    private bool _detached = true;

    public PowerButtonView(WidgetKind kind)
    {
        _action = PowerActionRules.ForKind(kind) ?? PowerAction.Lock;

        if (PowerActionRules.ForKind(kind) is null)
        {
            // Unreachable through the catalogue; if it ever happens, the least harmful action.
            Log.Error(Scope, $"{kind} is not a power widget; showing Lock");
        }

        _icon = Fluent.Icon(Glyph(_action), "TextFillColorPrimaryBrush", 32);
        _icon.HorizontalAlignment = HorizontalAlignment.Center;
        _iconBox.Children.Add(_ring);
        _iconBox.Children.Add(_icon);

        _label.HorizontalAlignment = HorizontalAlignment.Center;
        _label.TextAlignment = TextAlignment.Center;
        _label.VerticalAlignment = VerticalAlignment.Top;

        _face.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _face.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        SetRow(_label, 1);
        _face.Children.Add(_iconBox);
        _face.Children.Add(_label);
        Children.Add(_face);

        _round = new RoundButtonPart(Glyph(_action));
        _round.Overlay(_roundRing);

        // The whole slot is hit-testable, edge to edge: the tile is the button.
        Background = _rest;

        _ringTimer = DispatcherQueue.GetForCurrentThread()?.CreateTimer();
        if (_ringTimer is not null)
        {
            _ringTimer.IsRepeating = true;
            _ringTimer.Interval = RingFrame;
        }
        else
        {
            // ignore: a design-time host. PowerButton still expires on its own clock, so safety holds;
            // only the armed look lingers until the next press or pointer exit.
            Log.Warn(Scope, "no dispatcher queue; an armed widget will not repaint when it expires");
        }
    }

    /// <summary>[S-1910] The icon of each action, shared with the picker entry.</summary>
    public static string Glyph(PowerAction action) => action switch
    {
        PowerAction.Shutdown => Glyphs.Power,
        PowerAction.Restart => Glyphs.Reset,
        PowerAction.Sleep => Glyphs.Moon,
        PowerAction.Hibernate => Glyphs.Hibernate,
        PowerAction.Lock => Glyphs.Lock,
        PowerAction.SignOut => Glyphs.SignOut,
        _ => Glyphs.Display,
    };

    /// <inheritdoc />
    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _detached = false;

        // [S-1251] Inert unless the card is a real widget on the desktop.
        IPowerBackend backend = _surface is { IsPreview: false } ? Win32PowerActions.Instance : InertPowerActions.Instance;
        _button = new PowerButton(_action, backend);

        // [S-1881] Subscribed here, not in the constructor: a handler roots the view from the native
        // side, and a view parked under the Set up button is built but never attached or detached.
        PointerEntered += OnPointerEntered;
        PointerExited += OnPointerExited;
        PointerPressed += OnPointerPressed;
        PointerReleased += OnPointerReleased;
        PointerCaptureLost += OnPointerLost;
        PointerCanceled += OnPointerLost;
        Strings.Changed += OnLanguage;

        if (_ringTimer is not null)
        {
            _ringTimer.Tick += OnRingTick;
        }

        AutomationProperties.SetName(this, PowerActionRules.Label(_action));
        ApplyConfig(config);
    }

    /// <summary>
    /// [S-1919] The style is the only setting: the face of that style replaces the last one. A
    /// config change cannot leave the widget armed ([S-1913]).
    /// </summary>
    public void ApplyConfig(WidgetConfig config)
    {
        _style = config is PowerButtonConfig { Style: var style } && Enum.IsDefined(style) ? style : PowerButtonStyle.Tile;

        UIElement face = _style == PowerButtonStyle.Round ? _round : _face;
        if (Children.Count != 1 || Children[0] != face)
        {
            Children.Clear();
            Children.Add(face);
        }

        _ringTimer?.Stop();
        _button?.Disarm();
        Lay();
    }

    /// <summary>The draining ring of an armed widget, one per face.</summary>
    private static ProgressRing ArmRing() => new()
    {
        IsIndeterminate = false,
        Minimum = 0,
        Maximum = 100,
        IsActive = true,
        Visibility = Visibility.Collapsed,
        IsHitTestVisible = false,
    };

    /// <inheritdoc />
    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Lay();
    }

    public void Detach()
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        PointerEntered -= OnPointerEntered;
        PointerExited -= OnPointerExited;
        PointerPressed -= OnPointerPressed;
        PointerReleased -= OnPointerReleased;
        PointerCaptureLost -= OnPointerLost;
        PointerCanceled -= OnPointerLost;
        Strings.Changed -= OnLanguage;

        if (_ringTimer is not null)
        {
            _ringTimer.Stop();
            _ringTimer.Tick -= OnRingTick;
        }

        _button?.Disarm();
    }

    // ---- layout ------------------------------------------------------------------------------

    /// <summary>[S-1912] Icon alone at XS, icon and name above; both grow with the tile.</summary>
    private void Lay()
    {
        if (_detached)
        {
            return;
        }

        var compact = _area.WidthClass == SizeClass.Compact || _area.HeightClass == SizeClass.Compact;
        var inset = compact ? Fluent.Space2 : Fluent.Space3;
        Padding = new Thickness(inset);

        var name = PowerActionRules.Label(_action);
        var armedChars = PowerActionRules.NeedsArming(_action) ? PowerActionRules.ArmedLabel(_action).Length : 0;
        var width = _area.Width - (2 * inset);
        var height = _area.Height - (2 * inset);

        if (_style == PowerButtonStyle.Round)
        {
            _round.Lay(RoundButtonFace.Plan(_area, width, height, name.Length, armedChars), width);
            Paint();
            return;
        }

        _plan = PowerFace.Plan(_area, width, height, name.Length, armedChars);

        _iconBox.Width = _plan.IconBox;
        _iconBox.Height = _plan.IconBox;
        _ring.Width = _plan.IconBox;
        _ring.Height = _plan.IconBox;
        _icon.FontSize = _plan.IconSize;

        if (_plan.Label is null)
        {
            _label.Visibility = Visibility.Collapsed;
        }
        else
        {
            _label.Visibility = Visibility.Visible;
            _label.Margin = new Thickness(0, PowerFace.LabelGap, 0, 0);
            _label.Height = _plan.LabelSlot;
            _label.MaxWidth = Math.Max(0, width);
        }

        Paint();
    }

    /// <summary>The whole look from the state: idle, hover, pressed, armed, failed or unavailable.</summary>
    private void Paint()
    {
        if (_button is not { } button)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var armed = button.IsArmed(now);
        var name = PowerActionRules.Label(_action);

        string tooltip;
        Brush fill;
        Brush iconBrush;
        Brush labelBrush;
        var glyph = Glyph(_action);
        var text = name;

        if (!button.Available)
        {
            // [S-1914] Shown, but greyed and inert, with the reason on hover.
            tooltip = PowerActionRules.UnavailableHint;
            fill = _rest;
            iconBrush = _disabled;
            labelBrush = _disabled;
        }
        else if (armed)
        {
            // [S-1913] The tile says what the next click does, and the ring shows how long it waits.
            text = PowerActionRules.ArmedLabel(_action);
            tooltip = text;
            fill = _armedFill;
            iconBrush = _critical;
            labelBrush = _critical;
        }
        else if (button.Failure is { } failure)
        {
            // [S-1915] The machine is still up, so the tile says why, until the next press.
            glyph = Glyphs.Warning;
            text = failure;
            tooltip = failure;
            fill = _hover ? _hoverFill : _rest;
            iconBrush = _critical;
            labelBrush = _critical;
        }
        else
        {
            tooltip = name;
            fill = _pressed ? _pressedFill : _hover ? _hoverFill : _rest;
            iconBrush = _pressed ? _secondary : _primary;
            labelBrush = _pressed ? _secondary : _primary;
        }

        var ring = _style == PowerButtonStyle.Round ? _roundRing : _ring;
        ring.Visibility = armed ? Visibility.Visible : Visibility.Collapsed;
        ring.Foreground = _critical;
        ring.Value = button.ArmRemaining(now) * 100;

        if (_style == PowerButtonStyle.Round)
        {
            PaintRound(button, armed, glyph, text);
        }
        else
        {
            Background = fill;
            _icon.Glyph = glyph;
            _icon.Foreground = iconBrush;
            _label.Text = text;
            _label.Foreground = labelBrush;
            ApplyLabelStep(armed || button.Failure is not null);
        }

        ToolTipService.SetToolTip(this, tooltip);
        AutomationProperties.SetHelpText(this, tooltip);
        AutomationProperties.SetItemStatus(this, armed ? Str.WidgetsC_Power_ArmedStatus.Text() : string.Empty);
    }

    /// <summary>
    /// [S-1919] The round button: an accent circle (its hover and pressed steps on the circle, not the
    /// tile); armed, the critical background with the ring draining round its rim; failed, the
    /// warning glyph on a control fill; unavailable, the disabled accent.
    /// </summary>
    private void PaintRound(PowerButton button, bool armed, string glyph, string text)
    {
        Background = _rest;

        if (!button.Available)
        {
            _round.Paint(_accentDisabled, _accentDisabled, glyph, _onAccentDisabled, text, _disabled);
        }
        else if (armed)
        {
            _round.Paint(_armedFill, _critical, glyph, _critical, text, _critical, sentence: true);
        }
        else if (button.Failure is not null)
        {
            _round.Paint(_control, _controlEdge, glyph, _critical, text, _critical, sentence: true);
        }
        else
        {
            var fill = _pressed ? _accentPressed : _hover ? _accentHover : _accent;
            _round.Paint(fill, fill, glyph, _onAccent, text, _primary);
        }
    }

    /// <summary>The name on its step; the armed sentence (or a failure) on its own, wrapping to two lines if planned.</summary>
    private void ApplyLabelStep(bool sentence)
    {
        if (_plan.Label is not { } step)
        {
            return;
        }

        if (sentence)
        {
            _label.Apply(_plan.Armed);
            _label.MaxLines = _plan.ArmedLines;
            _label.TextWrapping = _plan.ArmedLines > 1 ? TextWrapping.WrapWholeWords : TextWrapping.NoWrap;
        }
        else
        {
            _label.Apply(step);
            _label.MaxLines = 1;
            _label.TextWrapping = TextWrapping.NoWrap;
        }
    }

    // ---- pointer -----------------------------------------------------------------------------

    /// <summary>[S-1911] In edit mode the card owns the press: the widget is selected, never run.</summary>
    private bool Inert => _detached || _surface?.EditMode == true || _button is not { Available: true };

    private void OnPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        // [S-1914] A capability change (powercfg /h on) shows the next time the pointer comes by.
        _button?.Refresh();
        _hover = !Inert;
        Paint();
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        _hover = false;
        _pressed = false;

        // [S-1913] An armed tile left under the cursor would defeat the second click on the next visit.
        Disarm();
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (Inert || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _pressed = CapturePointer(e.Pointer);
        e.Handled = true;
        Paint();
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_pressed)
        {
            return;
        }

        _pressed = false;
        ReleasePointerCapture(e.Pointer);
        e.Handled = true;

        // A press that slid off the tile before release is not a click, as with any button.
        var at = e.GetCurrentPoint(this).Position;
        var inside = at.X >= 0 && at.Y >= 0 && at.X <= ActualWidth && at.Y <= ActualHeight;

        if (inside && !Inert)
        {
            _ = PressAsync();
        }
        else
        {
            Paint();
        }
    }

    private void OnPointerLost(object sender, PointerRoutedEventArgs e)
    {
        _pressed = false;
        Paint();
    }

    /// <summary>One click: <see cref="PowerButton"/> decides, this only follows and repaints.</summary>
    private async Task PressAsync()
    {
        if (_button is not { } button)
        {
            return;
        }

        PowerPress result;

        try
        {
            var pending = button.PressAsync(DateTimeOffset.UtcNow);

            // Decided synchronously: armed, or running. Paint that before waiting on the machine.
            if (button.IsArmed(DateTimeOffset.UtcNow))
            {
                _ringTimer?.Start();
            }
            else
            {
                _ringTimer?.Stop();
            }

            Paint();
            result = await pending.ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Log.Error(Scope, $"{_action} could not be started", ex);
            return;
        }

        if (_detached)
        {
            return;
        }

        if (result == PowerPress.Ran && button.Failure is { } failure)
        {
            Log.Warn(Scope, failure);
        }

        Paint();
    }

    private void OnRingTick(DispatcherQueueTimer sender, object args)
    {
        if (_button is not { } button || !button.IsArmed(DateTimeOffset.UtcNow))
        {
            // The window is over whether or not the clock agrees to the millisecond.
            sender.Stop();
            _button?.Disarm();
        }

        Paint();
    }

    private void Disarm()
    {
        _ringTimer?.Stop();
        _button?.Disarm();
        Paint();
    }

    /// <summary>[S-1110] The name and the sentences again, in the new language.</summary>
    private void OnLanguage(Locale locale)
    {
        AutomationProperties.SetName(this, PowerActionRules.Label(_action));
        Lay();
    }
}
