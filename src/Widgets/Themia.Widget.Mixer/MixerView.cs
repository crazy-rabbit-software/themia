using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.Mixer;
using Glyphs = Themia.App.Shell.Glyphs;

namespace Themia.App.Widgets;

/// <summary>
/// [S-440] Per-application volume, as the Windows volume mixer lays it out: one row per app with its icon, its name, a
/// slider and a mute button, a kit list ([S-7074]) over the host's audio (<see cref="AudioFeed"/>) drawn in the mixer's
/// own look (<see cref="MixerLook"/>). No master row and no device name ([S-3251]): the device's own level is the volume
/// tile's.
/// <para>
/// [S-449] The rows are the kit's: their one fixed size, as many as the card reaches, the rest a scroll away; the
/// playing apps first and the quiet ones under them while rows are left. Nothing playing is the kit's state row; no
/// device is the stand-in row in the disabled role ([S-448], [S-1940]).
/// </para>
/// </summary>
internal sealed partial class MixerView : Grid, IWidgetView, IWidgetOpens, IWidgetSizeAware, IWidgetSurfaceAware, IWidgetKeys, IWidgetContextActions
{
    /// <inheritdoc />
    public CardOpen? Opens => MixerModule.Opens;

    /// <summary>Nothing on a row ages: the list is redrawn when Windows says; this is only the binding's slowest tick.</summary>
    private static readonly TimeSpan Redraw = TimeSpan.FromMinutes(15);

    private readonly AudioFeed _feed;
    private readonly KitBinding _binding;
    private readonly MixerLook _look;
    private readonly MixerSource _source;

    private MixerConfig _config = new();
    private IWidgetSurface? _surface;
    private WidgetArea _area;
    private AudioSnapshot? _snapshot;
    private Dictionary<string, double> _overrides = new(StringComparer.Ordinal);

    /// <summary>The rows on screen by key: the app's row, whether it is quiet, whether a device is there to set.</summary>
    private Dictionary<string, (MixerRow Row, bool Dimmed, bool Available)> _shown = new(StringComparer.Ordinal);

    /// <summary>[S-1940] Why there is no device to mix (none, the chosen one gone, Core Audio silent), or null while there is.</summary>
    private string? _unavailable;

    private bool _attached;

    // [S-11850] A sample's snapshot stands still; the live feed is not asked again over it.
    private bool _frozen;

    public MixerView(IWidgetHost host)
    {
        _feed = new AudioFeed(host.Audio, OnSnapshot);
        _look = new MixerLook(this);
        _source = new MixerSource(this);
        _binding = new KitBinding(host, MixerSpec.Spec, Glyphs.Mixer, item => MixerSpec.Actions(RowOf(item.Id)?.Row, RowOf(item.Id)?.Available == true), Run)
        {
            Look = _look,
            Decorate = LinkRow,
            Redrawing = ForgetRows,
            CursorMoved = PointCursor,
        };

        Children.Add(_binding.List);
        Children.Add(_binding.State);
        _binding.Status.Place(this);
    }

