using Glyphs = Themia.App.Shell.Glyphs;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.Sdk;
using Themia.Widget.Volume;

namespace Themia.App.Widgets;

/// <summary>
/// The master level of an output device: its Readout over the slider, a meter you can set.
/// <para>
/// [S-1220] Kept current by Core Audio's own volume notification (through the shared
/// <see cref="CoreAudio"/> worker) rather than by a poll, so the slider follows the volume keys,
/// the flyout and whatever a game just did the moment it happens. It is built so it cannot fight
/// the hand on it — while the thumb is held, snapshots leave it alone, and a dragged level is held
/// on screen until Windows confirms it.
/// </para>
/// <para>
/// [S-2151] [S-2731] The slider with its speaker (mute) button at the start is the Controls block's
/// row at the card's bottom inset at every size ([S-6716]); over it the level is a Readout where a
/// meter's stands, on 28, or 40 once expanded both ways, as CPU's does, with the device's name as its line under where
/// that fits ([S-2150], <see cref="LevelFace"/>). Muted, the Readout is drawn in the disabled role.
/// </para>
/// </summary>
internal sealed partial class VolumeView : Grid, IWidgetView, IWidgetOpens, IWidgetSurfaceAware, IWidgetKeys, IWidgetContextActions
{
    /// <inheritdoc />
    public CardOpen? Opens => VolumeModule.Opens;

    private readonly AudioFeed _feed;
    private readonly ControlsBlock _controls = new();
    private readonly Slider _slider;

    /// <summary>[S-2151] The level, where a meter's reading stands: the view's top, in the side inset.</summary>
    private readonly ReadoutBlock _reading = new() { VerticalAlignment = VerticalAlignment.Top, Visibility = Visibility.Collapsed };
    private readonly Button _mute;

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
    private AudioSnapshot? _snapshot;
    private Dictionary<string, double> _overrides = new(StringComparer.Ordinal);

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

#if DEBUG
    // Debug renders: a fixture snapshot stands still; the live feed is not asked again over it.
    private bool _frozen;
#else
    private const bool _frozen = false;
#endif

    public VolumeView(IWidgetHost host)
    {
        _feed = new AudioFeed(host.Audio, OnSnapshot);

        // [S-2730] [S-6716] The speaker and the slider are the Controls block's row at the bottom inset.
        _controls.VerticalAlignment = VerticalAlignment.Bottom;
        _controls.Margin = new Thickness(FamilySpace.Inset, 0, FamilySpace.Inset, FamilySpace.Inset);
        _controls.FlowDirection = FlowDirection.LeftToRight;
        _mute = _controls.Icon(Glyphs.Volume3, Str.Mixer_Mute.Text(), () => ToggleMute());
        _mute.IsTabStop = false;
        _slider = _controls.Slider(0, 100, 0, OnSliderChanged);
        _slider.StepFrequency = 1;
        _slider.IsTabStop = false;

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

        // [S-2732] The wheel anywhere on the tile, not only over the slider's track. A clear background
        // makes the tile's empty room hit-testable, so the wheel lands there too.
        _wheel = OnWheel;
        AddHandler(PointerWheelChangedEvent, _wheel, true);
        Background = Family.Brush(ColourRole.Clear);

        _controls.Visibility = Visibility.Collapsed;
        Children.Add(_controls);

        _reading.Margin = Family.SideInset;
        _reading.UnderText.TextTrimming = TextTrimming.CharacterEllipsis;
        Children.Add(_reading);
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

    /// <summary>[S-2730] Runs on a new area, a new config and a new snapshot.</summary>
    private void Lay()
    {
        if (_detached)
        {
            return;
        }

        if (!_frozen)
        {
            _feed.Want(new AudioWant(_config.DeviceId, Sessions: false));
        }

        if (_snapshot is not { } snapshot)
        {
            return;
        }

        _controls.Visibility = Visibility.Visible;

        if (snapshot.Status != AudioStatus.Ready || snapshot.Output is null)
        {
            // [S-1940] No device to set (none, the chosen one not connected, Core Audio not answering):
            // the same slider and speaker, disabled at the middle ([S-1941]), the reason on hover.
            UnavailableLook.Say(this, snapshot.Status switch
            {
                AudioStatus.DeviceMissing => VolumeStr.WidgetsC_Volume_NotConnected.Format(("device", AudioRules.DeviceName(_config.DeviceId, snapshot.Devices, _config.DeviceName))),
                AudioStatus.NoDevice => VolumeStr.WidgetsC_Volume_NoDevice.Text(),
                _ => VolumeStr.WidgetsC_Volume_Unavailable.Text(),
            });
            UnavailableLook.Enable(false, _slider, _mute);
            Glyph(ColourRole.TextDisabled);
            _reading.Visibility = Visibility.Collapsed;
            ShowLevel(Unavailable.Share, muted: false);
            ToolTipService.SetToolTip(_mute, null);
            return;
        }

        // [S-446] Keep a dragged level until a snapshot confirms it.
        var master = new MixerRow(MixerRules.MasterKey, string.Empty, null, snapshot.Output.Level, snapshot.Output.Muted, true, false, 0);
        _overrides = MixerRules.ReconcileOverrides(_overrides, [master]);

        if (Current() is not { } level)
        {
            return;
        }

        UnavailableLook.Say(this, null);

        // Read-only is a state of the controls, not a reason to hide them: a greyed slider still
        // shows the level, and a tile that drops its slider when control is switched off looks
        // like a different widget.
        UnavailableLook.Enable(_config.AllowControl, _slider, _mute);
        Glyph(_config.AllowControl ? ColourRole.TextPrimary : ColourRole.TextDisabled);

        ShowLevel(level.Level, level.Muted);
        ShowReading(level.Level, level.Muted, snapshot.Output.Name);
    }

    /// <summary>
    /// [S-2151] The level as a Readout on <see cref="LevelFace"/>'s step, the device's name its line
    /// under where that fits; muted, an off reading: the level it goes back to, in the disabled role.
    /// </summary>
    private void ShowReading(double level, bool muted, string device)
    {
        _reading.Visibility = Visibility.Visible;
        _reading.Use(LevelFace.Step);
        var split = ReadoutRules.Split(Formatting.Percent(VolumeRules.Percent(level) / 100.0));
        _reading.Show(split.Value, split.Unit, device, valueRole: muted ? ColourRole.TextDisabled : ColourRole.TextPrimary);
    }

    /// <summary>The speaker's glyph role: primary, or disabled with the button (its glyph names its own brush).</summary>
    private void Glyph(ColourRole role)
    {
        if (_mute.Content is FontIcon icon)
        {
            icon.Foreground = Family.Brush(role);
        }
    }

    private void ShowLevel(double level, bool muted)
    {
        // With no number on the tile, the speaker glyph and its tooltip carry the muted state.
        ControlsBlock.Reglyph(_mute, AudioLook.OutputGlyph(level, muted), (muted ? Str.Mixer_Unmute : Str.Mixer_Mute).Text());

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

    private void OnSliderChanged(double value)
    {
        if (_echoing)
        {
            return;
        }

        SetLevel(VolumeRules.Scalar(value));

        // [S-2151] The Readout follows the hand: the held level is what Current() gives now.
        Lay();
    }

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

        _slider.RemoveHandler(PointerPressedEvent, _grab);
        _slider.RemoveHandler(PointerReleasedEvent, _release);
        _slider.RemoveHandler(PointerCaptureLostEvent, _release);
        _slider.RemoveHandler(PointerCanceledEvent, _release);
        RemoveHandler(PointerWheelChangedEvent, _wheel);
        _feed.Dispose();
    }
}
