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
/// [S-3900] One quick setting as a whole widget, a Windows 11 quick-settings tile: Wi-Fi, Bluetooth,
/// airplane mode, energy saver, night light, rotation lock, do not disturb, sound, microphone, spatial
/// sound, mono audio, color filters, game mode. One view for all thirteen, parameterised by the toggle
/// its kind maps to.
/// <para>
/// <b>The tile is the switch</b> ([S-3904]): accent-filled while on, the Windows 11 subtle fills on hover
/// and press while off, dimmed with the reason in the tooltip where it cannot be switched. As with the
/// power widgets it is not a <see cref="Button"/>: in edit mode the press belongs to the card. The state
/// comes from one shared source per toggle (<see cref="IWidgetHost.Toggles"/>, the host's) that follows Windows' own
/// notifications; the clicks go through <see cref="ToggleGate"/> ([S-3905]). A debug render never
/// touches the machine ([S-1251]): it shows a made-up reading.
/// </para>
/// <para>
/// <b>Styles</b> ([S-3916]): the family's Tile block in its three looks (tile, round, switch,
/// [S-6715]). Only the drawing differs; the whole slot stays the hit target in every style, so the
/// click gate, the keys and edit mode are one path.
/// </para>
/// </summary>
public sealed partial class QuickToggleView : Grid, IWidgetView, IWidgetSizeAware, IWidgetSurfaceAware, IWidgetKeys, IWidgetTopEdge
{
    /// <summary>[S-5700] It owns its top edge: the whole tile is the switch, padded by itself.</summary>
    public bool OwnsTopEdge => true;

    private const string Scope = "toggles";

    private readonly QuickToggle _toggle;
    private readonly IWidgetHost _host;
    private readonly TileBlock _tile = new() { Padding = Family.Inset };
    private readonly ToggleGate _switch = new();

    private IWidgetSurface? _surface;
    private IToggleSource? _lease;
    private ToggleReading _preview = ToggleReading.Of(false);
    private ReadoutSize _size = ReadoutSizes.Default;
    private bool _hover;
    private bool _pressed;
    private bool _detached = true;

    public QuickToggleView(WidgetId kind, IWidgetHost host)
    {
        _host = host;
        _toggle = QuickToggleRules.ForKind(kind) ?? QuickToggle.Wifi;
        if (QuickToggleRules.ForKind(kind) is null)
        {
            // Unreachable through the catalogue.
            Log.Error(Scope, $"{kind} is not a quick-setting toggle; showing Wi-Fi");
        }

        Children.Add(_tile);

        // The whole slot is hit-testable, edge to edge: the tile is the switch.
        Background = Family.Brush(ColourRole.Clear);
    }

    /// <summary>[S-3902] The icon of each toggle, shared with the picker; sound and microphone are struck through while muted.</summary>
    public static string Glyph(QuickToggle toggle, bool on = true) => toggle switch
    {
        QuickToggle.Wifi => Glyphs.Wifi,
        QuickToggle.Bluetooth => Glyphs.Bluetooth,
        QuickToggle.AirplaneMode => Glyphs.Airplane,
        QuickToggle.EnergySaver => Glyphs.EnergySaver,
        QuickToggle.NightLight => Glyphs.NightLight,
        QuickToggle.RotationLock => Glyphs.RotationLock,
        QuickToggle.DoNotDisturb => Glyphs.QuietHours,
        QuickToggle.Sound => on ? Glyphs.Volume3 : Glyphs.Mute,
        QuickToggle.Microphone => on ? Glyphs.Microphone : Glyphs.MicrophoneOff,
        QuickToggle.SpatialSound => Glyphs.SpatialSound,
        QuickToggle.MonoAudio => Glyphs.MonoAudio,
        QuickToggle.ColorFilters => Glyphs.ColorFilters,
        _ => Glyphs.GameMode,
    };

    /// <inheritdoc />
    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _detached = false;

        // [S-1251] A debug render reads nothing and switches nothing.
        if (_surface is not { IsPreview: true })
        {
            _lease = _host.Toggles.Acquire(_toggle, OnChanged);
        }

        // [S-1881] Subscribed here, not in the constructor.
        PointerEntered += OnPointerEntered;
        PointerExited += OnPointerExited;
        PointerPressed += OnPointerPressed;
        PointerReleased += OnPointerReleased;
        PointerCaptureLost += OnPointerLost;
        PointerCanceled += OnPointerLost;
        Strings.Changed += OnLanguage;

