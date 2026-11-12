using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;

namespace Themia.App.Widgets;

/// <summary>
/// [S-1910] One power action as a whole widget: shut down, restart, sleep, hibernate, lock, sign out
/// or turn off the display. One view for all seven, parameterised by the action its kind maps to.
/// <para>
/// <b>The tile is the button</b> ([S-1911]): the family's Tile block, ringed ([S-1912], [S-6736]): the
/// glyph centred inside its arm ring's room with the name under it, and the item layer's hover and
/// pressed fills across the whole card. It is
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
/// <b>Styles</b> ([S-1919]): the Tile block's tile or round look, as the toggles'. Only the drawing
/// differs; the whole slot stays the hit target, so arming, edit mode and the click are one path.
/// </para>
/// </summary>
public sealed partial class PowerButtonView : Grid, IWidgetView, IWidgetSizeAware, IWidgetSurfaceAware, IWidgetTopEdge
{
    /// <summary>[S-5700] It owns its top edge: the button is centred in its own inset.</summary>
    public bool OwnsTopEdge => true;

    private const string Scope = "power";

    private readonly PowerAction _action;
    private readonly TileBlock _tile = new(TileLook.Tile, ringed: true) { Padding = Family.Inset };

    /// <summary>
    /// [S-1913] Fires once, when the arm window runs out, to draw the widget disarmed. Cosmetic like
    /// the ring: whether a click runs the action is the press time's (<see cref="PowerButton"/>).
    /// </summary>
    private readonly DispatcherQueueTimer? _expiry;

    private IWidgetSurface? _surface;
    private PowerButton? _button;
    private ReadoutSize _size = ReadoutSizes.Default;
    private bool _hover;
    private bool _pressed;
    private bool _detached = true;

    /// <summary>[S-6913] What the view asks of the host: what runs the action.</summary>
    private readonly IWidgetHost _widgetHost;

    public PowerButtonView(WidgetId kind, IWidgetHost host)
    {
        _widgetHost = host;
        _action = PowerActionRules.ForKind(kind) ?? PowerAction.Lock;

        if (PowerActionRules.ForKind(kind) is null)
        {
            // Unreachable through the catalogue; if it ever happens, the least harmful action.
            Log.Error(Scope, $"{kind} is not a power widget; showing Lock");
        }

        Children.Add(_tile);

        // The whole slot is hit-testable, edge to edge: the tile is the button.
        Background = Family.Brush(ColourRole.Clear);

        _expiry = DispatcherQueue.GetForCurrentThread()?.CreateTimer();
        if (_expiry is not null)
        {
            _expiry.IsRepeating = false;
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

        // [S-1251] The host's backend: inert unless the card is a real widget on the desktop.
        _button = new PowerButton(_action, _widgetHost.Power);

        // [S-1881] Subscribed here, not in the constructor: a handler roots the view from the native
        // side, and a view parked under the Set up button is built but never attached or detached.
        PointerEntered += OnPointerEntered;
        PointerExited += OnPointerExited;
        PointerPressed += OnPointerPressed;
        PointerReleased += OnPointerReleased;
        PointerCaptureLost += OnPointerLost;
        PointerCanceled += OnPointerLost;
        Strings.Changed += OnLanguage;

        if (_expiry is not null)
        {
            _expiry.Tick += OnExpiry;
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
        var style = config is PowerButtonConfig { Style: var chosen } && Enum.IsDefined(chosen) ? chosen : PowerButtonStyle.Tile;
        _tile.Look = style == PowerButtonStyle.Round ? TileLook.Round : TileLook.Tile;
        _size = config is PowerButtonConfig sized ? ReadoutSizes.Normalise(sized.Size) : ReadoutSizes.Default;

        _expiry?.Stop();
        _button?.Disarm();
        Lay();
    }

    /// <summary>[S-11307] The card is fixed by the Size and the look: the box changes nothing.</summary>
    public void ApplySize(WidgetArea area)
    {
    }

    /// <summary>[S-11307] Its one option past its look, under the Style: its Size.</summary>
    public static void MenuLines(WidgetMenuLines menu, WidgetConfig config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        if (config is PowerButtonConfig button)
        {
            menu.StyleLines.Add(KitMenuLines.ReadoutSize(menu, button));
        }
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

        if (_expiry is not null)
        {
            _expiry.Stop();
            _expiry.Tick -= OnExpiry;
        }

        _button?.Disarm();
        _tile.Ring.Stop();
    }

    // ---- layout ------------------------------------------------------------------------------

    /// <summary>
    /// [S-1912] [S-11307] The Tile block in the card's inset, ringed: the glyph on the step its Size names
    /// inside its ring's room, the name under it; no state line. The armed sentence and a failure take the name's line, so arming
    /// moves nothing.
    /// </summary>
    private void Lay()
    {
        if (_detached)
        {
            return;
        }

        _tile.Use(TileRules.GlyphAt(_size), TileRules.SideAt(_size) - (2 * FamilySpace.Inset), state: false);
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
        var failure = button.Available && !armed ? button.Failure : null;
        var name = PowerActionRules.Label(_action);

        // [S-1913] The tile says what the next click does; [S-1915] after a failure, why (the
        // machine is still up), until the next press; [S-1914] greyed with the reason on hover.
        var (glyph, text, tooltip) = !button.Available
            ? (Glyph(_action), name, PowerActionRules.UnavailableHint)
            : armed
                ? (Glyph(_action), PowerActionRules.ArmedLabel(_action), PowerActionRules.ArmedLabel(_action))
                : failure is not null
                    ? (Glyphs.Warning, failure, failure)
                    : (Glyph(_action), name, name);

        _tile.Show(glyph, text, string.Empty, PowerActionRules.Tile(button.Available, armed, failure is not null));
        _tile.Face(!button.Available ? ItemState.Rest : _pressed ? ItemState.Pressed : _hover ? ItemState.Hover : ItemState.Rest);

        // [S-1913] The ring starts draining when the widget arms and drains on its own (the
        // compositor's): a repaint while armed (a hover) leaves it be.
        if (!armed)
        {
            _tile.Ring.Stop();
        }
        else if (!_tile.Ring.Draining)
        {
            _tile.Ring.Drain(button.ArmRemaining(now), button.ArmLeft(now));
        }

        ToolTipService.SetToolTip(this, tooltip);
        AutomationProperties.SetHelpText(this, tooltip);
        AutomationProperties.SetItemStatus(this, armed ? Str.WidgetsC_Power_ArmedStatus.Text() : string.Empty);
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
            ScheduleExpiry(button);
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

    /// <summary>[S-1913] Armed: repaint once, when the window runs out. Not armed: nothing to wait for.</summary>
    private void ScheduleExpiry(PowerButton button)
    {
        if (_expiry is null)
        {
            return;
        }

        _expiry.Stop();
        var left = button.ArmLeft(DateTimeOffset.UtcNow);

        if (left > TimeSpan.Zero)
        {
            _expiry.Interval = left;
            _expiry.Start();
        }
    }

    private void OnExpiry(DispatcherQueueTimer sender, object args)
    {
        if (_detached || _button is not { } button)
        {
            return;
        }

        // A timer may fire a hair early: still armed, it waits out the rest.
        if (button.IsArmed(DateTimeOffset.UtcNow))
        {
            ScheduleExpiry(button);
            return;
        }

        button.Disarm();
        Paint();
    }

    private void Disarm()
    {
        _expiry?.Stop();
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
