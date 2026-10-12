using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-3200] [S-3210] The audio visualizer: the spectrum of whatever the PC is playing, and nothing
/// else, in the design its kind names; one view and one drawing (<see cref="SpectrumCanvas"/>) for all six.
/// [S-3212] The waveform design draws the frame's trace instead of its bands; it holds the same capture.
/// <para>
/// The bars come from the one shared loopback capture (<see cref="AudioLoopback"/>, [S-3203]),
/// held exactly while this tile can be seen: attached, in the tree, not a preview. Game mode
/// detaches every widget, so it lets go too. In silence the bars rest on a flat baseline
/// ([S-3206]); only a device the capture cannot reach puts a sentence in their place ([S-3204]).
/// </para>
/// </summary>
internal sealed partial class AudioVisualizerView : Grid, IWidgetView, IWidgetSizeAware, IWidgetSurfaceAware
{
    private const string Scope = "visualizer";

    private readonly VisualizerStyle _style;
    private readonly SpectrumCanvas _bars;
    private readonly StackPanel _unavailable;

    private DispatcherQueue? _queue;
    private IDisposable? _loopback;
    private AudioFrame? _pending;
    private int _drawQueued;
    private bool _detached = true;
    private bool _preview;

    public AudioVisualizerView(WidgetKind kind)
    {
        _style = VisualizerLayout.StyleOf(kind) ?? VisualizerStyle.Bars;
        _bars = new SpectrumCanvas(_style);

        // [S-1113] A spectrum is an axis (low to high), not text: left to right in every language.
        FlowDirection = Microsoft.UI.Xaml.FlowDirection.LeftToRight;

        var message = new TextBlock { Text = Str.WidgetsC_Visualizer_Unavailable.Text() };
        _unavailable = Fluent.EmptyState(WidgetCatalog.Definition(kind).Glyph, message, out _);
        _unavailable.Visibility = Visibility.Collapsed;

        Children.Add(_bars);
        Children.Add(_unavailable);

        Loaded += (_, _) => SyncCapture();
        Unloaded += (_, _) => SyncCapture();
    }

    public void Bind(IWidgetSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        _preview = surface.IsPreview;
        SyncCapture();
    }

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _detached = false;
        _queue = DispatcherQueue;
        SyncCapture();
    }

    // Nothing to read: the widget has no settings of its own ([S-3200]).
    public void ApplyConfig(WidgetConfig config)
    {
    }

    public void ApplySize(WidgetArea area) => _bars.Arrange(VisualizerLayout.Plan(area, _style));

    /// <summary>
    /// [S-3203] Hold the capture while the bars can be seen, and let it go otherwise; the last tile
    /// to let go stops the capture thread.
    /// </summary>
    private void SyncCapture()
    {
        var wanted = !_detached && !_preview && IsLoaded;

        if (wanted && _loopback is null)
        {
            _loopback = AudioLoopback.Subscribe(OnFrame, OnAvailability);
        }
        else if (!wanted && _loopback is not null)
        {
            _loopback.Dispose();
            _loopback = null;
            _bars.Clear();
        }
    }

    /// <summary>On the capture thread: keep the newest frame, and queue one draw if none is waiting.</summary>
    private void OnFrame(AudioFrame frame)
    {
        Volatile.Write(ref _pending, frame);

        if (Interlocked.Exchange(ref _drawQueued, 1) == 0 && _queue?.TryEnqueue(DrawFrame) != true)
        {
            Interlocked.Exchange(ref _drawQueued, 0);
        }
    }

    private void DrawFrame()
    {
        Interlocked.Exchange(ref _drawQueued, 0);

        if (_loopback is not null && Volatile.Read(ref _pending) is { } frame)
        {
            _bars.SetFrame(frame);
        }
    }

    /// <summary>On the capture thread: whether the output device can be captured. Drawn on the UI thread.</summary>
    private void OnAvailability(bool available)
    {
        if (_queue?.TryEnqueue(() => ShowAvailability(available)) != true)
        {
            Log.Debug(Scope, "an availability change arrived with no dispatcher to draw it on");
        }
    }

    /// <summary>[S-3204] The sentence instead of the bars only while the device cannot be reached.</summary>
    private void ShowAvailability(bool available)
    {
        if (_detached)
        {
            return;
        }

        _bars.Visibility = available ? Visibility.Visible : Visibility.Collapsed;
        _unavailable.Visibility = available ? Visibility.Collapsed : Visibility.Visible;

        if (!available)
        {
            _bars.Clear();
        }
    }

    public void Detach()
    {
        _detached = true;
        SyncCapture();
        _queue = null;
    }
}
