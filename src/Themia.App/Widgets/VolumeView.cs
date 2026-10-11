using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Glyphs = Themia.App.Shell.Glyphs;

namespace Themia.App.Widgets;

/// <summary>
/// The master level of an output device, as the volume flyout would show it.
/// <para>
/// [S-1220] A readout first, kept current by Core Audio's own volume notification (through the
/// shared <see cref="CoreAudio"/> worker) rather than by a poll, so the tile follows the volume keys,
/// the flyout and whatever a game just did the moment it happens. The slider is the second job, and
/// it is built so it cannot fight the first — while the thumb is held, snapshots leave it alone, and
/// a dragged level is held on screen until Windows confirms it.
/// </para>
/// <para>
/// [S-1230] A ladder: the number and the slider at every size; the device's name in place of
/// "Output level" once there is room beside the number; a live meter from a medium tile. A larger
/// tile gives the number more room, nothing else: the microphone, the apps and a device switcher
/// are not this widget's job (the mixer lists apps; Windows picks the output).
/// </para>
/// </summary>
internal sealed partial class VolumeView : Grid, IWidgetView, IWidgetSizeAware, IWidgetSurfaceAware, IWidgetKeys, IWidgetContextActions
{
    /// <summary>Below this content width the caption beside the number is dropped: the number is the point.</summary>
    private const double CaptionMinimumWidth = 168;

    private readonly AudioFeed _feed;
    private readonly TextBlock _readout = Fluent.Text(FluentType.Subtitle);
    private readonly TextBlock _caption = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
    private readonly Grid _reading = new();
    private readonly Rectangle _meter = new() { Height = AudioLook.MeterHeight };
    private readonly Rectangle _meterTrack = new() { Height = AudioLook.MeterHeight };
    private readonly Grid _meterHost = new();
    private readonly ScaleTransform _meterScale = new() { ScaleX = 0 };
    private readonly Slider _slider = new();
    private readonly Button _mute = new();
    private readonly FontIcon _muteIcon;
    private readonly Grid _band = new();
    private readonly StackPanel _empty;
    private readonly TextBlock _emptyText = new() { Text = Str.WidgetsC_Volume_NoDevice.Text() };

    // Looked up once: the readout swaps between them on every change.
    private readonly Brush _readingBrush = FluentLayout.Themed("TextFillColorPrimaryBrush", Colors.White);
    private readonly Brush _mutedBrush = FluentLayout.Themed("TextFillColorDisabledBrush", Colors.Gray);

    /// <summary>
    /// The exact delegate instances the slider was hooked with. <c>RemoveHandler</c> matches on the
    /// delegate, so building a fresh one at teardown would unhook nothing and leave a torn-down
    /// view listening to a control that outlives it.
    /// </summary>
    private readonly PointerEventHandler _grab;

    private readonly PointerEventHandler _release;

    private VolumeConfig _config = new();
    private IWidgetSurface? _surface;
    private WidgetArea _area;
    private AudioSnapshot? _snapshot;
    private AudioPeaks _peaks = AudioPeaks.Silent;
    private Dictionary<string, double> _overrides = new(StringComparer.Ordinal);
    private TypeStep _heroStep = FluentType.Subtitle;
    private TypeStep _captionStep = FluentType.Caption;
    private bool _peaksOn;
    private bool _loaded;

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

