using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Glyphs = Themia.App.Shell.Glyphs;

namespace Themia.App.Widgets;

/// <summary>
/// [S-440] Per-application volume, as the Windows volume mixer lays it out: one row per app with its
/// icon, its name, a slider and a mute button. No master row and no device name
/// ([S-3251]): the device's own level is the volume tile's.
/// <para>
/// [S-1227]..[S-1229] A ladder, not one layout scaled: rows are compact (icon, mute), two
/// lines or one line by width, and the list takes the whole rows that fit — the rest scroll, never
/// half a row. Room left over goes to the quiet apps only-active left out, then to the rows
/// themselves ([S-1624]). The mixer is the apps' levels and nothing else: no device line or picker,
/// no microphone, no side panel at any size (choosing the output is Windows' job).
/// </para>
/// <para>
/// The rows are reused by position, never rebuilt. Each snapshot produces fresh row data, and
/// replacing the row visuals would destroy the slider under the user's pointer mid-drag — the
/// original hit exactly that and had to switch its list to position keying for the same reason.
/// </para>
/// </summary>
internal sealed partial class MixerView : Grid, IWidgetView, IWidgetSizeAware, IWidgetSurfaceAware, IWidgetKeys, IWidgetContextActions
{
    /// <summary>[S-1227] Row heights per style: fixed, so the whole-row arithmetic is exact.</summary>
    private const double CompactRowHeight = Fluent.RowHeightCompact;

    private const double TwoLineRowHeight = 48;
    private const double OneLineRowHeight = Fluent.RowHeight;

    private readonly AudioFeed _feed;
    private readonly StackPanel _list = new();
    private readonly ScrollViewer _scroller;
    private readonly StackPanel _empty;
    private readonly TextBlock _emptyText = new();
    private readonly List<RowVisual> _rows = [];

    private MixerConfig _config = new();
    private IWidgetSurface? _surface;
    private WidgetArea _area;
    private AudioSnapshot? _snapshot;
    private Dictionary<string, double> _overrides = new(StringComparer.Ordinal);

    /// <summary>The rows on screen, in order: the list's, then the quiet ones.</summary>
    private List<MixerRow> _shown = [];

    /// <summary>[S-1225] The keyboard cursor's row, by key, so it follows its row across snapshots.</summary>
    private string? _focused;

    private bool _detached;

    public MixerView()
    {
        _feed = new AudioFeed(OnSnapshot);

        _scroller = new ScrollViewer
        {
            Content = _list,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalAlignment = VerticalAlignment.Top,
        };

        _empty = Fluent.EmptyState(Glyphs.Mixer, _emptyText, out _);
        _empty.Visibility = Visibility.Collapsed;

        Children.Add(_scroller);
        Children.Add(_empty);
    }

    public void Bind(IWidgetSurface surface)
    {
        _surface = surface;
        _feed.Preview = surface.IsPreview;
    }

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _detached = false;
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
        : [new WidgetAction(Glyphs.Speakers, Str.WidgetsB_Mixer_FollowDefault.Text(), () => PickDevice(null))];

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
    /// [S-1227]..[S-1229] The ladder: the rows the mixer shows, decided for the area it has and the
    /// rows the snapshot holds. Runs on a new area, a new config and a new snapshot.
    /// </summary>
    private void Lay()
    {
        if (_detached)
        {
            return;
        }

        var style = MixerRules.RowStyle(_area.Width);

        _feed.Want(new AudioWant(_config.DeviceId, Sessions: true));

        if (_snapshot is not { } snapshot || !_area.IsMeasured)
        {
            return;
        }

        if (snapshot.Status != AudioStatus.Ready)
        {
            // [S-448] [S-1221] An intentional empty state, not a blank card.
            ShowEmpty(snapshot.Status switch
            {
                AudioStatus.DeviceMissing => Str.WidgetsB_Mixer_DeviceMissing.Format(("device", AudioRules.DeviceName(_config.DeviceId, snapshot.Devices, _config.DeviceName))),
                AudioStatus.NoDevice => Str.WidgetsB_Mixer_NoDevice.Text(),
                _ => Str.WidgetsB_Mixer_Unavailable.Text(),
            });
            return;
        }

        var options = new MixerRowOptions(_config.ShowSystemSounds, _config.OnlyActive);
        var grouped = MixerRules.Group(snapshot.Sessions, _config.GroupByApp);
        var main = MixerRules.BuildRows(grouped, options);
        var quiet = MixerRules.QuietRows(grouped, options);

        // [S-446] Keep each dragged level until a snapshot confirms it or its row is gone.
        var all = new List<MixerRow>(main.Count + quiet.Count);
        all.AddRange(main);
        all.AddRange(quiet);

        _overrides = MixerRules.ReconcileOverrides(_overrides, all);
        main = MixerRules.ApplyOverrides(main, _overrides);
        quiet = MixerRules.ApplyOverrides(quiet, _overrides);

        if (main.Count == 0)
        {
            ShowEmpty(Str.Mixer_Empty.Text());
            return;
        }

        _empty.Visibility = Visibility.Collapsed;
        _scroller.Visibility = Visibility.Visible;

        // ---- the height ladder ----------------------------------------------------------------
        var rowHeight = style switch
        {
            MixerRowStyle.Compact => CompactRowHeight,
            MixerRowStyle.TwoLine => TwoLineRowHeight,
            _ => OneLineRowHeight,
        };

        var room = new Disclosure(_area.Width, _area.Height - Fluent.Space1);
        var rows = Math.Max(1, room.TakeRows(rowHeight, main.Count));

        // [S-1228] The quiet apps only when the playing ones all fit: they never push one off.
        var quietRows = rows == main.Count ? room.TakeRows(rowHeight, quiet.Count) : 0;

        _shown = [.. main, .. quiet.Take(quietRows)];

        // [S-1624] On a tall card a one-line list that all fits shares the height rather than stopping
        // short; rows tall enough say what each app is doing under its name. The rest stays empty:
        // the mixer does not fill a large card with things that are not its rows.
        if (_area.HeightClass >= SizeClass.Expanded && style == MixerRowStyle.OneLine && rows == main.Count && quietRows == quiet.Count)
        {
            rowHeight = MixerFill.RowHeight(_area.Height - Fluent.Space1, _shown.Count, rowHeight);
        }

        ShowRows(style, rowHeight, main.Count);
        _scroller.Height = Math.Min(rows, main.Count) * rowHeight + (quietRows * rowHeight);

        RestoreFocus();
    }