    public void Bind(IWidgetSurface surface)
    {
        _surface = surface;
        _feed.Preview = surface.IsPreview;
    }

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _config = config as MixerConfig ?? new MixerConfig();
        _attached = true;
        _links.Join(_surface?.Links);
        Want();
        _binding.Attach(ticker, _source, _config, Redraw);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as MixerConfig ?? new MixerConfig();
        if (_attached)
        {
            Want();
            _binding.Apply(_source, _config, Redraw, show: false);
        }
    }

    public void ApplySize(WidgetArea area)
    {
        _area = area;
        _binding.Size(area);
    }

    /// <inheritdoc />
    public IReadOnlyList<WidgetAction> ContextActions => _config.DeviceId is null
        ? []
        : [new WidgetAction(Glyphs.Speakers, MixerStr.WidgetsB_Mixer_FollowDefault.Text(), () => PickDevice(null))];

    public void Detach()
    {
        _attached = false;
        _feed.Dispose();
        _binding.Dispose();
        _look.Forget();
        _links.Leave();
    }

    private void Want()
    {
        if (!_frozen)
        {
            _feed.Want(new AudioWant(_config.DeviceId, Sessions: true));
        }
    }

    private void OnSnapshot(AudioSnapshot snapshot)
    {
        if (_attached)
        {
            _snapshot = snapshot;
            _source.Raise();
        }
    }

    /// <summary>The row on screen under <paramref name="key"/>, with how it stands.</summary>
    private (MixerRow Row, bool Dimmed, bool Available)? RowOf(string key) => _shown.TryGetValue(key, out var row) ? row : null;

    /// <summary>
    /// [S-449] [S-11309] The rows: the playing apps, then the quiet ones under them while the card's rows are left; no
    /// device, the system sounds' row disabled, the reason on hover ([S-448], [S-1221], [S-1940]).
    /// </summary>
    private IReadOnlyList<KitItem>? Rows()
    {
        if (_snapshot is not { } snapshot)
        {
            return null;
        }

        _unavailable = snapshot.Status switch
        {
            AudioStatus.Ready => null,
            AudioStatus.DeviceMissing => MixerStr.WidgetsB_Mixer_DeviceMissing.Format(("device", AudioRules.DeviceName(_config.DeviceId, snapshot.Devices, _config.DeviceName))),
            AudioStatus.NoDevice => MixerStr.WidgetsB_Mixer_NoDevice.Text(),
            _ => MixerStr.WidgetsB_Mixer_Unavailable.Text(),
        };
        UnavailableLook.Say(this, _unavailable);

        var shown = new Dictionary<string, (MixerRow Row, bool Dimmed, bool Available)>(StringComparer.Ordinal);
        if (_unavailable is not null)
        {
            _overrides.Clear();
            shown[MixerRules.StandIn.Key] = (MixerRules.StandIn, false, false);
        }
        else
        {
            var options = new MixerRowOptions(_config.ShowSystemSounds, _config.OnlyActive);
            var grouped = MixerRules.Group(snapshot.Sessions, _config.GroupByApp);
            var main = MixerRules.BuildRows(grouped, options);
            var quiet = MixerRules.QuietRows(grouped, options);

            // [S-446] Keep each dragged level until a snapshot confirms it or its row is gone.
            _overrides = MixerRules.ReconcileOverrides(_overrides, [.. main, .. quiet]);
            main = MixerRules.ApplyOverrides(main, _overrides);
            quiet = MixerRules.ApplyOverrides(quiet, _overrides);

            // [S-1228] The quiet rows only while the card has rows left under the playing ones.
            var lines = Math.Clamp(_config.Lines, 1, 2);
            var rows = _area.IsMeasured ? KitPlanner.Reach(_area, lines).Shown : MixerLayout.ClampRows(_config.Rows);
            var plan = MixerLayout.Plan(_area.IsMeasured ? _area.Width : MixerLayout.NaturalWidth, _config.ShowIcons, lines, rows, main.Count, quiet.Count);
            foreach (var row in main)
            {
                shown[row.Key] = (row, !row.Active && !row.IsSystem, true);
            }

            if (main.Count > 0)
            {
                foreach (var row in quiet.Take(plan.Quiet))
                {
                    shown[row.Key] = (row, true, true);
                }
            }
        }

        _shown = shown;
        return [.. shown.Values.Select(s => MixerSpec.Item(s.Row))];
    }

    // ---- following the default ([S-1222]) ----------------------------------------------------

    /// <summary>
    /// [S-1614] A pick makes the device Windows' default and the mixer keeps following the default;
    /// when Windows refuses, the mixer binds to the device and a notice says why ([S-1616], [S-880]).
    /// Null picks "Follow the default output".
    /// </summary>
    private async void PickDevice(AudioDevice? device)
    {
        if (_surface is not { IsPreview: false } surface)
        {
            return;
        }

        try
        {
            if (await _feed.PickAsync(device, _config.DeviceId, _config.DeviceName, _snapshot?.DefaultId) is not { } after || !_attached)
            {
                return;
            }

            _config.DeviceId = after.DeviceId;
            _config.DeviceName = after.DeviceName;
            _overrides.Clear();
            surface.RequestSave();
            Want();
            _source.Raise();

            if (after.Notice is { } notice)
            {
                // [S-1616]/[S-880] Said once, as a notice: the widget is bound now, nothing to answer.
                Themia.App.Services.Notices.Warning(MixerStr.WidgetsB_Mixer_DefaultNotChanged.Text(), notice);
            }
        }
        catch (Exception ex)
        {
            Themia.Core.Diagnostics.Log.Error("audio", "the mixer could not change its device", ex);
        }
    }

    // ---- writes -------------------------------------------------------------------------------

    /// <summary>[S-447] The row's menu: mute or unmute, or back to 100 %.</summary>
    private void Run(KitItem item, KitAction action)
    {
        if (RowOf(item.Id) is not { Available: true } shown)
        {
            return;
        }

        switch (action.Id)
        {
            case MixerSpec.MuteId:
                OnMute(shown.Row);
                break;

            case MixerSpec.ResetId:
                OnReset(shown.Row);
                break;
        }
    }

    /// <summary>[S-446] The user moved a slider: hold the value on screen and queue the write.</summary>
    private void OnLevel(MixerRow row, double level)
    {
        if (_unavailable is not null)
        {
            return;
        }

        _overrides[row.Key] = level;

        // [S-447] A muted row dragged above zero unmutes, as the volume widget and the flyout do — a slider that moves
        // but stays silent reads as broken.
        if (row.Muted && level > 0)
        {
            _feed.SetMute(row.Key, false);
        }

        _feed.SetLevel(row.Key, level);
    }

    private void OnMute(MixerRow row)
    {
        if (_unavailable is null)
        {
            _feed.SetMute(row.Key, !row.Muted);
        }
    }

    private void OnReset(MixerRow row)
    {
        if (_unavailable is not null)
        {
            return;
        }

        _overrides[row.Key] = 1;

        if (row.Muted)
        {
            _feed.SetMute(row.Key, false);
        }

        _feed.SetLevel(row.Key, 1);
    }

    // ---- keys ([S-1225]) ------------------------------------------------------------------------

    /// <summary>
    /// [S-1225] The kit's keys (Up/Down move, Space mutes, the Menu key opens the row's menu, Escape lets go); with a row
    /// focused, Left/Right, Page Up/Down and +/- step its level and M mutes it.
    /// </summary>
    public bool HandleKey(WidgetKey key)
    {
        // [S-1940] The stand-in row takes no key: there is nothing to set.
        if (_unavailable is not null)
        {
            return false;
        }

        var index = _binding.List.CursorIndex;
        var items = _binding.List.Items;
        if (index >= 0 && index < items.Count && RowOf(items[index].Id) is { } shown)
        {
            var action = AudioRules.MixerKey(key, index, items.Count);
            switch (action.Kind)
            {
                case AudioKeyKind.Step:
                    var level = AudioRules.Step(shown.Row.Muted ? 0 : shown.Row.Level, action.Delta);
                    OnLevel(shown.Row, level);
                    _source.Raise();
                    return true;

                case AudioKeyKind.ToggleMute when key == WidgetKey.Mute:
                    OnMute(shown.Row);
                    return true;
            }
        }

        return _binding.HandleKey(key);
    }

    /// <summary>[S-1225] The mixer's audio as a kit source: its rows, nothing playing the spec's sentence.</summary>
    private sealed class MixerSource(MixerView owner) : IKitSource
    {
        public event Action? Changed;

        public bool Busy => false;

        public void Show(bool refresh)
        {
            // ignore: Core Audio says every change itself; there is nothing to ask again.
        }

        public KitRead Read()
            => owner.Rows() is { } rows
                ? new KitRead(rows, [new KitOutcome(null, FetchStatus.Ok)])
                : new KitRead([], [new KitOutcome(null, FetchStatus.Loading)]);

        public void Raise() => Changed?.Invoke();
    }
}
