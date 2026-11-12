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
/// [S-440] Per-application volume, as the Windows volume mixer lays it out: one row per app with its
/// icon, its name, a slider and a mute button. No master row and no device name
/// ([S-3251]): the device's own level is the volume tile's.
/// <para>
/// [S-449] The rows are the family's Row block ([S-6711]): 28 or 40 tall by the tier, as many whole
/// rows as the area holds, the rest a scroll away, never half a row (<see cref="MixerLayout"/>). A short
/// list on a large card stays a short list ([S-666]): the mixer is the apps' levels and nothing else.
/// Nothing playing is the Status row at the top; no device is the stand-in row in the disabled role
/// ([S-448], [S-1940]).
/// </para>
/// <para>
/// The rows are reused by position, never rebuilt. Each snapshot produces fresh row data, and
/// replacing the row visuals would destroy the slider under the user's pointer mid-drag — the
/// original hit exactly that and had to switch its list to position keying for the same reason.
/// </para>
/// </summary>
internal sealed partial class MixerView : Grid, IWidgetView, IWidgetOpens, IWidgetSizeAware, IWidgetSurfaceAware, IWidgetKeys, IWidgetContextActions
{
    /// <inheritdoc />
    public CardOpen? Opens => MixerModule.Opens;

    private readonly AudioFeed _feed;
    private readonly StackPanel _list = new();
    private readonly ScrollViewer _scroller;
    private readonly StatusBlock _status = new() { VerticalAlignment = VerticalAlignment.Top };
    private readonly List<RowVisual> _rows = [];

    /// <summary>Whether the rows were built with their icon's lead: a change of App icons builds them again.</summary>
    private bool _rowsLead = true;

    private MixerConfig _config = new();
    private IWidgetSurface? _surface;
    private WidgetArea _area;
    private AudioSnapshot? _snapshot;
    private Dictionary<string, double> _overrides = new(StringComparer.Ordinal);

    /// <summary>The rows on screen, in order: the list's, then the quiet ones.</summary>
    private List<MixerRow> _shown = [];

    /// <summary>[S-1225] The keyboard cursor's row, by key, so it follows its row across snapshots.</summary>
    private string? _focused;

    /// <summary>[S-1940] Why there is no device to mix (none, the chosen one gone, Core Audio silent), or null while there is.</summary>
    private string? _unavailable;

    private bool _detached;

    // [S-11850] A sample's snapshot stands still; the live feed is not asked again over it.
    private bool _frozen;

    public MixerView(IWidgetHost host)
    {
        _feed = new AudioFeed(host.Audio, OnSnapshot);

        _scroller = new ScrollViewer
        {
            Content = _list,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalAlignment = VerticalAlignment.Top,
        };

        _status.Visibility = Visibility.Collapsed;
        Children.Add(_scroller);
        Children.Add(_status);
    }

    public void Bind(IWidgetSurface surface)
    {
        _surface = surface;
        _feed.Preview = surface.IsPreview;
    }

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _detached = false;
        _links.Join(_surface?.Links);
        ApplyConfig(config);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as MixerConfig ?? new MixerConfig();
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
        : [new WidgetAction(Glyphs.Speakers, MixerStr.WidgetsB_Mixer_FollowDefault.Text(), () => PickDevice(null))];

    private void OnSnapshot(AudioSnapshot snapshot)
    {
        if (_detached)
        {
            return;
        }

        _snapshot = snapshot;
        Lay();
    }

    /// <summary>
    /// [S-449] The ladder: the rows the mixer shows, decided for the area it has and the rows the
    /// snapshot holds. Runs on a new area, a new config and a new snapshot.
    /// </summary>
    private void Lay()
    {
        if (_detached)
        {
            return;
        }

        if (!_frozen)
        {
            _feed.Want(new AudioWant(_config.DeviceId, Sessions: true));
        }

        if (_snapshot is not { } snapshot)
        {
            return;
        }

        // [S-448] [S-1221] [S-1940] No device to mix: the system sounds' row, disabled at the middle,
        // the reason on hover — the mixer's own content, never a sentence in its place.
        _unavailable = snapshot.Status switch
        {
            AudioStatus.Ready => null,
            AudioStatus.DeviceMissing => MixerStr.WidgetsB_Mixer_DeviceMissing.Format(("device", AudioRules.DeviceName(_config.DeviceId, snapshot.Devices, _config.DeviceName))),
            AudioStatus.NoDevice => MixerStr.WidgetsB_Mixer_NoDevice.Text(),
            _ => MixerStr.WidgetsB_Mixer_Unavailable.Text(),
        };
        UnavailableLook.Say(this, _unavailable);

        List<MixerRow> main;
        List<MixerRow> quiet;

        if (_unavailable is not null)
        {
            main = [MixerRules.StandIn];
            quiet = [];
            _overrides.Clear();
        }
        else
        {
            var options = new MixerRowOptions(_config.ShowSystemSounds, _config.OnlyActive);
            var grouped = MixerRules.Group(snapshot.Sessions, _config.GroupByApp);
            main = MixerRules.BuildRows(grouped, options);
            quiet = MixerRules.QuietRows(grouped, options);

            // [S-446] Keep each dragged level until a snapshot confirms it or its row is gone.
            var all = new List<MixerRow>(main.Count + quiet.Count);
            all.AddRange(main);
            all.AddRange(quiet);

            _overrides = MixerRules.ReconcileOverrides(_overrides, all);
            main = MixerRules.ApplyOverrides(main, _overrides);
            quiet = MixerRules.ApplyOverrides(quiet, _overrides);
        }

        if (main.Count == 0)
        {
            // [S-448] Nothing playing on a device that is there: the Status row where the first row would be.
            _status.Show(StatusKind.Empty, Glyphs.Mixer, MixerStr.Mixer_Empty.Text());
            _scroller.Visibility = Visibility.Collapsed;
            _shown = [];
            return;
        }

        _status.Hide();
        _scroller.Visibility = Visibility.Visible;

        // [S-449] [S-11309] The config's rows: the playing apps, then the quiet ones under them while rows are left.
        var plan = MixerLayout.Plan(_area.IsMeasured ? _area.Width : MixerLayout.NaturalWidth, _config.ShowIcons, _config.Lines, _config.Rows, main.Count, quiet.Count);
        _shown = [.. main, .. quiet.Take(plan.Quiet)];

        ShowRows(plan, main.Count);
        _scroller.Height = plan.Rows * FamilyShape.RowHeight(plan.Lines);

        RestoreFocus();
    }

