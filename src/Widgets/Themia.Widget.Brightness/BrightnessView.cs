using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Themia.App.Services;
using Themia.App.Services.Brightness;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.Brightness;
using Themia.Sdk;

namespace Themia.App.Widgets;

/// <summary>
/// [S-4440] [S-17184] The display brightness as one row, as Windows' quick settings draw it (owner, 2026-11-23: "brightness
/// slider should be moved to the quick settings and should have a row design and not columnar design"): the sun, the level
/// (Body, held to "100%") and the Controls slider across what is left ([S-6716]: its rail the Track role, its value the
/// accent), the row centred in the card (<see cref="LevelRow"/>). No display names.
/// <para>
/// [S-4442] It sets every display whose brightness can be set, together, or with "Only the screen it's
/// on" the display its surface is on — one window per display, so the display is the window's. It shows
/// the level of its own display, or of the first that has one. The levels come from
/// the host's brightness service (<see cref="IBrightness"/>), which hears a built-in panel change and reads a monitor again when the
/// pointer comes by; while the thumb is held a reading never moves it.
/// </para>
/// </summary>
internal sealed partial class BrightnessView : Grid, IWidgetView, IWidgetOpens, IWidgetSurfaceAware, IWidgetKeys
{
    /// <inheritdoc />
    public CardOpen? Opens => BrightnessModule.Opens;

    private readonly Slider _slider;

    /// <summary>[S-17184] The level beside the sun, held to its widest case so the slider never moves.</summary>
    private readonly TextBlock _level;

    private readonly PointerEventHandler _grab;
    private readonly PointerEventHandler _release;
    private readonly PointerEventHandler _wheel;

    private BrightnessConfig _config = new();
    private IWidgetSurface? _surface;
    private readonly IBrightness _brightness;
    private IDisposable? _lease;
    private string? _own;
    private BrightnessSnapshot _preview = BrightnessSnapshot.Pending;
    private int _wheelCarry;
    private bool _echoing;
    private bool _grabbed;
    private bool _detached = true;

    public BrightnessView(IWidgetHost host)
    {
        _brightness = host.Brightness;

        // [S-17184] One row, centred between the card's top inset and the view's own at its foot, in the side insets: the sun,
        // 8, the level, 12, the slider across what is left.
        Padding = new Thickness(FamilySpace.Inset, 0, FamilySpace.Inset, FamilySpace.Inset);
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var sun = Family.Glyph(Glyphs.Brightness);
        sun.VerticalAlignment = VerticalAlignment.Center;
        sun.Margin = new Thickness(0, 0, FamilySpace.GlyphGap, 0);
        Children.Add(sun);

        _level = Family.Text(FamilyType.Body, ColourRole.TextPrimary);
        Family.Figures(_level);
        _level.MinWidth = LevelRow.LevelWidth;
        _level.VerticalAlignment = VerticalAlignment.Center;
        _level.Margin = new Thickness(0, 0, FamilySpace.S3, 0);
        SetColumn(_level, 1);
        Children.Add(_level);

        // [S-6716] The Controls slider: its rail the Track role, its value the accent, no focus.
        _slider = ControlsBlock.RailSlider(0, 100, 0);
        _slider.StepFrequency = 1;
        _slider.IsTabStop = false;
        _slider.ValueChanged += (_, args) => OnSliderChanged(args.NewValue);

        // The template's own height (32) is no taller than its thumb's ink here: the row is the thumb's height, never cut.
        _slider.Resources["SliderHorizontalHeight"] = ControlsRules.SliderThumb;
        SetColumn(_slider, 2);
        Children.Add(_slider);

        _grab = OnGrab;
        _release = OnRelease;
        _wheel = OnWheel;

        // The wheel anywhere on the row, as on the volume tile.
        Background = Family.Brush(ColourRole.Clear);
    }

    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _detached = false;

        // [S-1251] A debug render reads and sets nothing.
        if (_surface is not { IsPreview: true })
        {
            _lease = _brightness.Acquire(Lay);
        }
        else if (!_preview.Ready)
        {
            // A render shows one made-up display at 70 %, never this PC's.
            _preview = new BrightnessSnapshot(true, [new BrightnessDisplay(@"\\.\SAMPLE1", BrightnessPath.BuiltIn, 70)]);
        }

