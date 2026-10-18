using Glyphs = Themia.App.Shell.Glyphs;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Sdk;
using Themia.Widget.Volume;

namespace Themia.App.Widgets;

/// <summary>
/// The master level of an output device, as a slider and nothing else.
/// <para>
/// [S-1220] Kept current by Core Audio's own volume notification (through the shared
/// <see cref="CoreAudio"/> worker) rather than by a poll, so the slider follows the volume keys,
/// the flyout and whatever a game just did the moment it happens. It is built so it cannot fight
/// the hand on it — while the thumb is held, snapshots leave it alone, and a dragged level is held
/// on screen until Windows confirms it.
/// </para>
/// <para>
/// [S-2730] The tile is the slider with its speaker (mute) button at the start, at every size: no
/// number, no meter, no device name. A larger tile gives the slider more length, nothing else
/// ([S-666]); a column stands it up ([S-2731]).
/// </para>
/// </summary>
internal sealed partial class VolumeView : Grid, IWidgetView, IWidgetSizeAware, IWidgetSurfaceAware, IWidgetKeys, IWidgetContextActions
{
    private readonly AudioFeed _feed;
    private readonly Slider _slider = new();
    private readonly Button _mute = new();
    private readonly FontIcon _muteIcon;
    private readonly Grid _band = new();
    private readonly StackPanel _empty;
    private readonly TextBlock _emptyText = new() { Text = VolumeStr.WidgetsC_Volume_NoDevice.Text() };

    /// <summary>
    /// The exact delegate instances the slider and the tile were hooked with. <c>RemoveHandler</c>
    /// matches on the delegate, so building a fresh one at teardown would unhook nothing and leave a
    /// torn-down view listening to a control that outlives it.
    /// </summary>
    private readonly PointerEventHandler _grab;

    private readonly PointerEventHandler _release;
    private readonly PointerEventHandler _wheel;

    private VolumeConfig _config = new();
    private IWidgetSurface? _surface;
    private WidgetArea _area;
    private AudioSnapshot? _snapshot;
    private Dictionary<string, double> _overrides = new(StringComparer.Ordinal);
    private VolumeAxis? _axis;

    /// <summary>[S-2732] The part of a wheel notch not spent yet (a touchpad reports a notch in pieces).</summary>
    private int _wheelCarry;

    /// <summary>
    /// True while the slider is being written from a snapshot rather than by a person. The slider
    /// raises <c>ValueChanged</c> either way, and without this every snapshot would write the level
    /// it had just read straight back into the mixer.
    /// </summary>
    private bool _echoing;

    /// <summary>
    /// True while the thumb is held. A snapshot that lands mid-drag would snap it back to where the
    /// mixer was a moment ago, which feels like the control fighting the hand on it.
    /// </summary>
    private bool _grabbed;

    private bool _detached;

    public VolumeView(IWidgetHost host)
    {
        _feed = new AudioFeed(host.Audio, OnSnapshot);

        _muteIcon = Fluent.Icon(Glyphs.Volume3, "TextFillColorPrimaryBrush");
        _mute.Content = _muteIcon;
        _mute.Padding = new Thickness(Fluent.Space2, Fluent.Space1, Fluent.Space2, Fluent.Space1);
        _mute.CornerRadius = new CornerRadius(Fluent.ControlRadius);
        _mute.HorizontalAlignment = HorizontalAlignment.Center;
        _mute.VerticalAlignment = VerticalAlignment.Center;
        _mute.IsTabStop = false;
        _mute.Click += OnMuteClicked;
        ToolTipService.SetToolTip(_mute, Str.Mixer_Mute.Text());

        _slider.Minimum = 0;
        _slider.Maximum = 100;
        _slider.StepFrequency = 1;
        _slider.IsTabStop = false;
        _slider.ValueChanged += OnSliderChanged;

        // Handled events too: Slider marks its own pointer events handled, so a plain subscription
        // never sees the press. Four events rather than one, for the same reason a row's hover
        // needs four — the pointer leaves without an Exited when the capture is taken or the
        // widget is torn down mid-drag, and either would strand the tile refusing to update.
        _grab = OnGrab;
        _release = OnRelease;
        _slider.AddHandler(PointerPressedEvent, _grab, true);
        _slider.AddHandler(PointerReleasedEvent, _release, true);
        _slider.AddHandler(PointerCaptureLostEvent, _release, true);
        _slider.AddHandler(PointerCanceledEvent, _release, true);

        // [S-2732] The wheel anywhere on the tile, not only over the slider's track. A transparent
        // background makes the tile's empty room hit-testable, so the wheel lands there too.
        _wheel = OnWheel;
        AddHandler(PointerWheelChangedEvent, _wheel, true);
        Background = ThemeBrush.Of("SubtleFillColorTransparentBrush", Colors.Transparent);

        _band.Children.Add(_mute);
        _band.Children.Add(_slider);

        var definition = ModuleCatalog.Of(new WidgetId("volume"));
        _empty = Fluent.EmptyState(definition.Glyph, _emptyText, out _);
        _empty.Visibility = Visibility.Collapsed;

        Children.Add(_band);
        Children.Add(_empty);

        Orient(VolumeAxis.Horizontal);
    }

