using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Services.Toggles;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;

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
/// comes from one shared source per toggle (<see cref="ToggleHub"/>) that follows Windows' own
/// notifications; the clicks go through <see cref="ToggleGate"/> ([S-3905]). A debug render never
/// touches the machine ([S-1251]): it shows a made-up reading.
/// </para>
/// <para>
/// <b>Styles</b> ([S-3916]): the same switch drawn as the tile, a round button or a Settings-style
/// switch. Only the drawing differs; the whole slot stays the hit target in every style, so the
/// click gate, the keys and edit mode are one path.
/// </para>
/// </summary>
internal sealed partial class QuickToggleView : Grid, IWidgetView, IWidgetSizeAware, IWidgetSurfaceAware, IWidgetKeys
{
    private const string Scope = "toggles";

    private readonly QuickToggle _toggle;
    private readonly StackPanel _face = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly StackPanel _words = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly FontIcon _icon;
    private readonly TextBlock _name = Fluent.Text(FluentType.Body);
    private readonly TextBlock _state = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
    private readonly RoundButtonPart _round;
    private readonly ToggleGate _switch = new();

    private readonly Brush _rest = new SolidColorBrush(Colors.Transparent);
    private readonly Brush _accent = FluentLayout.Themed("AccentFillColorDefaultBrush", Colors.DodgerBlue);
    private readonly Brush _accentHover = FluentLayout.Themed("AccentFillColorSecondaryBrush", Colors.DodgerBlue);
    private readonly Brush _accentPressed = FluentLayout.Themed("AccentFillColorTertiaryBrush", Colors.DodgerBlue);
    private readonly Brush _onAccent = FluentLayout.Themed("TextOnAccentFillColorPrimaryBrush", Colors.White);
    private readonly Brush _onAccentSecondary = FluentLayout.Themed("TextOnAccentFillColorSecondaryBrush", Colors.White);
    private readonly Brush _primary = FluentLayout.Themed("TextFillColorPrimaryBrush", Colors.White);
    private readonly Brush _secondary = FluentLayout.Themed("TextFillColorSecondaryBrush", Colors.Gray);
    private readonly Brush _disabled = FluentLayout.Themed("TextFillColorDisabledBrush", Colors.Gray);
    private readonly Brush _controlEdge = FluentLayout.Themed("ControlStrokeColorDefaultBrush", Colors.Gray);

    private IWidgetSurface? _surface;
    private ToggleLease? _lease;
    private ToggleReading _preview = ToggleReading.Of(false);
    private QuickToggleStyle _style = QuickToggleStyle.Tile;
    private WidgetArea _area;
    private bool _hover;
    private bool _pressed;
    private bool _detached = true;

    public QuickToggleView(WidgetId kind)
    {
        _toggle = QuickToggleRules.ForKind(kind) ?? QuickToggle.Wifi;
        if (QuickToggleRules.ForKind(kind) is null)
        {
            // Unreachable through the catalogue.
            Log.Error(Scope, $"{kind} is not a quick-setting toggle; showing Wi-Fi");
        }

        _icon = Fluent.Icon(Glyph(_toggle, on: true), "TextFillColorPrimaryBrush", 32);
        _icon.HorizontalAlignment = HorizontalAlignment.Center;
        _icon.VerticalAlignment = VerticalAlignment.Center;

        _name.TextTrimming = TextTrimming.CharacterEllipsis;
        _name.TextWrapping = TextWrapping.NoWrap;
        _name.MaxLines = 1;
        _state.TextTrimming = TextTrimming.CharacterEllipsis;
        _state.MaxLines = 1;

        _words.Children.Add(_name);
        _words.Children.Add(_state);
        _face.Children.Add(_icon);
        _face.Children.Add(_words);
        _round = new RoundButtonPart(Glyph(_toggle, on: true));
        BuildSwitch();
        Children.Add(_face);

        // The whole slot is hit-testable, edge to edge: the tile is the switch.
        Background = _rest;
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
            _lease = ToggleHub.Acquire(_toggle, OnChanged);
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

    /// <summary>[S-3916] The style is the only setting: the face of that style replaces the last one.</summary>
    public void ApplyConfig(WidgetConfig config)
    {
        _style = config is QuickToggleConfig { Style: var style } && Enum.IsDefined(style) ? style : QuickToggleStyle.Tile;

        UIElement face = _style switch
        {
            QuickToggleStyle.Round => _round,
            QuickToggleStyle.Switch => _switchFace,
            _ => _face,
        };

        if (Children.Count != 1 || Children[0] != face)
        {
            Children.Clear();
            Children.Add(face);
        }

        Lay();
    }

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

        _lease?.Dispose();
        _lease = null;
    }

    private ToggleReading Reading => _lease?.Reading ?? _preview;

    // ---- layout ------------------------------------------------------------------------------

    /// <summary>The style's ladder over the box inside the inset.</summary>
    private void Lay()
    {
        if (_detached)
        {
            return;
        }

        // [S-3916] One inset for every style: the card's standard one, tighter while Compact.
        var inset = ButtonInset.For(_area);
        Padding = new Thickness(inset);

        var width = _area.Width - (2 * inset);
        var height = _area.Height - (2 * inset);
        var nameChars = QuickToggleRules.Name(_toggle).Length;

        switch (_style)
        {
            case QuickToggleStyle.Round:
                _round.Lay(RoundButtonFace.Plan(_area, width, height, nameChars), width);
                break;

            case QuickToggleStyle.Switch:
                LaySwitch(SwitchFace.Plan(width, height, nameChars, QuickToggleRules.LongestStateChars(Strings.Current)));
                break;

            default:
                LayTile(width, height, nameChars);
                break;
        }

        Paint();
    }

