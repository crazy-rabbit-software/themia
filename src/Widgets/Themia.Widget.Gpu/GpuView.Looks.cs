using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Widget.Gpu;

namespace Themia.App.Widgets;

internal sealed partial class GpuView
{
    /// <summary>
    /// [S-6321] A panel's styles other than the history: the load inside a gauge, or a bar per
    /// engine type under it. Built with the meters' shared parts (<see cref="GaugeDial"/>,
    /// <see cref="BarList"/>) on the meters' ladder, so a GPU gauge and a CPU gauge of one size
    /// are one size; what one style builds is released when another is chosen ([S-6005]).
    /// </summary>
    private sealed partial class Panel
    {
        private const double BodyBottom = Fluent.Space3;

        private GpuStyle _style;
        private FrameworkElement? _body;
        private GaugeDial? _dial;
        private BarList? _engines;
        private IReadOnlyList<GpuEngineLoad> _engineLoads = [];
        private WidgetArea _area;

        private void UseStyle(GpuStyle style)
        {
            if (style == _style && (_body is not null || style == GpuStyle.Graph))
            {
                return;
            }

            _style = style;

            if (_body is not null)
            {
                _root.Children.Remove(_body);
            }

            _dial = null;
            _engines = null;
            _body = style switch
            {
                GpuStyle.Gauge => _dial = new GaugeDial(),
                GpuStyle.Engines => _engines = new BarList(BarListShape.Rows),
                _ => null,
            };

            if (_body is not null)
            {
                _root.Children.Insert(0, _body);
            }

            _heroStep = null;

            if (_engines is not null)
            {
                ShowEngines(_engineLoads);
            }
        }

        /// <summary>[S-6322] The adapter's engine types as the newest sample has them; a different set is a different layout.</summary>
        public void ShowEngines(IReadOnlyList<GpuEngineLoad> engines)
        {
            var recount = engines.Count != _engineLoads.Count;
            _engineLoads = engines;

            if (_engines is null)
            {
                return;
            }

            _engines.SetCount(engines.Count);

            for (var i = 0; i < engines.Count; i++)
            {
                var load = engines[i];
                _engines.SetItem(i, load.Percent / 100, Pressure(GpuRules.LoadPressure(load.Percent)), Label(load.Kind), Formatting.Percent(load.Percent / 100));
            }

            if (recount && _area.IsMeasured)
            {
                Lay(_area);
            }
        }

        /// <summary>[S-6304] The widest engine line: the longest label and "100%".</summary>
        private static double LineCharacters
            => Enum.GetValues<GpuEngineKind>().Max(k => Label(k).Length) + Formatting.Percent(1).Length;

        private static string Label(GpuEngineKind kind) => kind switch
        {
            GpuEngineKind.ThreeD => GpuStr.Styles_Gpu_Engine3d.Text(),
            GpuEngineKind.Copy => GpuStr.Styles_Gpu_EngineCopy.Text(),
            GpuEngineKind.VideoEncode => GpuStr.Styles_Gpu_EngineVideoEncode.Text(),
            _ => GpuStr.Styles_Gpu_EngineVideoDecode.Text(),
        };