        AutomationProperties.SetName(this, QuickToggleRules.Name(_toggle));
        ApplyConfig(config);
    }

    /// <summary>[S-3916] The style is the only setting: the Tile block's look.</summary>
    public void ApplyConfig(WidgetConfig config)
    {
        var style = config is QuickToggleConfig { Style: var chosen } && Enum.IsDefined(chosen) ? chosen : QuickToggleStyle.Tile;
        _tile.Look = style switch
        {
            QuickToggleStyle.Round => TileLook.Round,
            QuickToggleStyle.Switch => TileLook.Switch,
            _ => TileLook.Tile,
        };
        _size = config is QuickToggleConfig sized ? ReadoutSizes.Normalise(sized.Size) : ReadoutSizes.Default;

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
        if (config is QuickToggleConfig toggle)
        {
            menu.StyleLines.Add(KitMenuLines.ReadoutSize(menu, toggle));
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

        _lease?.Dispose();
        _lease = null;
    }

    private ToggleReading Reading => _lease?.Reading ?? _preview;

    // ---- layout ------------------------------------------------------------------------------

    /// <summary>
    /// [S-3906] [S-11307] The Tile block in the card's inset: the glyph on the step its Size names above
    /// the name and the state line, both planned at every size so switching moves nothing.
    /// </summary>
    private void Lay()
    {
        if (_detached)
        {
            return;
        }

        var width = _tile.Look == TileLook.Switch ? TileRules.SwitchWidthAt(_size) : TileRules.SideAt(_size);
        _tile.Use(TileRules.GlyphAt(_size), width - (2 * FamilySpace.Inset));
        Paint();
    }

    /// <summary>The whole look from the reading, the switch on its way and the pointer.</summary>
    private void Paint()
    {
        if (_detached)
        {
            return;
        }

        var locale = Strings.Current;
        var reading = Reading;
        var look = _switch.Look(reading);
        var live = look is ToggleLook.On or ToggleLook.Off;

        _tile.Show(Glyph(_toggle, on: look != ToggleLook.Off), QuickToggleRules.Name(locale, _toggle), QuickToggleRules.StateText(locale, look), QuickToggleRules.Tile(look));
        _tile.Face(!live ? ItemState.Rest : _pressed ? ItemState.Pressed : _hover ? ItemState.Hover : ItemState.Rest);

        var tooltip = QuickToggleRules.Tooltip(locale, _toggle, reading, look, _switch.Refused);
        ToolTipService.SetToolTip(this, tooltip);
        AutomationProperties.SetHelpText(this, tooltip);
        AutomationProperties.SetItemStatus(this, QuickToggleRules.StateText(locale, look));
    }

    // ---- input -------------------------------------------------------------------------------

    /// <summary>In edit mode the card owns the press: the widget is selected, never switched.</summary>
    private bool Inert => _detached || _surface?.EditMode == true;

    /// <summary>[S-3905] Space or Enter switches it while the widget holds the keyboard.</summary>
    public bool HandleKey(WidgetKey key)
    {
        if (key is not (WidgetKey.Space or WidgetKey.Enter) || Inert)
        {
            return false;
        }

        _ = PressAsync();
        return true;
    }

    /// <summary>Only a toggle that can be switched takes the keyboard from the app in front.</summary>
    public bool WantsKeys => !Inert && Reading.CanSwitch;

    private void OnPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        // What Windows announces nowhere (a dock, a policy) shows the next time the pointer comes by.
        _lease?.Refresh();
        _hover = !Inert && Reading.CanSwitch;
        Paint();
    }

    private void OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        _hover = false;
        _pressed = false;
        Paint();
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (Inert || !Reading.CanSwitch || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
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

    /// <summary>One click: <see cref="ToggleGate"/> decides, the shared source switches, this repaints.</summary>
    private async Task PressAsync()
    {
        if (_switch.Press(Reading, DateTimeOffset.UtcNow) is not { } on)
        {
            return;
        }

        Paint();

        bool done;
        try
        {
            if (_lease is { } lease)
            {
                done = await lease.SetAsync(on).ConfigureAwait(true);
            }
            else
            {
                // A debug render: the made-up reading flips, nothing else does.
                _preview = ToggleReading.Of(on);
                done = true;
            }
        }
        catch (Exception ex)
        {
            Log.Error(Scope, $"{_toggle} could not be switched", ex);
            done = false;
        }

        _switch.Settled(done);
        if (!done)
        {
            Log.Warn(Scope, $"Windows did not switch {_toggle} {(on ? "on" : "off")}");
        }

        Paint();
    }

    /// <summary>The shared source moved; on this view's thread.</summary>
    private void OnChanged() => Paint();

    /// <summary>[S-1110] The name and the state again, in the new language.</summary>
    private void OnLanguage(Locale locale)
    {
        AutomationProperties.SetName(this, QuickToggleRules.Name(_toggle));
        Lay();
    }
}
