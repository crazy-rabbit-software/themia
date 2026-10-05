using Glyphs = Themia.App.Shell.Glyphs;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-430] Graphics load, as the CPU and memory tiles show theirs: the reading and the history as a
/// chart inside the card's padding ([S-1824]), nothing else ([S-1762]).
/// <para>
/// One adapter fills the tile and names the card after itself. Two or more share the tile as panels,
/// each with its own name, reading and history ([S-433], [S-1207]) — a laptop's integrated and
/// discrete GPUs are two different stories, and one averaged number would tell neither.
/// </para>
/// <para>
/// [S-1201] Each panel climbs the meters' ladder (<see cref="MeterFace"/>): the load alone on a tiny
/// panel, else the history under it (beside it on a strip) growing to fill the panel.
/// </para>
/// </summary>
internal sealed partial class GpuView : Grid, IWidgetView, IWidgetSurfaceAware, IWidgetSizeAware
{
    private const string Scope = "gpu-widget";

    private readonly Grid _panels = new();
    private readonly StackPanel _empty;
    private readonly TextBlock _emptyText = new();
    private readonly List<Panel> _shown = [];

    private Ticker? _ticker;
    private IWidgetSurface? _surface;
    private GpuConfig _config = new();
    private IDisposable? _subscription;
    private TimeSpan _subscribedInterval;
    private List<int> _selection = [];
    private GpuSnapshot? _latest;
    private WidgetArea _area;
    private bool _acquired;
    private bool _detached;

    public GpuView()
    {
        _emptyText.Text = Str.WidgetsB_Gpu_NoAdapter.Text();
        _empty = Fluent.EmptyState(Glyphs.Gpu, _emptyText, out _);
        _empty.Visibility = Visibility.Collapsed;

        Children.Add(_panels);
        Children.Add(_empty);
    }

    public void Bind(IWidgetSurface surface)
    {
        _surface = surface;
    }

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _ticker = ticker;
        _detached = false;

        if (!_acquired)
        {
            GpuSampler.Acquire();
            _acquired = true;
        }

        ApplyConfig(config);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        _config = config as GpuConfig ?? new GpuConfig();

        var selection = GpuRules.ResolveAdapters(_config.Adapters);

        // [S-436] A different adapter is a different measurement: its panels — and their history —
        // start fresh. Anything else only changes what the existing panels show.
        if (!selection.SequenceEqual(_selection))
        {
            _selection = selection;
            BuildPanels();
        }

        foreach (var panel in _shown)
        {
            panel.Configure(_config, stacked: GpuRules.Stacked(_selection.Count));
        }

        if (!Resubscribe() && _latest is not null)
        {
            Present(_latest, addSample: false);
        }

        Arrange();
    }

    public void ApplySize(WidgetArea area)
    {
        _area = area;
        Arrange();
    }

    private void BuildPanels()
    {
        _panels.Children.Clear();
        _shown.Clear();

        foreach (var index in _selection)
        {
            var panel = new Panel(index);
            _shown.Add(panel);
            _panels.Children.Add(panel.Root);
        }
    }

    private bool Resubscribe()
    {
        var interval = _config.TickInterval;

        if (_subscription is not null && interval == _subscribedInterval)
        {
            return false;
        }

        _subscription?.Dispose();
        _subscribedInterval = interval;
        _subscription = _ticker?.Subscribe(interval, () => _ = SampleAsync());
        return _subscription is not null;
    }

    private async Task SampleAsync()
    {
        if (_detached)
        {
            return;
        }

        GpuSnapshot snapshot;
        try
        {
            snapshot = await GpuSampler.SampleAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "could not read the graphics adapters", ex);
            return;
        }

        // A sample that lands after the widget was torn down belongs to nobody.
        if (_detached)
        {
            return;
        }

        _latest = snapshot;
        Present(snapshot, addSample: true);
    }

    private void Present(GpuSnapshot snapshot, bool addSample)
    {
        // [S-433] No adapter at all, or none of the chosen ones present (an eGPU unplugged): said
        // as a state, not as a tile of dashes.
        var none = snapshot.Adapters.Count == 0;
        var gone = !none && _selection.All(index => index >= snapshot.Readings.Count);
        _emptyText.Text = none ? Str.WidgetsB_Gpu_NoAdapter.Text() : Str.WidgetsB_Gpu_AdapterMissing.Text();
        _empty.Visibility = none || gone ? Visibility.Visible : Visibility.Collapsed;
        _panels.Visibility = none || gone ? Visibility.Collapsed : Visibility.Visible;

        if (none || gone)
        {
            _surface?.SetHeaderTitle(null);
            return;
        }

        for (var i = 0; i < _shown.Count; i++)
        {
            var index = _selection[i];
            var reading = index < snapshot.Readings.Count ? snapshot.Readings[index] : null;

            _shown[i].Show(reading, addSample);
        }

        // [S-430] One adapter names the card; several keep the catalogue's "GPU" and name each panel.
        var single = _shown.Count == 1 && _selection[0] < snapshot.Readings.Count
            ? GpuRules.ShortName(snapshot.Readings[_selection[0]].Name)
            : null;

        _surface?.SetHeaderTitle(single);
        Arrange();
    }

    /// <summary>[S-1207] Share the tile between the panels — side by side on a strip, stacked in a column — and lay each out.</summary>
    private void Arrange()
    {
        if (!_area.IsMeasured || _shown.Count == 0)
        {
            return;
        }

        var (columns, rows) = GpuLayout.Grid(_area, _shown.Count, Fluent.Space3);

        if (_panels.ColumnDefinitions.Count != columns || _panels.RowDefinitions.Count != rows)
        {
            _panels.ColumnDefinitions.Clear();
            _panels.RowDefinitions.Clear();

            for (var c = 0; c < columns; c++)
            {
                _panels.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            for (var r = 0; r < rows; r++)
            {
                _panels.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            }
        }

        _panels.ColumnSpacing = columns > 1 ? Fluent.Space3 : 0;
        _panels.RowSpacing = rows > 1 ? Fluent.Space3 : 0;

        var cell = GpuLayout.Cell(_area, columns, rows, Fluent.Space3);

        for (var i = 0; i < _shown.Count; i++)
        {
            SetColumn(_shown[i].Root, i % columns);
            SetRow(_shown[i].Root, i / columns);
            _shown[i].Lay(cell);
        }
    }

    public void Detach()
    {
        _detached = true;
        _subscription?.Dispose();
        _subscription = null;
        _subscribedInterval = TimeSpan.Zero;

        if (_acquired)
        {
            _acquired = false;
            GpuSampler.Release();
        }
    }
}