    /// <summary>[S-3906] Icon alone at XS, then the name, then the state; side by side in a strip.</summary>
    private void LayTile(double width, double height, int nameChars)
    {
        var plan = ToggleFace.Plan(_area, width, height, nameChars, QuickToggleRules.LongestStateChars(Strings.Current));

        _icon.FontSize = plan.IconSize;
        _face.Orientation = plan.Row ? Orientation.Horizontal : Orientation.Vertical;
        _face.Spacing = plan.Name is null ? 0 : ToggleFace.Gap;
        _words.Spacing = plan.Row ? 2 : ToggleFace.Gap;
        _words.HorizontalAlignment = plan.Row ? HorizontalAlignment.Left : HorizontalAlignment.Center;

        var align = plan.Row ? TextAlignment.Left : TextAlignment.Center;
        var room = Math.Max(0, plan.Row ? width - plan.IconSize - ToggleFace.Gap : width);

        if (plan.Name is { } name)
        {
            _words.Visibility = Visibility.Visible;
            _name.Apply(name);
            _name.TextAlignment = align;
            _name.HorizontalAlignment = plan.Row ? HorizontalAlignment.Left : HorizontalAlignment.Center;
            _name.MaxWidth = room;
        }
        else
        {
            _words.Visibility = Visibility.Collapsed;
        }

        if (plan.State is { } state)
        {
            _state.Visibility = Visibility.Visible;
            _state.Apply(state);
            _state.TextAlignment = align;
            _state.HorizontalAlignment = plan.Row ? HorizontalAlignment.Left : HorizontalAlignment.Center;
            _state.MaxWidth = room;
        }
        else
        {
            _state.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>The whole look from the reading, the switch on its way, the pointer and the style.</summary>
    private void Paint()
    {
        if (_detached)
        {
            return;
        }

        var locale = Strings.Current;
        var reading = Reading;
        var look = _switch.Look(reading);

        switch (_style)
        {
            case QuickToggleStyle.Round:
                PaintRound(locale, look);
                break;

            case QuickToggleStyle.Switch:
                PaintSwitch(locale, look);
                break;

            default:
                PaintTile(locale, look);
                break;
        }

        var tooltip = QuickToggleRules.Tooltip(locale, _toggle, reading, look, _switch.Refused);
        ToolTipService.SetToolTip(this, tooltip);
        AutomationProperties.SetHelpText(this, tooltip);
        AutomationProperties.SetItemStatus(this, QuickToggleRules.StateText(locale, look));
    }

    /// <summary>[S-3904] The tile: accent-filled while on, subtle fills while off, dimmed where unavailable.</summary>
    private void PaintTile(Locale locale, ToggleLook look)
    {
        Brush? fill;
        var face = ItemState.Rest;
        Brush iconBrush;
        Brush nameBrush;
        Brush stateBrush;

        switch (look)
        {
            case ToggleLook.On:
                fill = _pressed ? _accentPressed : _hover ? _accentHover : _accent;
                iconBrush = _onAccent;
                nameBrush = _onAccent;
                stateBrush = _onAccentSecondary;
                break;

            case ToggleLook.Unavailable:
                fill = null;
                iconBrush = _disabled;
                nameBrush = _disabled;
                stateBrush = _disabled;
                break;

            case ToggleLook.Unknown:
                fill = null;
                iconBrush = _secondary;
                nameBrush = _secondary;
                stateBrush = _secondary;
                break;

            default:
                fill = null;
                face = ItemFace();
                iconBrush = _pressed ? _secondary : _primary;
                nameBrush = _pressed ? _secondary : _primary;
                stateBrush = _secondary;
                break;
        }

        // [S-6513] Off, the tile is an item of the card's item layer; on, it is the accent's.
        if (fill is null)
        {
            ItemFill.Paint(this, ItemRole.Row, face);
        }
        else
        {
            ItemFill.Release(this);
            Background = fill;
        }

        _icon.Glyph = Glyph(_toggle, on: look != ToggleLook.Off);
        _icon.Foreground = iconBrush;
        _name.Text = QuickToggleRules.Name(locale, _toggle);
        _name.Foreground = nameBrush;
        _state.Text = QuickToggleRules.StateText(locale, look);
        _state.Foreground = stateBrush;
    }

    /// <summary>
    /// [S-3917] The round button: the circle accent-filled while on, a control fill while off (its
    /// hover and pressed steps on the circle, not the tile), the disabled fill where unavailable.
    /// </summary>
    private void PaintRound(Locale locale, ToggleLook look)
    {
        // [S-6546] The round look draws no tile: only its circle is an item.
        ItemFill.Release(this);
        Background = _rest;
        var glyph = Glyph(_toggle, on: look != ToggleLook.Off);
        var name = QuickToggleRules.Name(locale, _toggle);

        switch (look)
        {
            case ToggleLook.On:
                var accent = _pressed ? _accentPressed : _hover ? _accentHover : _accent;
                _round.Paint(accent, accent, glyph, _onAccent, name, _primary);
                break;

            case ToggleLook.Unavailable:
                _round.Paint(ItemState.Disabled, _controlEdge, glyph, _disabled, name, _disabled);
                break;

            case ToggleLook.Unknown:
                _round.Paint(ItemState.Rest, _controlEdge, glyph, _secondary, name, _secondary);
                break;

            default:
                _round.Paint(ItemFace(), _controlEdge, glyph, _pressed ? _secondary : _primary, name, _primary);
                break;
        }
    }

    /// <summary>[S-6513] The tile's item state from the pointer: pressed, under it, or at rest.</summary>
    private ItemState ItemFace() => _pressed ? ItemState.Pressed : _hover ? ItemState.Hover : ItemState.Rest;

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