    public void Bind(IWidgetSurface surface)
    {
        _surface = surface;
        _feed.Preview = surface.IsPreview;
    }

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        // [S-1220] No ticker: the level moves when Core Audio says it moved.
        _detached = false;
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;
        ApplyConfig(config);
    }

    /// <summary>[S-1110] The next lay-out re-reads every text.</summary>
    private void OnLanguage(Locale locale) => Lay();

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as VolumeConfig ?? new VolumeConfig();
        Lay();
    }

    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Lay();
    }

    /// <inheritdoc />
    public IReadOnlyList<WidgetAction> ContextActions => _config.DeviceId is null
        ? []
        : [new WidgetAction(Glyphs.Speakers, VolumeStr.WidgetsC_Volume_FollowDefault.Text(), () => PickDevice(null))];

    private void OnSnapshot(AudioSnapshot snapshot)
    {
        if (_detached)
        {
            return;
        }

        _snapshot = snapshot;
        Lay();
    }

    /// <summary>The level on screen: a dragged one Windows has not confirmed yet, else the device's.</summary>
    private (double Level, bool Muted)? Current()
    {
        if (_snapshot?.Output is not { } output)
        {
            return null;
        }

        return _overrides.TryGetValue(MixerRules.MasterKey, out var held)
            ? (held, output.Muted && held <= 0)
            : (output.Level, output.Muted);
    }

    /// <summary>
    /// [S-2731] Lays the band along the tile: the speaker button left of a horizontal slider, or
    /// under a vertical one (a vertical slider starts at the bottom, so the button stays at its start).
    /// </summary>
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
            SetColumn(_slider, 0);
            SetRow(_slider, 0);
            SetColumn(_mute, 0);
            SetRow(_mute, 1);
            return;
        }

        _band.ColumnSpacing = Fluent.Space2;
        _band.RowSpacing = 0;
        _band.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _band.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _band.Margin = WidgetLayout.SideInset;

        // The only thing on the tile, so it sits in the middle of it rather than at the foot.
        _band.VerticalAlignment = VerticalAlignment.Center;

        _slider.Orientation = Orientation.Horizontal;
        _slider.HorizontalAlignment = HorizontalAlignment.Stretch;
        _slider.VerticalAlignment = VerticalAlignment.Center;
        SetRow(_slider, 0);
        SetColumn(_slider, 1);
        SetRow(_mute, 0);
        SetColumn(_mute, 0);
    }

    /// <summary>[S-2730] Runs on a new area, a new config and a new snapshot.</summary>
    private void Lay()
    {
        if (_detached)
        {
            return;
        }

        _feed.Want(new AudioWant(_config.DeviceId, Sessions: false));

        if (!_area.IsMeasured)
        {
            return;
        }

        Orient(VolumeRules.Axis(_area));

        if (_snapshot is not { } snapshot)
        {
            return;
        }

        if (snapshot.Status != AudioStatus.Ready || snapshot.Output is null)
        {
            _emptyText.Text = snapshot.Status switch
            {
                AudioStatus.DeviceMissing => VolumeStr.WidgetsC_Volume_NotConnected.Format(("device", AudioRules.DeviceName(_config.DeviceId, snapshot.Devices, _config.DeviceName))),
                AudioStatus.NoDevice => VolumeStr.WidgetsC_Volume_NoDevice.Text(),
                _ => VolumeStr.WidgetsC_Volume_Unavailable.Text(),
            };

            _empty.Visibility = Visibility.Visible;
            _band.Visibility = Visibility.Collapsed;
            return;
        }

        // [S-446] Keep a dragged level until a snapshot confirms it.
        var master = new MixerRow(MixerRules.MasterKey, string.Empty, null, snapshot.Output.Level, snapshot.Output.Muted, true, false, 0);
        _overrides = MixerRules.ReconcileOverrides(_overrides, [master]);

        if (Current() is not { } level)
        {
            return;
        }

        _empty.Visibility = Visibility.Collapsed;
        _band.Visibility = Visibility.Visible;

        // Read-only is a state of the controls, not a reason to hide them: a greyed slider still
        // shows the level, and a tile that drops its slider when control is switched off looks
        // like a different widget.
        _slider.IsEnabled = _config.AllowControl;
        _mute.IsEnabled = _config.AllowControl;

        ShowLevel(level.Level, level.Muted);
    }

    private void ShowLevel(double level, bool muted)
    {
        // With no number on the tile, the speaker glyph and its tooltip carry the muted state.
        _muteIcon.Glyph = AudioLook.OutputGlyph(level, muted);
        ToolTipService.SetToolTip(_mute, (muted ? Str.Mixer_Unmute : Str.Mixer_Mute).Text());

        // Echoing: this is a snapshot writing back what it just read, and the ValueChanged it raises
        // must not be mistaken for somebody moving the control.
        if (!_grabbed)
        {
            _echoing = true;
            _slider.Value = muted ? 0 : VolumeRules.Percent(level);
            _echoing = false;
        }
    }

    // ---- writes -------------------------------------------------------------------------------

    private void SetLevel(double level)
    {
        if (_detached || !_config.AllowControl || Current() is not { } current)
        {
            return;
        }

        _overrides[MixerRules.MasterKey] = level;

        // Moving a muted endpoint above silence unmutes it, which is what the volume flyout does
        // and what anybody dragging a slider expects.
        if (current.Muted && level > 0)
        {
            _feed.SetMute(MixerRules.MasterKey, false);
        }

        _feed.SetLevel(MixerRules.MasterKey, level);
    }

    /// <summary>[S-1226] [S-2732] A step from where the slider shows now (a muted tile steps up from silence).</summary>
    private bool Step(int delta)
    {
        if (_detached || !_config.AllowControl || Current() is not { } current)
        {
            return false;
        }

        SetLevel(AudioRules.Step(current.Muted ? 0 : current.Level, delta));
        Lay();
        return true;
    }

    private bool ToggleMute()
    {
        if (_detached || !_config.AllowControl || Current() is not { } current)
        {
            return false;
        }

        _feed.SetMute(MixerRules.MasterKey, !current.Muted);
        return true;
    }

    private void OnGrab(object sender, PointerRoutedEventArgs args) => _grabbed = true;

    private void OnRelease(object sender, PointerRoutedEventArgs args)
    {
        _grabbed = false;

        // Show what the machine holds now; a held level stays until a snapshot confirms it.
        Lay();
    }

    private void OnSliderChanged(object sender, RangeBaseValueChangedEventArgs args)
    {
        if (_echoing || Math.Abs(args.NewValue - args.OldValue) < 0.5)
        {
            return;
        }

        SetLevel(VolumeRules.Scalar(args.NewValue));
    }

    private void OnMuteClicked(object sender, RoutedEventArgs args) => ToggleMute();

    /// <summary>[S-2732] One arrow step per notch, carrying a touchpad's part-notches to the next event.</summary>
    private void OnWheel(object sender, PointerRoutedEventArgs args)
    {
        if (_detached || !_config.AllowControl || Current() is null)
        {
            return;
        }

        var (delta, carry) = AudioRules.Wheel(_wheelCarry, args.GetCurrentPoint(this).Properties.MouseWheelDelta);
        _wheelCarry = carry;

        if (delta != 0)
        {
            Step(delta);
        }

        // Taken even for a part-notch: the tile is the volume, so the wheel over it is too.
        args.Handled = true;
    }

    // ---- keys ([S-1226], [S-2733]) ---------------------------------------------------------------

    public bool HandleKey(WidgetKey key)
    {
        var action = AudioRules.VolumeKey(key);

        return action.Kind switch
        {
            AudioKeyKind.Step => Step(action.Delta),
            AudioKeyKind.ToggleMute => ToggleMute(),
            _ => false,
        };
    }

    // ---- following the default ([S-1222]) ----------------------------------------------------

    /// <summary>
    /// [S-1614] A pick makes the device Windows' default and the tile keeps following the default;
    /// when Windows refuses, the tile binds to the device and says why ([S-1616]).
    /// </summary>
    private async void PickDevice(AudioDevice? device)
    {
        if (_surface is not { IsPreview: false } surface)
        {
            return;
        }

        try
        {
            if (await _feed.PickAsync(device, _config.DeviceId, _config.DeviceName, _snapshot?.DefaultId) is not { } after || _detached)
            {
                return;
            }

            _config.DeviceId = after.DeviceId;
            _config.DeviceName = after.DeviceName;
            _overrides.Clear();
            surface.RequestSave();
            Lay();

            if (after.Notice is { } notice)
            {
                // [S-1616]/[S-880] Said once, as a notice: the widget is bound now, nothing to answer.
                Themia.App.Services.Notices.Warning(VolumeStr.WidgetsC_Volume_DefaultNotChanged.Text(), notice);
            }
        }
        catch (Exception ex)
        {
            Themia.Core.Diagnostics.Log.Error("audio", "the volume tile could not change its device", ex);
        }
    }

    public void Detach()
    {
        _detached = true;
        Strings.Changed -= OnLanguage;

        _slider.ValueChanged -= OnSliderChanged;
        _slider.RemoveHandler(PointerPressedEvent, _grab);
        _slider.RemoveHandler(PointerReleasedEvent, _release);
        _slider.RemoveHandler(PointerCaptureLostEvent, _release);
        _slider.RemoveHandler(PointerCanceledEvent, _release);
        RemoveHandler(PointerWheelChangedEvent, _wheel);
        _mute.Click -= OnMuteClicked;
        _feed.Dispose();
    }
}
