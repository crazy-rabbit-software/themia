using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Themia.App.Services;
using Themia.App.Services.Brightness;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Widget.Brightness;
using Glyphs = Themia.App.Shell.Glyphs;

namespace Themia.App.Widgets;

/// <summary>
/// [S-4440] The display brightness as one slider and nothing else, as Windows 11's quick settings row and
/// Karpo's widget (§35, §103): the sun at its start, no number, no display names. A larger tile gives the
/// slider more length; a column stands it up, as the volume tile ([S-2731]).
/// <para>
/// [S-4442] It sets every display whose brightness can be set, together, or with "Only the screen it's
/// on" the display its surface is on — one window per display, so the display is the window's. It shows
/// the level of its own display, or of the first that has one. The levels come from
/// the host's brightness service (<see cref="IBrightness"/>), which hears a built-in panel change and reads a monitor again when the
/// pointer comes by; while the thumb is held a reading never moves it.
/// </para>
/// </summary>
internal sealed partial class BrightnessView : Grid, IWidgetView, IWidgetSizeAware, IWidgetSurfaceAware, IWidgetKeys
{
    private readonly Slider _slider = new();
    private readonly FontIcon _sun;
    private readonly Grid _band = new();
    private readonly Microsoft.UI.Xaml.Media.Brush _sunBrush = ThemeBrush.Of("TextFillColorPrimaryBrush", Colors.White);

    private readonly PointerEventHandler _grab;
    private readonly PointerEventHandler _release;
    private readonly PointerEventHandler _wheel;

    private BrightnessConfig _config = new();
    private IWidgetSurface? _surface;
    private readonly IBrightness _brightness;
    private IDisposable? _lease;
    private WidgetArea _area;
    private VolumeAxis? _axis;
    private string? _own;
    private BrightnessSnapshot _preview = BrightnessSnapshot.Pending;
    private int _wheelCarry;
    private bool _echoing;
    private bool _grabbed;
    private bool _detached = true;

    public BrightnessView(IWidgetHost host)
    {
        _brightness = host.Brightness;
        _sun = Fluent.Icon(Glyphs.Brightness, "TextFillColorPrimaryBrush");
        _sun.HorizontalAlignment = HorizontalAlignment.Center;
        _sun.VerticalAlignment = VerticalAlignment.Center;
        _sun.Margin = new Thickness(Fluent.Space2, 0, 0, 0);

        _slider.Minimum = 0;
        _slider.Maximum = 100;
        _slider.StepFrequency = 1;
        _slider.IsTabStop = false;

        _grab = OnGrab;
        _release = OnRelease;
        _wheel = OnWheel;

        _band.Children.Add(_sun);
        _band.Children.Add(_slider);
        _band.Visibility = Visibility.Collapsed;

        Children.Add(_band);

        // The wheel anywhere on the tile, as on the volume tile.
        Background = ThemeBrush.Of("SubtleFillColorTransparentBrush", Colors.Transparent);
        Orient(VolumeAxis.Horizontal);
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

        _slider.ValueChanged += OnSliderChanged;
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
        _slider.ValueChanged -= OnSliderChanged;
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

    private void Orient(VolumeAxis axis)
    {
        if (_axis == axis)
        {
            return;
        }

        _axis = axis;
        _band.RowDefinitions.Clear();
        _band.ColumnDefinitions.Clear();

        if (axis == VolumeAxis.Vertical)
        {
            _band.RowSpacing = Fluent.Space2;
            _band.ColumnSpacing = 0;
            _band.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            _band.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _band.Margin = WidgetLayout.Inset;
            _band.VerticalAlignment = VerticalAlignment.Stretch;
            _slider.Orientation = Orientation.Vertical;
            _slider.HorizontalAlignment = HorizontalAlignment.Center;
            _slider.VerticalAlignment = VerticalAlignment.Stretch;
            _sun.Margin = new Thickness(0, 0, 0, Fluent.Space1);
            SetColumn(_slider, 0);
            SetRow(_slider, 0);
            SetColumn(_sun, 0);
            SetRow(_sun, 1);
            return;
        }

        _band.ColumnSpacing = Fluent.Space3;
        _band.RowSpacing = 0;
        _band.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _band.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _band.Margin = WidgetLayout.SideInset;
        _band.VerticalAlignment = VerticalAlignment.Center;
        _slider.Orientation = Orientation.Horizontal;
        _slider.HorizontalAlignment = HorizontalAlignment.Stretch;
        _slider.VerticalAlignment = VerticalAlignment.Center;
        _sun.Margin = new Thickness(Fluent.Space2, 0, 0, 0);
        SetRow(_slider, 0);
        SetColumn(_slider, 1);
        SetRow(_sun, 0);
        SetColumn(_sun, 0);
    }

    /// <summary>[S-4440] On a new area, config or reading.</summary>
    private void Lay()
    {
        if (_detached || !_area.IsMeasured)
        {
            return;
        }

        Orient(VolumeRules.Axis(_area));
        var snapshot = Snapshot;
        if (!snapshot.Ready)
        {
            // Not looked at yet: the frame, never a wrong level (§11).
            _band.Visibility = Visibility.Collapsed;
            return;
        }

        _band.Visibility = Visibility.Visible;
        var level = Level();

        // [S-4445] [S-1940] Nothing to set (no display has a brightness, or not the one this is on):
        // the same slider and sun, disabled at the middle ([S-1941]), the reason on hover.
        var reason = level is not null ? null
            : snapshot.Displays.Count == 0 ? BrightnessStr.Panels_Brightness_None.Text() : BrightnessStr.Panels_Brightness_OwnNone.Text();
        UnavailableLook.Enable(level is not null, _slider);
        _sun.Foreground = level is null ? UnavailableLook.Text : _sunBrush;
        UnavailableLook.Say(this, reason);

        if (!_grabbed)
        {
            _echoing = true;
            _slider.Value = Unavailable.Slider(level);
            _echoing = false;
        }

        var words = level is { } shown ? BrightnessStr.Panels_Brightness_Level.Format(("level", Formatting.Percent(shown / 100.0))) : reason;
        AutomationProperties.SetName(_slider, words);
        ToolTipService.SetToolTip(_sun, level is null ? null : words);
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

    private void OnSliderChanged(object sender, RangeBaseValueChangedEventArgs args)
    {
        if (_echoing || Math.Abs(args.NewValue - args.OldValue) < 0.5)
        {
            return;
        }

        SetLevel((int)Math.Round(args.NewValue));
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
