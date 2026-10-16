using Glyphs = Themia.App.Shell.Glyphs;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Services.SystemData;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-430] Graphics load, as the CPU and memory tiles show theirs: the reading and the history as a
/// chart inside the card's padding ([S-1824]), nothing else ([S-1762]). One adapter per widget,
/// chosen in its settings ([S-433]); the card is named after it. A laptop's integrated and discrete
/// GPUs are two widgets: one averaged number would tell neither story.
/// <para>
/// [S-1201] The tile climbs the meters' ladder (<see cref="MeterFace"/>): the load alone on a tiny
/// tile, else the history under it (beside it on a strip) growing to fill the tile.
/// </para>
/// <para>
/// [S-2500] [S-2504] The samples are the system hub's GPU feed: the adapter's name from its
/// snapshots, the load and history from its adapter's series, so a tile built again shows the
/// history the hub kept.
/// </para>
/// </summary>
internal sealed partial class GpuView : Grid, IWidgetView, IWidgetSurfaceAware, IWidgetSizeAware
{
    private const string Scope = "gpu-widget";

    private readonly StackPanel _empty;
    private readonly TextBlock _emptyText = new();

    private IWidgetSurface? _surface;
    private GpuConfig _config = new();
    private FeedWatch<GpuSnapshot>? _snapshots;
    private Panel? _panel;
    private WidgetArea _area;

    // [S-1881] Until Attach: a parked view watches nothing, whatever config it is handed.
    private bool _detached = true;

    public GpuView()
    {
        _emptyText.Text = Str.WidgetsB_Gpu_NoAdapter.Text();
        _empty = Fluent.EmptyState(Glyphs.Gpu, _emptyText, out _);
        _empty.Visibility = Visibility.Collapsed;

        Children.Add(_empty);
    }

    public void Bind(IWidgetSurface surface)
    {
        _surface = surface;
    }

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _detached = false;
        ApplyConfig(config);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as GpuConfig ?? new GpuConfig();
        var index = Math.Max(0, _config.Adapter);

        // [S-436] A different adapter is a different measurement: its own series, its history fresh.
        // Anything else only changes what the panel shows.
        var rebuilt = _panel is null || _panel.Index != index;

        if (rebuilt)
        {
            if (_panel is not null)
            {
                _panel.Unwatch();
                Children.Remove(_panel.Root);
            }

            _panel = new Panel(index);
            Children.Insert(0, _panel.Root);
        }

        _panel!.Configure(_config);
        Rewatch(rebuilt);

        if (_snapshots is { HasValue: true } watch)
        {
            Present(watch.Value);
        }

        Arrange();
    }

    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Arrange();
    }

    /// <summary>
    /// [S-2504] Watch the GPU feed, and the panel its adapter's series — only when nothing is watched
    /// yet or the adapter changed, so a settings slider does not drop the watches.
    /// </summary>
    private void Rewatch(bool adapterChanged)
    {
        if (_detached)
        {
            return;
        }

        if (_snapshots is not null && !adapterChanged)
        {
            return;
        }

        if (AppServices.System is not { } hub)
        {
            Log.Warn(Scope, "no system hub for the GPU widget");
            return;
        }

        _snapshots ??= hub.Watch(Sources.Gpu, OnSnapshot);
        _panel?.Watch(hub);
    }

    private void OnSnapshot(GpuSnapshot snapshot)
    {
        // A sample that lands after the widget was torn down belongs to nobody.
        if (!_detached)
        {
            Present(snapshot);
        }
    }

    private void Present(GpuSnapshot snapshot)
    {
        if (_panel is not { } panel)
        {
            return;
        }

        // [S-433] No adapter at all, or not the chosen one (an eGPU unplugged): said as a state, not
        // as a tile of dashes.
        var none = snapshot.Adapters.Count == 0;
        var gone = !none && panel.Index >= snapshot.Readings.Count;
        _emptyText.Text = none ? Str.WidgetsB_Gpu_NoAdapter.Text() : Str.WidgetsB_Gpu_AdapterMissing.Text();
        _empty.Visibility = none || gone ? Visibility.Visible : Visibility.Collapsed;
        panel.Root.Visibility = none || gone ? Visibility.Collapsed : Visibility.Visible;

        if (none || gone)
        {
            _surface?.SetHeaderTitle(null);
            return;
        }

        var reading = snapshot.Readings[panel.Index];
        panel.Name(reading);
        panel.ShowEngines(reading.Engines);

        // [S-430] The adapter names the card.
        _surface?.SetHeaderTitle(GpuRules.ShortName(reading.Name));
        Arrange();
    }

    private void Arrange()
    {
        if (_area.IsMeasured)
        {
            _panel?.Lay(_area);
        }
    }

    public void Detach()
    {
        _detached = true;

        // [S-2513] The adapter's history stays in the hub for the tile built in this one's place.
        _snapshots?.Dispose();
        _snapshots = null;
        _panel?.Unwatch();
    }
}