    public VolumeView()
    {
        _feed = new AudioFeed(OnSnapshot, OnPeaks);

        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        _readout.Text = "--";
        _readout.Foreground = _readingBrush;
        _readout.VerticalAlignment = VerticalAlignment.Bottom;

        // [S-1232] The caption names the device. It is text, not a switcher: picking the output is
        // Windows' job (and the settings page's), not the tile's.
        _caption.TextTrimming = TextTrimming.CharacterEllipsis;
        _caption.HorizontalAlignment = HorizontalAlignment.Left;
        _caption.VerticalAlignment = VerticalAlignment.Bottom;

        // Lifted off the baseline it sits beside: bottom-aligning a 12px caption against a 68px
        // number puts it below the number's baseline, which reads as a slipped line.
        _caption.Margin = new Thickness(0, 0, 0, Fluent.Space1);

        // A Grid rather than a horizontal StackPanel: a StackPanel measures its children with
        // unbounded width, so the caption never learns how much room it has and its trimming never
        // fires — on a narrow tile it runs off the edge of the card instead of ellipsising.
        _reading.ColumnSpacing = Fluent.Space2;
        _reading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _reading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _reading.Margin = WidgetLayout.SideInset;
        _reading.VerticalAlignment = VerticalAlignment.Center;
        SetColumn(_caption, 1);
        _reading.Children.Add(_readout);
        _reading.Children.Add(_caption);

        // [S-1223] The meter: a scale transform on a full-width bar, so a sample costs no layout.
        _meter.Fill = FluentLayout.Themed("AccentFillColorDefaultBrush", Colors.DodgerBlue);
        _meter.RenderTransform = _meterScale;
        _meter.RenderTransformOrigin = new Windows.Foundation.Point(0, 0.5);
        _meter.IsHitTestVisible = false;
        _meterTrack.Fill = FluentLayout.Themed("ControlStrongFillColorDisabledBrush", Colors.Gray);
        _meterHost.Children.Add(_meterTrack);
        _meterHost.Children.Add(_meter);
        SetRow(_meterHost, 1);

        _muteIcon = Fluent.Icon(Glyphs.Volume3, "TextFillColorPrimaryBrush");
        _mute.Content = _muteIcon;
        _mute.Padding = new Thickness(Fluent.Space2, Fluent.Space1, Fluent.Space2, Fluent.Space1);
        _mute.CornerRadius = new CornerRadius(Fluent.ControlRadius);
        _mute.VerticalAlignment = VerticalAlignment.Center;
        _mute.IsTabStop = false;
        _mute.Click += OnMuteClicked;
        ToolTipService.SetToolTip(_mute, Str.Mixer_Mute.Text());

        _slider.Minimum = 0;
        _slider.Maximum = 100;
        _slider.StepFrequency = 1;
        _slider.VerticalAlignment = VerticalAlignment.Center;
        _slider.HorizontalAlignment = HorizontalAlignment.Stretch;
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

        _band.ColumnSpacing = Fluent.Space2;
        _band.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _band.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _band.Margin = WidgetLayout.FooterInset;

        SetColumn(_slider, 1);
        _band.Children.Add(_mute);
        _band.Children.Add(_slider);
        SetRow(_band, 2);

        var definition = WidgetCatalog.Definition(WidgetKind.Volume);
        _empty = Fluent.EmptyState(definition.Glyph, _emptyText, out _);
        _empty.Visibility = Visibility.Collapsed;
        SetRowSpan(_empty, 3);

        Children.Add(_reading);
        Children.Add(_meterHost);
        Children.Add(_band);
        Children.Add(_empty);

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
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
        : [new WidgetAction(Glyphs.Speakers, Str.WidgetsC_Volume_FollowDefault.Text(), () => PickDevice(null))];

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        _loaded = true;
        Lay();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        // [S-1223] Off screen: stop the meter.
        _loaded = false;
        Lay();
    }

    private void OnSnapshot(AudioSnapshot snapshot)
    {
        if (_detached)
        {
            return;
        }

        _snapshot = snapshot;
        Lay();
    }