        /// <summary>
        /// [S-6321] The gauge as large as the tile allows, the load in its bowl.
        /// </summary>
        private void LayGauge(WidgetArea area)
        {
            var top = HeroLayout.TopInset;
            var boxWidth = Math.Max(0, area.Width - (2 * HeroLayout.SideInset));
            var boxHeight = Math.Max(0, area.Height - top - BodyBottom);
            var gauge = GaugeLayout.Fit(boxWidth, boxHeight);
            var bound = HeroBound.Of(Formatting.Percent(1));

            _root.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
            _root.ColumnDefinitions[1].Width = new GridLength(0);
            SetRow(_reading, 0);
            SetColumnSpan(_reading, 2);

            if (gauge is not { } g || _dial is null)
            {
                if (_dial is not null)
                {
                    _dial.Visibility = Visibility.Collapsed;
                }

                // Too small for a gauge: the load alone, as the history's tiny panel.
                var plan = MeterFace.Plan(area, false, bound, false, Fluent.Space2, MinChart, null);
                SetRowSpan(_reading, 2);
                _reading.HorizontalAlignment = HorizontalAlignment.Left;
                _reading.VerticalAlignment = VerticalAlignment.Center;
                _reading.Margin = new Thickness(HeroLayout.SideInset, 0, HeroLayout.SideInset, 0);
                LayHero(plan.Reading);
                return;
            }

            _dial.Visibility = Visibility.Visible;
            _dial.Lay(g);
            SetRow(_dial, 0);
            SetRowSpan(_dial, 2);
            SetColumnSpan(_dial, 2);
            _dial.HorizontalAlignment = HorizontalAlignment.Center;
            _dial.VerticalAlignment = VerticalAlignment.Center;
            _dial.Margin = new Thickness(HeroLayout.SideInset, top, HeroLayout.SideInset, BodyBottom);

            // The load's line stands on the dial's bottom edge, centred: the number in the bowl.
            SetRowSpan(_reading, 2);
            _reading.HorizontalAlignment = HorizontalAlignment.Center;
            _reading.VerticalAlignment = VerticalAlignment.Bottom;
            _reading.Margin = new Thickness(HeroLayout.SideInset, 0, HeroLayout.SideInset, BodyBottom + ((boxHeight - g.Height) / 2));
            LayHero(GaugeLayout.Reading(g, bound, FluentType.Body, FluentType.Display));
        }

        /// <summary>
        /// [S-6322] The engine rows where the history would be (under the load, beside it on a wide
        /// strip), inside the content inset; the rows grow their type and bars with the room.
        /// </summary>
        private void LayEngines(WidgetArea area)
        {
            var plan = MeterFace.Plan(area, true, HeroBound.Of(Formatting.Percent(1)), false, Fluent.Space2, MinChart, null);
            var side = plan.Arrangement == MeterFaceArrangement.Side;
            var alone = plan.Arrangement == MeterFaceArrangement.ReadingOnly || _engineLoads.Count == 0 || _engines is null;

            _root.ColumnDefinitions[0].Width = side ? new GridLength(plan.ReadingColumn) : new GridLength(1, GridUnitType.Star);
            _root.ColumnDefinitions[1].Width = side ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
            SetRow(_reading, 0);
            SetRowSpan(_reading, alone || side ? 2 : 1);
            SetColumnSpan(_reading, side ? 1 : 2);
            _reading.HorizontalAlignment = HorizontalAlignment.Left;
            _reading.VerticalAlignment = alone || side ? VerticalAlignment.Center : VerticalAlignment.Top;
            _reading.Margin = alone || side
                ? new Thickness(HeroLayout.SideInset, 0, HeroLayout.SideInset, 0)
                : new Thickness(HeroLayout.SideInset, HeroLayout.TopInset, HeroLayout.SideInset, 0);
            LayHero(plan.Reading);

            if (_engines is null)
            {
                return;
            }

            _engines.Visibility = alone ? Visibility.Collapsed : Visibility.Visible;

            if (alone)
            {
                return;
            }

            SetRow(_engines, side ? 0 : 1);
            SetRowSpan(_engines, side ? 2 : 1);
            SetColumn(_engines, side ? 1 : 0);
            SetColumnSpan(_engines, 1);
            _engines.VerticalAlignment = side ? VerticalAlignment.Center : VerticalAlignment.Top;
            _engines.Margin = side
                ? new Thickness(0, BodyBottom, HeroLayout.SideInset, BodyBottom)
                : new Thickness(HeroLayout.SideInset, Fluent.Space2, HeroLayout.SideInset, BodyBottom);

            var height = side ? Math.Max(0, area.Height - (2 * BodyBottom)) : Math.Max(0, plan.GraphHeight - BodyBottom);
            var width = side ? Math.Max(0, area.Width - plan.ReadingColumn - HeroLayout.SideInset) : Math.Max(0, area.Width - (2 * HeroLayout.SideInset));
            _engines.LayRows(BarLayout.Rows(height, _engines.Count, Fluent.Space3, width, LineCharacters));
        }

        private void LayHero(TypeStep step)
        {
            if (step == _heroStep)
            {
                return;
            }

            _heroStep = step;
            _hero.Lay(step, icon: false, trailingBeside: true, linesBeside: false);
        }
    }
}