    private void ShowEmpty(string message)
    {
        _emptyText.Text = message;
        _empty.Visibility = Visibility.Visible;
        _scroller.Visibility = Visibility.Collapsed;
        _shown = [];
    }

    private void ShowRows(MixerRowStyle style, double rowHeight, int mainCount)
    {
        while (_rows.Count < _shown.Count)
        {
            var visual = new RowVisual(this);
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
                visual.Arrange(style, rowHeight, _config.ShowIcons, detail: rowHeight >= MixerFill.DetailRowHeight);

                // [S-1228] An app holding a session but silent reads as such, and the quiet rows
                // under the list always do.
                visual.Show(row, _config.ShowIcons, dimmed: i >= mainCount || (!row.Active && !row.IsSystem));
            }
            else
            {
                _rows[i].Host.Visibility = Visibility.Collapsed;
            }
        }
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
                Themia.App.Services.Notices.Warning(Str.WidgetsB_Mixer_DefaultNotChanged.Text(), notice);
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
        _overrides[row.Key] = level;

        // [S-447] A muted row dragged above zero unmutes, as the volume widget and the flyout do —
        // a slider that moves but stays silent reads as broken.
        if (row.Muted && level > 0)
        {
            _feed.SetMute(row.Key, false);
        }

        _feed.SetLevel(row.Key, level);
    }

    private void OnMute(MixerRow row) => _feed.SetMute(row.Key, !row.Muted);

    private void OnReset(MixerRow row)
    {
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
        var index = _focused is { } id ? _shown.FindIndex(row => row.Key == id) : -1;
        var action = AudioRules.MixerKey(key, index >= 0 ? index : null, _shown.Count);

        switch (action.Kind)
        {
            case AudioKeyKind.Clear:
                var had = _focused is not null;
                _focused = null;
                return had;

            case AudioKeyKind.Move:
                Focus(action.Index);
                return true;

            case AudioKeyKind.Step:
                var row = _shown[action.Index];
                var level = AudioRules.Step(row.Muted ? 0 : row.Level, action.Delta);
                OnLevel(row, level);
                _rows[action.Index].Show(row with { Level = level, Muted = row.Muted && level <= 0 }, _config.ShowIcons, _rows[action.Index].Dimmed);
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
        var host = _rows[index].Host;
        host.Focus(FocusState.Keyboard);
        host.StartBringIntoView();
    }

    /// <summary>After a relayout the cursor goes back to its row; a row that left lets it go.</summary>
    private void RestoreFocus()
    {
        if (_focused is not { } id)
        {
            return;
        }

        var index = _shown.FindIndex(row => row.Key == id);
        if (index < 0)
        {
            _focused = null;
            return;
        }

        if (index < _rows.Count && _rows[index].Host.FocusState == FocusState.Unfocused)
        {
            _rows[index].Host.Focus(FocusState.Keyboard);
        }
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
    }

    private static string RowName(MixerRow row) => row.IsSystem ? Str.Mixer_SystemSounds.Text()
        : row.Name.Length > 0 ? row.Name
        : Str.Mixer_UnknownApp.Text();
}