    private void ShowRows(MixerPlan plan, int mainCount)
    {
        // [S-446] The lead's size is fixed when a row is made: App icons on or off makes them again.
        if (_rowsLead != _config.ShowIcons)
        {
            foreach (var row in _rows)
            {
                row.Detach();
            }

            _rows.Clear();
            _list.Children.Clear();
            Apps.Clear();
            _rowsLead = _config.ShowIcons;
        }

        while (_rows.Count < _shown.Count)
        {
            var visual = new RowVisual(this, _config.ShowIcons);
            _rows.Add(visual);
            _list.Children.Add(visual.Host);
        }

        for (var i = 0; i < _rows.Count; i++)
        {
            if (i < _shown.Count)
            {
                var row = _shown[i];
                var visual = _rows[i];
                visual.Host.Visibility = Visibility.Visible;
                visual.Arrange(plan);

                // [S-1228] An app holding a session but silent says so, and the quiet rows under the list always do.
                visual.Show(row, dimmed: i >= mainCount || (!row.Active && !row.IsSystem), available: _unavailable is null);
            }
            else
            {
                _rows[i].Host.Visibility = Visibility.Collapsed;
            }
        }

        LinkRows();
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
                Themia.App.Services.Notices.Warning(MixerStr.WidgetsB_Mixer_DefaultNotChanged.Text(), notice);
            }
        }
        catch (Exception ex)
        {
            Themia.Core.Diagnostics.Log.Error("audio", "the mixer could not change its device", ex);
        }
    }

    // ---- writes -------------------------------------------------------------------------------

    /// <summary>[S-446] The user moved a slider: hold the value on screen and queue the write.</summary>
    private void OnLevel(MixerRow row, double level)
    {
        if (_unavailable is not null)
        {
            return;
        }

        _overrides[row.Key] = level;

        // [S-447] A muted row dragged above zero unmutes, as the volume widget and the flyout do —
        // a slider that moves but stays silent reads as broken.
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

    public bool HandleKey(WidgetKey key)
    {
        // [S-1940] The stand-in row takes no key: there is nothing to set.
        if (_unavailable is not null)
        {
            return false;
        }

        var index = _focused is { } id ? _shown.FindIndex(row => row.Key == id) : -1;
        var action = AudioRules.MixerKey(key, index >= 0 ? index : null, _shown.Count);

        switch (action.Kind)
        {
            case AudioKeyKind.Clear:
                var had = _focused is not null;
                _focused = null;
                Ring(-1);
                PointCursor(-1);
                return had;

            case AudioKeyKind.Move:
                Focus(action.Index);
                return true;

            case AudioKeyKind.Step:
                var row = _shown[action.Index];
                var level = AudioRules.Step(row.Muted ? 0 : row.Level, action.Delta);
                OnLevel(row, level);
                _rows[action.Index].Show(row with { Level = level, Muted = row.Muted && level <= 0 }, _rows[action.Index].Dimmed, available: true);
                return true;

            case AudioKeyKind.ToggleMute:
                OnMute(_shown[action.Index]);
                return true;

            case AudioKeyKind.Menu:
                _rows[action.Index].OpenMenu(null);
                return true;

            default:
                return false;
        }
    }

    private void Focus(int index)
    {
        if (index < 0 || index >= _shown.Count || index >= _rows.Count)
        {
            return;
        }

        _focused = _shown[index].Key;
        Ring(index);
        PointCursor(index);
    }

    /// <summary>[S-1225] The keyboard cursor is the Row block's ring on its row ([S-1288]); the others' ring is clear.</summary>
    private void Ring(int index)
    {
        for (var i = 0; i < _rows.Count; i++)
        {
            _rows[i].Host.Focused = i == index;
        }
    }

    /// <summary>After a relayout the cursor goes back to its row; a row that left lets it go.</summary>
    private void RestoreFocus()
    {
        if (_focused is not { } id)
        {
            Ring(-1);
            return;
        }

        var index = _shown.FindIndex(row => row.Key == id);
        if (index < 0)
        {
            _focused = null;
        }

        Ring(index);
    }

    public void Detach()
    {
        _detached = true;
        _feed.Dispose();

        foreach (var row in _rows)
        {
            row.Detach();
        }

        _rows.Clear();
        _list.Children.Clear();
        _focused = null;
        _links.Leave();
        Apps.Clear();
    }

    private static string RowName(MixerRow row) => row.IsSystem ? MixerStr.Mixer_SystemSounds.Text()
        : row.Name.Length > 0 ? row.Name
        : MixerStr.Mixer_UnknownApp.Text();
}