        _slider.AddHandler(PointerPressedEvent, _grab, true);
        _slider.AddHandler(PointerReleasedEvent, _release, true);
        _slider.AddHandler(PointerCaptureLostEvent, _release, true);
        _slider.AddHandler(PointerCanceledEvent, _release, true);
        AddHandler(PointerWheelChangedEvent, _wheel, true);
        PointerEntered += OnPointerEntered;
        Strings.Changed += OnLanguage;
        ApplyConfig(config);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as BrightnessConfig ?? new BrightnessConfig();
        Lay();
    }

    public void Detach()
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        _slider.RemoveHandler(PointerPressedEvent, _grab);
        _slider.RemoveHandler(PointerReleasedEvent, _release);
        _slider.RemoveHandler(PointerCaptureLostEvent, _release);
        _slider.RemoveHandler(PointerCanceledEvent, _release);
        RemoveHandler(PointerWheelChangedEvent, _wheel);
        PointerEntered -= OnPointerEntered;
        Strings.Changed -= OnLanguage;
        _lease?.Dispose();
        _lease = null;
    }

    private BrightnessSnapshot Snapshot => _lease is null ? _preview : _brightness.Snapshot;

    /// <summary>The display this surface is on (its window's), worked out once the view is in a window.</summary>
    private string? Own()
    {
        if (_own is not null || _surface is { IsPreview: true })
        {
            return _own;
        }

        if (XamlRoot?.ContentIslandEnvironment is { } environment)
        {
            _own = _brightness.DeviceOf((nint)Microsoft.UI.Win32Interop.GetWindowFromWindowId(environment.AppWindowId));
        }

        return _own;
    }

    /// <summary>The level on screen: one being written, else what the display took; null where it shows none.</summary>
    private int? Level()
    {
        if (BrightnessRules.Shown(Snapshot.Displays, Own(), _config.Screens) is not { } shown)
        {
            return null;
        }

        return (_lease is null ? null : _brightness.Asked(shown.Device)) ?? shown.Level;
    }

    /// <summary>[S-4440] On a new area, config or reading.</summary>
    private void Lay()
    {
        if (_detached)
        {
            return;
        }

        var snapshot = Snapshot;
        if (!snapshot.Ready)
        {
            // Not looked at yet: never a wrong level (§11), the default ([S-12813]): "--" and the slider disabled at
            // the middle.
            UnavailableLook.Enable(false, _slider);
            UnavailableLook.Say(this, null);
            _echoing = true;
            _slider.Value = Unavailable.Slider(null);
            _echoing = false;
            ShowReading(null, null);
            return;
        }

        var level = Level();

        // [S-4445] [S-1940] Nothing to set (no display has a brightness, or not the one this is on):
        // the same slider and sun, disabled at the middle ([S-1941]), the reason on hover.
        var reason = level is not null ? null
            : snapshot.Displays.Count == 0 ? BrightnessStr.Panels_Brightness_None.Text() : BrightnessStr.Panels_Brightness_OwnNone.Text();
        UnavailableLook.Enable(level is not null, _slider);
        UnavailableLook.Say(this, reason);

        if (!_grabbed)
        {
            _echoing = true;
            _slider.Value = Unavailable.Slider(level);
            _echoing = false;
        }

        var words = level is { } shown ? BrightnessStr.Panels_Brightness_Level.Format(("level", Formatting.Percent(shown / 100.0))) : reason;
        AutomationProperties.SetName(_slider, words);
        ToolTipService.SetToolTip(_slider, level is null ? null : words);
        ShowReading(_grabbed ? (int)Math.Round(_slider.Value) : level, reason);
    }

    /// <summary>
    /// [S-17184] The level beside the sun. [S-12813] Where nothing can be set it stays, "--" disabled beside the disabled
    /// slider ([S-4445]), the reason the row's tooltip.
    /// </summary>
    private void ShowReading(int? level, string? reason)
    {
        _level.Text = level is { } shown ? Formatting.Percent(shown / 100.0) : "--";
        _level.Foreground = Family.Brush(level is null ? ColourRole.TextDisabled : ColourRole.TextPrimary);
        ToolTipService.SetToolTip(_level, level is null ? reason : null);
    }

    private void SetLevel(int level)
    {
        if (_detached)
        {
            return;
        }

        if (_lease is null)
        {
            // A debug render: the made-up displays move, nothing else does.
            var dimmed = BrightnessRules.Dimmed(_preview.Displays, Own(), _config.Screens);
            _preview = _preview with { Displays = [.. _preview.Displays.Select(d => dimmed.Contains(d) ? d with { Level = level } : d)] };
            return;
        }

        _ = _brightness.Set(Own(), _config.Screens, level);
    }

    private void OnSliderChanged(double value)
    {
        if (_echoing)
        {
            return;
        }

        SetLevel((int)Math.Round(value));

        // [S-2152] The Readout follows the hand, not the display's next answer.
        ShowReading((int)Math.Round(value), null);
    }

    private void OnGrab(object sender, PointerRoutedEventArgs args) => _grabbed = true;

    private void OnRelease(object sender, PointerRoutedEventArgs args)
    {
        _grabbed = false;
        Lay();
    }

    /// <summary>[S-4441] A monitor's own buttons say nothing: the pointer coming by reads the levels again.</summary>
    private void OnPointerEntered(object sender, PointerRoutedEventArgs args)
    {
        if (_lease is not null && !_grabbed)
        {
            _brightness.Refresh();
        }
    }

    /// <summary>[S-4444] The wheel anywhere on the tile, part-notches carried.</summary>
    private void OnWheel(object sender, PointerRoutedEventArgs args)
    {
        if (_detached || Level() is not { } level)
        {
            return;
        }

        var (delta, carry) = BrightnessRules.Wheel(_wheelCarry, args.GetCurrentPoint(this).Properties.MouseWheelDelta);
        _wheelCarry = carry;
        if (delta != 0)
        {
            Step(Math.Clamp(level + delta, 0, 100));
        }

        args.Handled = true;
    }

    private void Step(int level)
    {
        SetLevel(level);
        _echoing = true;
        _slider.Value = level;
        _echoing = false;
        ShowReading(level, null);
    }

    /// <summary>[S-4444] The arrows, Page Up/Down, Home and End, as on Windows' slider.</summary>
    public bool HandleKey(WidgetKey key)
    {
        if (_detached || Level() is not { } level || BrightnessRules.Key(key, level) is not { } next)
        {
            return false;
        }

        Step(next);
        return true;
    }

    /// <summary>Only a tile with a level to move takes the keyboard.</summary>
    public bool WantsKeys => !_detached && Level() is not null;

    private void OnLanguage(Locale locale) => Lay();
}
