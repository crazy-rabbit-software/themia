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

namespace Themia.App.Widgets;

internal sealed partial class GpuView
{
    /// <summary>
    /// One adapter: its load and its history — and, when it shares the tile with other adapters, its
    /// name beside the load. Built once and rewritten in place on every sample, so the tile does not
    /// flicker as it updates; laid out again whenever its room changes. The load and the history are
    /// the hub's series for this adapter ([S-2504]).
    /// </summary>
    private sealed class Panel
    {
        /// <summary>The least history worth drawing under a reading: below it the trace is a smudge.</summary>
        private const double MinChart = Fluent.Space6;

        private static readonly Brush ReadingBrush = FluentLayout.Themed("TextFillColorPrimaryBrush", Colors.White);
        private static readonly Brush UnavailableBrush = FluentLayout.Themed("TextFillColorDisabledBrush", Colors.Gray);
        private static readonly Brush AccentBrush = FluentLayout.Themed("AccentFillColorDefaultBrush", Colors.DodgerBlue);
        private static readonly Brush CautionBrush = FluentLayout.Themed("SystemFillColorCautionBrush", Colors.Orange);
        private static readonly Brush CriticalBrush = FluentLayout.Themed("SystemFillColorCriticalBrush", Colors.Red);

        private readonly int _index;
        private readonly Grid _root = new();
        private readonly Grid _reading = new() { ColumnSpacing = Fluent.Space2 };
        private readonly TextBlock _name = Fluent.Text(FluentType.BodyStrong, "TextFillColorSecondaryBrush");
        private readonly TextBlock _hero = Fluent.Text(FluentType.Subtitle);
        private readonly SparkBand _spark;

        private GpuConfig _config = new();
        private SeriesWatch? _watch;
        private bool _stacked;
        private TypeStep _heroStep = FluentType.Subtitle;

        public Panel(int index)
        {
            _index = index;
            _spark = new SparkBand(Capacity(_config));

            _hero.Text = "--";
            _hero.Foreground = UnavailableBrush;
            _hero.VerticalAlignment = VerticalAlignment.Center;
            Typography.SetNumeralAlignment(_hero, FontNumeralAlignment.Tabular);
            _hero.TextTrimming = TextTrimming.None;

            _name.Text = Str.WidgetsB_Gpu_Indexed.Format(("index", index));
            _name.VerticalAlignment = VerticalAlignment.Center;
            _name.TextTrimming = TextTrimming.CharacterEllipsis;
            _name.MaxLines = 1;

            // [S-430] Several panels: the name leads (and gives way, trimmed) and the number keeps its
            // place at the right; one panel: the number alone.
            _reading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _reading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _reading.Margin = WidgetLayout.SideInset;
            SetColumn(_hero, 1);
            _reading.Children.Add(_name);
            _reading.Children.Add(_hero);

            _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            _root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0) });
            _root.Children.Add(_reading);
            _root.Children.Add(_spark);
        }

        public FrameworkElement Root => _root;

        public void Configure(GpuConfig config, bool stacked)
        {
            _config = config;
            _stacked = stacked;

            // [S-1200] The chart holds the configured span; a longer span shows the longer history the
            // hub holds at once.
            var capacity = Capacity(config);
            var grew = capacity > _spark.Capacity;
            _spark.Capacity = capacity;

            if (grew && _watch is not null)
            {
                _spark.Load(_watch.History(_spark.Capacity), _watch.Added);
            }

            _name.Visibility = stacked ? Visibility.Visible : Visibility.Collapsed;
            _reading.ColumnDefinitions[0].Width = stacked ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
            _reading.HorizontalAlignment = stacked ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;
        }

        /// <summary>
        /// [S-2504] Watch this adapter's series, once: the history so far and the newest load at once,
        /// each read after through the watch.
        /// </summary>
        public void Watch(SystemHub hub)
        {
            if (_watch is not null)
            {
                return;
            }

            var key = FeedKey.Create(SystemSource.Gpu);
            _watch = hub.WatchSeries(new SeriesInterest(key, _index.ToString(System.Globalization.CultureInfo.InvariantCulture)), OnUpdate);
            _spark.Load(_watch.History(_spark.Capacity), _watch.Added);

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
        /// The adapter's name, or [S-433] the adapter is gone — undocked, or the selection names one
        /// this machine does not have. Said in the panel, not by blanking the tile.
        /// </summary>
        public void Name(GpuReading? reading)
        {
            if (reading is null)
            {
                _name.Text = Str.WidgetsB_Gpu_Indexed.Format(("index", _index));
                return;
            }

            _name.Text = GpuRules.ShortName(reading.Name);
            ToolTipService.SetToolTip(_root, reading.Name);
        }

        private void OnUpdate(SeriesUpdate update)
        {
            ShowLoad(update.Reading);

            if (update.Point is { } point)
            {
                _spark.Push(point.First);
            }
        }

        private void ShowLoad(MeterReading reading)
        {
            _hero.Text = reading.Primary;
            _hero.Foreground = reading.Available ? ReadingBrush : UnavailableBrush;

            // [S-432] Pressure colours the trace, not the number: the number stays legible in every
            // state, and a trace that turns amber is seen from across the room.
            if (reading is { Available: true, Value: { } percent })
            {
                _spark.SetBrush(Pressure(GpuRules.LoadPressure(percent)));
            }
        }

        /// <summary>
        /// [S-1762] The meters' ladder for this panel's room: the load alone on a tiny panel, else the
        /// history filling what the load leaves — beside it on a strip. Several panels keep the load at
        /// one step beside their names, and give the rest to their histories.
        /// </summary>
        public void Lay(WidgetArea area)
        {
            if (!area.IsMeasured)
            {
                return;
            }

            var plan = MeterFace.Plan(area, _config.ShowGraph, Formatting.Percent(1).Length, WidgetLayout.ContentInset, Fluent.Space2, MinChart,
                _stacked ? FluentType.Subtitle : null);
            var side = plan.Arrangement == MeterFaceArrangement.Side;
            var alone = plan.Arrangement == MeterFaceArrangement.ReadingOnly;

            if (plan.Reading != _heroStep)
            {
                _heroStep = plan.Reading;
                _hero.Apply(plan.Reading);
            }

            _root.ColumnDefinitions[0].Width = side ? new GridLength(plan.ReadingColumn) : new GridLength(1, GridUnitType.Star);
            _root.ColumnDefinitions[1].Width = side ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

            SetRow(_reading, 0);
            SetRowSpan(_reading, alone || side ? 2 : 1);
            _reading.VerticalAlignment = alone || side ? VerticalAlignment.Center : VerticalAlignment.Top;

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
