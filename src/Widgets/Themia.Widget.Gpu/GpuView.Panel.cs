using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Services.SystemData;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.SystemData;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Sdk;

namespace Themia.App.Widgets;

internal sealed partial class GpuView
{
    /// <summary>
    /// The adapter's load and its history. Built once and rewritten in place on every sample, so the
    /// tile does not flicker as it updates; laid out again whenever its room changes. The load and
    /// the history are the hub's series for this adapter ([S-2504]).
    /// </summary>
    private sealed partial class Panel
    {
        /// <summary>The least history worth drawing under a reading: below it the trace is a smudge.</summary>
        private const double MinChart = Fluent.Space6;

        private static readonly Brush ReadingBrush = ThemeBrush.Of("TextFillColorPrimaryBrush", Colors.White);
        private static readonly Brush UnavailableBrush = ThemeBrush.Of("TextFillColorDisabledBrush", Colors.Gray);
        private static readonly Brush AccentBrush = ThemeBrush.Of("AccentFillColorDefaultBrush", Colors.DodgerBlue);
        private static readonly Brush CautionBrush = ThemeBrush.Of("SystemFillColorCautionBrush", Colors.Orange);
        private static readonly Brush CriticalBrush = ThemeBrush.Of("SystemFillColorCriticalBrush", Colors.Red);

        private readonly int _index;
        private readonly Grid _root = new();
        private readonly Grid _reading = new();
        private readonly HeroReadout _hero = new();
        private readonly SparkBand _spark;

        private GpuConfig _config = new();
        private ISeriesWatch? _watch;
        private TypeStep? _heroStep;
        private string _shown = string.Empty;

        // [S-1940] No adapter at this index: drawn absent until a snapshot names one again.
        private bool _absent;
        private string? _absentReason;