    private void OnPeaks(AudioPeaks peaks)
    {
        if (_detached)
        {
            return;
        }

        _peaks = peaks;
        ShowPeaks();
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

    /// <summary>[S-1230] The ladder. Runs on a new area, a new config and a new snapshot.</summary>
    private void Lay()
    {
        if (_detached)
        {
            return;
        }

        var measured = _area.IsMeasured;
        // A short tile keeps a smaller foot under the slider: on a 120 px square, 8 px of it is the
        // difference between having a slider and not.
        var foot = _area.HeightClass == SizeClass.Compact ? Fluent.Space2 : WidgetLayout.ContentInset;
        var box = _area.Deflate(WidgetLayout.ContentInset * 2, foot);
        _band.Margin = new Thickness(WidgetLayout.ContentInset, 0, WidgetLayout.ContentInset, foot);
        var room = new Disclosure(box, gap: _area.HeightClass == SizeClass.Compact ? Fluent.Space1 : Fluent.Space2);

        // Rung 0: the number, at its smallest.
        room.Reserve(FluentType.BodyLarge.LineHeight);

        // Rung 1: the slider band.
        var band = _config.ShowSlider && room.Take(Fluent.RowHeightCompact);

        // Rung 2: [S-1223] the meter, from a medium tile (or along a wide strip).
        var peaks = _config.ShowPeaks && _loaded
            && (_area.Least >= SizeClass.Regular || _area.WidthClass >= SizeClass.Expanded)
            && room.Take(AudioLook.MeterHeight);

        // Nothing past the meter: the rest of a large tile goes to the number.
        _peaksOn = peaks;

        _feed.Want(new AudioWant(_config.DeviceId, Sessions: false, Peaks: peaks));

        if (_snapshot is not { } snapshot || !measured)
        {
            return;
        }

        if (snapshot.Status != AudioStatus.Ready || Current() is not { } level)
        {
            _emptyText.Text = snapshot.Status switch
            {
                AudioStatus.DeviceMissing => Str.WidgetsC_Volume_NotConnected.Format(("device", AudioRules.DeviceName(_config.DeviceId, snapshot.Devices, _config.DeviceName))),
                AudioStatus.NoDevice => Str.WidgetsC_Volume_NoDevice.Text(),
                _ => Str.WidgetsC_Volume_Unavailable.Text(),
            };

            _empty.Visibility = Visibility.Visible;
            _reading.Visibility = _band.Visibility = _meterHost.Visibility = Visibility.Collapsed;
            return;
        }

        // [S-446] Keep a dragged level until a snapshot confirms it.
        var master = new MixerRow(MixerRules.MasterKey, string.Empty, null, snapshot.Output!.Level, snapshot.Output.Muted, true, false, 0);
        _overrides = MixerRules.ReconcileOverrides(_overrides, [master]);
        level = Current() ?? level;

        _empty.Visibility = Visibility.Collapsed;
        _reading.Visibility = Visibility.Visible;
        _band.Visibility = band ? Visibility.Visible : Visibility.Collapsed;
        _meterHost.Visibility = peaks ? Visibility.Visible : Visibility.Collapsed;

        // Read-only is a state of the controls, not a reason to hide them: a greyed slider still
        // shows the level, and a tile that drops its slider when control is switched off looks
        // like a different widget.
        _slider.IsEnabled = _config.AllowControl;
        _mute.IsEnabled = _config.AllowControl;

        ShowLevel(level.Level, level.Muted, snapshot);

        // The meter sits between the slider band and whatever is above it, inset like the slider.
        _meterHost.Margin = new Thickness(WidgetLayout.ContentInset + Fluent.RowHeightCompact + Fluent.Space2, 0, WidgetLayout.ContentInset, band ? 0 : foot);

        ShowPeaks();

        // Whatever the rungs left goes to the number. Four characters: "100%" is the widest this
        // readout can ever be — fitting to the live text would make "9%" and "100%" pick different
        // steps, and the number would change size between two changes a moment apart.
        var captionBeside = box.Width >= CaptionMinimumWidth;
        var heroWidth = captionBeside ? box.Width / 2 : box.Width;
        var hero = FluentType.Hero(heroWidth, Math.Min(room.Remaining + FluentType.BodyLarge.LineHeight, FluentType.Display.LineHeight), 4);

        if (hero.FontSize < FluentType.BodyLarge.FontSize)
        {
            hero = FluentType.BodyLarge;
        }

        if (hero != _heroStep)
        {
            _heroStep = hero;
            _readout.Apply(hero);
        }

        var caption = hero.FontSize >= FluentType.Title.FontSize ? FluentType.Body : FluentType.Caption;
        if (caption != _captionStep)
        {
            _captionStep = caption;
            _caption.Apply(caption);
        }

        _caption.Visibility = captionBeside ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ShowLevel(double level, bool muted, AudioSnapshot snapshot)
    {
        _readout.Text = VolumeRules.Readout(muted ? 0 : level);

        // [S-1232] The device's name where "Output level" used to be.
        _caption.Text = VolumeRules.Caption(level, muted, snapshot.Output?.Name);

        // Muted is a colour, not an opacity: the disabled text brush is what every other disabled
        // thing on the desktop uses and it follows the theme.
        _readout.Foreground = muted ? _mutedBrush : _readingBrush;
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

    private void ShowPeaks()
    {
        var on = _peaksOn && Current() is { Muted: false };
        _meterScale.ScaleX = on ? AudioRules.Peak(_peaks.Output) : 0;
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

    private void ToggleMute()
    {
        if (_detached || !_config.AllowControl || Current() is not { } current)
        {
            return;
        }

        _feed.SetMute(MixerRules.MasterKey, !current.Muted);
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

    // ---- keys ([S-1226]) ------------------------------------------------------------------------

    public bool HandleKey(WidgetKey key)
    {
        if (_detached || !_config.AllowControl || Current() is not { } current)
        {
            return false;
        }

        var action = AudioRules.VolumeKey(key);

        switch (action.Kind)
        {
            case AudioKeyKind.Step:
                SetLevel(AudioRules.Step(current.Muted ? 0 : current.Level, action.Delta));
                Lay();
                return true;

            case AudioKeyKind.ToggleMute:
                ToggleMute();
                return true;

            default:
                return false;
        }
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
                Themia.App.Services.Notices.Warning(Str.WidgetsC_Volume_DefaultNotChanged.Text(), notice);
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

        Loaded -= OnLoaded;
        Unloaded -= OnUnloaded;
        _slider.ValueChanged -= OnSliderChanged;
        _slider.RemoveHandler(PointerPressedEvent, _grab);
        _slider.RemoveHandler(PointerReleasedEvent, _release);
        _slider.RemoveHandler(PointerCaptureLostEvent, _release);
        _slider.RemoveHandler(PointerCanceledEvent, _release);
        _mute.Click -= OnMuteClicked;
        _feed.Dispose();
    }
}