        public Panel(int index)
        {
            _index = index;
            _spark = new SparkBand(Capacity(_config));

            // [S-3860] The shared hero readout: the number and its smaller unit, as every meter's.
            _hero.SetValue("--", string.Empty, false);
            _hero.ValueLine.Foreground = UnavailableBrush;
            _hero.ValueLine.TextTrimming = TextTrimming.None;
            _hero.VerticalAlignment = VerticalAlignment.Center;

            _reading.Margin = new Thickness(HeroLayout.SideInset, HeroLayout.TopInset, HeroLayout.SideInset, 0);
            _reading.HorizontalAlignment = HorizontalAlignment.Left;
            _reading.Children.Add(_hero);

            _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            _root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0) });
            _root.Children.Add(_reading);
            _root.Children.Add(_spark);
        }

        public FrameworkElement Root => _root;

        public int Index => _index;

        public void Configure(GpuConfig config)
        {
            _config = config;

            // [S-1200] The chart holds the configured span; a longer span shows the longer history the
            // hub holds at once.
            var capacity = Capacity(config);
            var grew = capacity > _spark.Capacity;
            _spark.Capacity = capacity;

            if (grew && _watch is not null)
            {
                _spark.Load(_watch.History(_spark.Holds), _watch.Added);
            }

            UseStyle(config.Style);
        }

        /// <summary>
        /// [S-2504] Watch this adapter's series, once: the history so far and the newest load at once,
        /// each read after through the watch.
        /// </summary>
        public void Watch(ISystemFeeds hub)
        {
            if (_watch is not null)
            {
                return;
            }

            var key = FeedKey.Create(SystemSource.Gpu);
            _watch = hub.WatchSeries(new SeriesInterest(key, _index.ToString(System.Globalization.CultureInfo.InvariantCulture)), OnUpdate);
            _spark.Load(_watch.History(_spark.Holds), _watch.Added);

            if (_watch.Latest is { } latest)
            {
                ShowLoad(latest);
            }
        }

        /// <summary>[S-1121] The span in points: the GPU is read every tick.</summary>
        private static int Capacity(GpuConfig config) => MeterTiming.Points(config.GraphSpanSeconds, SystemCadence.PeriodSeconds(SystemSource.Gpu));

        public void Unwatch()
        {
            _watch?.Dispose();
            _watch = null;
        }

        /// <summary>
        /// [S-430] The adapter's whole name, as the driver reports it, on the tile's tooltip (the header
        /// has the short one). True when the adapter came back: the tile is laid out again ([S-6332]).
        /// </summary>
        public bool Name(GpuReading reading)
        {
            var back = _absent;

            if (_absent)
            {
                // The stand-in line goes with the adapter's return; its own series fills the chart again.
                _absent = false;
                _absentReason = null;
                _spark.Clear();
            }

            UnavailableLook.Say(_root, reading.Name);
            return back;
        }

        /// <summary>
        /// [S-433] [S-1940] No adapter at all, or not the chosen one: the same tile in the disabled look —
        /// "--", the history a flat line at the middle, the gauge at the middle ([S-1941]) — the reason on hover.
        /// </summary>
        public bool Absent(string reason)
        {
            // [S-6332] Absent already for this reason: every snapshot says so again, nothing changes.
            if (_absent && reason == _absentReason)
            {
                return false;
            }

            // The adapter's series reads nothing while it is not there; its readings do not undo this.
            _absent = true;
            _absentReason = reason;
            _spark.Load(Unavailable.History(_spark.Holds));
            ShowLoad(MeterReading.Missing(reason));
            ShowAbsentEngines();
            UnavailableLook.Say(_root, reason);
            return true;
        }

        private void OnUpdate(SeriesUpdate update)
        {
            if (_absent)
            {
                return;
            }

            ShowLoad(update.Reading);

            if (update.Point is { } point)
            {
                _spark.Push(point.First);
            }
        }

        private void ShowLoad(MeterReading reading)
        {
            // [S-6330] The reading's own text, at once.
            ShowText(reading.Primary);

            _hero.ValueLine.Foreground = reading.Available ? ReadingBrush : UnavailableBrush;

            // [S-432] Pressure colours the trace, not the number: the number stays legible in every
            // state, and a trace that turns amber is seen from across the room.
            if (reading is { Available: true, Value: { } percent })
            {
                _spark.SetBrush(Pressure(GpuRules.LoadPressure(percent)));
                _dial?.SetBrush(Pressure(GpuRules.LoadPressure(percent)));
            }
            else if (reading.Absent)
            {
                _spark.SetBrush(UnavailableLook.Fill);
                _dial?.SetBrush(UnavailableLook.Fill);
            }

            // [S-6302] The gauge is the load's own share; [S-1941] the middle for an adapter not there.
            _dial?.SetFraction(Unavailable.Fraction(reading));
        }

        /// <summary>[S-3860] The number and its smaller unit, split again only when the text changed.</summary>
        private void ShowText(string primary)
        {
            if (primary != _shown)
            {
                _shown = primary;
                var text = HeroLayout.Split(primary);
                _hero.SetValue(text.Value, text.Unit, text.Spaced);
            }
        }

        /// <summary>
        /// [S-1762] The meters' ladder for the tile: the load alone on a tiny tile, else the history
        /// filling what the load leaves — beside it on a strip.
        /// </summary>
        public void Lay(WidgetArea area)
        {
            if (!area.IsMeasured)
            {
                return;
            }

            _area = area;

            // [S-6321] A style other than the history lays its own part on the same ladder.
            if (_style != GpuStyle.Graph)
            {
                _spark.Visibility = Visibility.Collapsed;

                if (_style == GpuStyle.Gauge)
                {
                    LayGauge(area);
                }
                else
                {
                    LayEngines(area);
                }

                return;
            }

            _reading.HorizontalAlignment = HorizontalAlignment.Left;
            SetColumnSpan(_reading, 1);

            var plan = MeterFace.Plan(area, _config.ShowGraph, HeroBound.Of(Formatting.Percent(1)), false, Fluent.Space2, MinChart, null);
            var side = plan.Arrangement == MeterFaceArrangement.Side;
            var alone = plan.Arrangement == MeterFaceArrangement.ReadingOnly;

            if (plan.Reading != _heroStep)
            {
                _heroStep = plan.Reading;
                _hero.Lay(plan.Reading, icon: false, trailingBeside: true, linesBeside: false);
            }

            _root.ColumnDefinitions[0].Width = side ? new GridLength(plan.ReadingColumn) : new GridLength(1, GridUnitType.Star);
            _root.ColumnDefinitions[1].Width = side ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

            SetRow(_reading, 0);
            SetRowSpan(_reading, alone || side ? 2 : 1);
            _reading.VerticalAlignment = alone || side ? VerticalAlignment.Center : VerticalAlignment.Top;
            _reading.Margin = alone || side
                ? new Thickness(HeroLayout.SideInset, 0, HeroLayout.SideInset, 0)
                : new Thickness(HeroLayout.SideInset, HeroLayout.TopInset, HeroLayout.SideInset, 0);

            _spark.Visibility = plan.Graph ? Visibility.Visible : Visibility.Collapsed;
            SetRow(_spark, side ? 0 : 1);
            SetRowSpan(_spark, side ? 2 : 1);
            SetColumn(_spark, side ? 1 : 0);

            // [S-1824] Edge to edge, as every meter's history.
            _spark.Margin = side ? new Thickness(0) : new Thickness(0, Fluent.Space2, 0, 0);
        }

        private static Brush Pressure(GpuPressure pressure) => pressure switch
        {
            GpuPressure.Critical => CriticalBrush,
            GpuPressure.High => CautionBrush,
            _ => AccentBrush,
        };
    }
}
