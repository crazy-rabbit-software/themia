using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Glyphs = Themia.App.Shell.Glyphs;

namespace Themia.App.Widgets;

internal sealed partial class MixerView
{
    /// <summary>One row's visuals, rewritten in place for whichever row sits at its position.</summary>
    private sealed class RowVisual
    {
        private static readonly Brush PrimaryBrush = FluentLayout.Themed("TextFillColorPrimaryBrush", Colors.White);
        private static readonly Brush QuietBrush = FluentLayout.Themed("TextFillColorTertiaryBrush", Colors.Gray);
        private static readonly Brush MutedBrush = FluentLayout.Themed("TextFillColorDisabledBrush", Colors.Gray);

        private readonly MixerView _owner;
        private readonly Grid _grid = new();
        private readonly Grid _iconSlot = new() { Width = Fluent.Space5, Height = Fluent.Space5 };
        private readonly Image _image = new() { Width = Fluent.Space5, Height = Fluent.Space5, Stretch = Stretch.Uniform };
        private readonly FontIcon _glyph = Fluent.Icon(Glyphs.Apps, "TextFillColorSecondaryBrush");
        private readonly TextBlock _name = Fluent.Text(FluentType.Body);
        private readonly TextBlock _detail = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
        private readonly Border _count;
        private readonly TextBlock _countText = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
        private readonly Slider _slider = new() { Minimum = 0, Maximum = 100, StepFrequency = 1 };
        private readonly Rectangle _meter = new() { Height = AudioLook.MeterHeight };
        private readonly ScaleTransform _meterScale = new() { ScaleX = 0 };
        private readonly Button _mute = new();
        private readonly FontIcon _muteIcon = Fluent.Icon(Glyphs.Volume3, "TextFillColorPrimaryBrush");
        private readonly IDisposable _hover;
        private readonly PointerEventHandler _grab;
        private readonly PointerEventHandler _release;
        private readonly HugRow _nameLine;

        private MixerRow? _row;
        private string? _iconPath;
        private MixerRowStyle? _style;
        private bool _icons = true;
        private bool _detailOn;
        private bool _meterOn;
        private bool _echoing;
        private bool _grabbed;

        public RowVisual(MixerView owner)
        {
            _owner = owner;

            _iconSlot.Children.Add(_glyph);
            _iconSlot.Children.Add(_image);
            _iconSlot.VerticalAlignment = VerticalAlignment.Center;

            _name.VerticalAlignment = VerticalAlignment.Center;
            _detail.TextTrimming = TextTrimming.CharacterEllipsis;
            _detail.Visibility = Visibility.Collapsed;

            // [S-442] How many sessions the one slider drives — a quiet pill, not an unread badge.
            _countText.VerticalAlignment = VerticalAlignment.Center;
            _count = new Border
            {
                Child = _countText,
                Background = FluentLayout.Themed("ControlFillColorSecondaryBrush", Colors.Gray),
                CornerRadius = new CornerRadius(Fluent.OverlayRadius),
                Padding = new Thickness(Fluent.Space1 + 2, 0, Fluent.Space1 + 2, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed,
            };

            // The count hugs the name, and the name ellipsises rather than running under the slider.
            _nameLine = new HugRow(_name, _count, Fluent.Space2) { VerticalAlignment = VerticalAlignment.Center };

            _mute.Content = _muteIcon;
            _mute.Padding = new Thickness(Fluent.Space1 + 2);
            _mute.MinWidth = 0;
            _mute.MinHeight = 0;
            _mute.Background = FluentLayout.Themed("SubtleFillColorTransparentBrush", Colors.Transparent);
            _mute.BorderThickness = new Thickness(0);
            _mute.CornerRadius = new CornerRadius(Fluent.ControlRadius);
            _mute.VerticalAlignment = VerticalAlignment.Center;
            _mute.IsTabStop = false;
            _mute.Click += OnMuteClicked;

            _slider.VerticalAlignment = VerticalAlignment.Center;
            _slider.IsTabStop = false;
            _slider.ValueChanged += OnSliderChanged;

            // [S-1223] The meter: a scale transform on a full-width bar, so a sample costs no layout.
            _meter.Fill = Accent();
            _meter.RenderTransform = _meterScale;
            _meter.RenderTransformOrigin = new Windows.Foundation.Point(0, 0.5);
            _meter.VerticalAlignment = VerticalAlignment.Bottom;
            _meter.IsHitTestVisible = false;
            _meter.Visibility = Visibility.Collapsed;

            // Handled events too: the slider marks its own pointer events handled. Four of them, for
            // the same reason a row's hover needs four — the pointer can leave without a release.
            _grab = (_, _) => _grabbed = true;
            _release = (_, _) => _grabbed = false;
            _slider.AddHandler(PointerPressedEvent, _grab, true);
            _slider.AddHandler(PointerReleasedEvent, _release, true);
            _slider.AddHandler(PointerCaptureLostEvent, _release, true);
            _slider.AddHandler(PointerCanceledEvent, _release, true);

            _grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            _grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
            _grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            _grid.Children.Add(_iconSlot);
            _grid.Children.Add(_nameLine);
            _grid.Children.Add(_detail);
            _grid.Children.Add(_slider);
            _grid.Children.Add(_meter);
            _grid.Children.Add(_mute);

            Host = Fluent.Row(_grid);
            Host.RightTapped += OnRightTapped;

            // [S-1225] The keyboard cursor sits on the row, with the system focus rect: no layout cost.
            Host.IsTabStop = true;
            Host.UseSystemFocusVisuals = true;
            _hover = Fluent.Hover(Host);
        }

        public Border Host { get; }

        /// <summary>Whether the row is drawn as quiet (an app holding a session but silent).</summary>
        public bool Dimmed { get; private set; }

        /// <summary>
        /// [S-1227] Put the parts where this width wants them, at the height the ladder counted on.
        /// Gaps are margins on the parts rather than grid spacing, because spacing is paid between
        /// empty columns too, and a narrow row cannot afford thirty pixels of gaps around nothing.
        /// </summary>
        /// <param name="detail">
        /// [S-1624] A one-line row tall enough for it has a caption under the name; the slider and
        /// the mute then span both lines, centred on the row.
        /// </param>
        public void Arrange(MixerRowStyle style, double height, bool icons, bool detail)
        {
            Host.Height = height;
            Host.MinHeight = height;

            detail &= style == MixerRowStyle.OneLine;

            if (_style == style && _icons == icons && _detailOn == detail)
            {
                return;
            }

            _style = style;
            _icons = icons;
            _detailOn = detail;
            _detail.Visibility = detail ? Visibility.Visible : Visibility.Collapsed;
            var lines = detail ? 2 : 1;
            Place(_iconSlot, 0, 0, 1, lines);

            switch (style)
            {
                case MixerRowStyle.OneLine:
                    Place(_nameLine, 0, 1, 1);
                    Place(_detail, 1, 1, 1);
                    Place(_slider, 0, 2, 1, lines);
                    Place(_meter, 0, 2, 1, lines);
                    Place(_mute, 0, 4, 1, lines);
                    _nameLine.VerticalAlignment = detail ? VerticalAlignment.Bottom : VerticalAlignment.Center;
                    _detail.VerticalAlignment = VerticalAlignment.Top;
                    _slider.Margin = new Thickness(Fluent.Space3, 0, 0, 0);
                    _meter.Margin = new Thickness(Fluent.Space3 + Fluent.Space2, 0, Fluent.Space2, 0);
                    break;

                case MixerRowStyle.TwoLine:
                    _nameLine.VerticalAlignment = VerticalAlignment.Center;
                    Place(_nameLine, 0, 1, 2);
                    Place(_mute, 0, 4, 1);
                    Place(_slider, 1, 1, 4);
                    Place(_meter, 1, 1, 4);

                    // Tucked up under the name line: the slider's own box is taller than its track.
                    _slider.Margin = new Thickness(icons ? Fluent.Space2 : 0, -Fluent.Space2, Fluent.Space2, -Fluent.Space2);
                    _meter.Margin = new Thickness((icons ? Fluent.Space2 : 0) + Fluent.Space2, 0, Fluent.Space4, -2);
                    break;

                default:
                    _nameLine.VerticalAlignment = VerticalAlignment.Center;
                    Place(_nameLine, 0, 1, 2);
                    Place(_mute, 0, 4, 1);
                    break;
            }

            _slider.Visibility = style == MixerRowStyle.Compact ? Visibility.Collapsed : Visibility.Visible;

            // A compact row is the app and its mute (the glyph shows the level band, [S-2771]): the
            // icon names the app, and the name only comes back when there is no icon to do it.
            _nameLine.Visibility = style == MixerRowStyle.Compact && icons ? Visibility.Collapsed : Visibility.Visible;
            _nameLine.Margin = new Thickness(icons ? Fluent.Space2 : 0, 0, 0, 0);
            _detail.Margin = _nameLine.Margin;
            _mute.Margin = new Thickness(Fluent.Space1, 0, 0, 0);

            // A two-line row is the name line (28: the mute button) and the tucked slider (16) in
            // 48: two pixels above, none below, and the meter in the last two.
            Host.Padding = style switch
            {
                MixerRowStyle.Compact => new Thickness(Fluent.Space3, 0, Fluent.Space1, 0),
                MixerRowStyle.TwoLine => new Thickness(Fluent.Space4, 2, Fluent.Space2, 0),
                _ => new Thickness(Fluent.Space4, Fluent.Space1, Fluent.Space2, Fluent.Space1),
            };
        }

        private static void Place(FrameworkElement element, int row, int column, int span, int rows = 1)
        {
            SetRow(element, row);
            SetRowSpan(element, rows);
            SetColumn(element, column);
            SetColumnSpan(element, span);
        }

        public void Show(MixerRow row, bool showIcons, bool dimmed, bool meter)
        {
            _row = row;
            Dimmed = dimmed;
            _meterOn = meter && _style != MixerRowStyle.Compact;
            _meter.Visibility = _meterOn ? Visibility.Visible : Visibility.Collapsed;

            var name = RowName(row);
            _name.Text = name;
            _name.Foreground = row.Muted ? MutedBrush : dimmed ? QuietBrush : PrimaryBrush;
            _detail.Text = MixerFill.Detail(row);
            ToolTipService.SetToolTip(Host, dimmed ? Str.WidgetsB_Mixer_NotPlaying.Format(("name", name)) : name);

            _count.Visibility = row.Sessions > 1 ? Visibility.Visible : Visibility.Collapsed;
            _countText.Text = LocaleFormat.Integer(row.Sessions);

            // [S-2771] The level is the slider's position (and the mute glyph's band), never a number.
            var percent = MixerRules.DisplayPercent(row);

            _muteIcon.Glyph = AudioLook.OutputGlyph(row.Level, row.Muted);
            ToolTipService.SetToolTip(_mute, row.Muted ? Str.Mixer_Unmute.Text() : Str.Mixer_Mute.Text());

            // A snapshot is the machine's truth, but not under a held thumb: writing it there would
            // snap the slider back to where the app was a moment ago.
            if (!_grabbed)
            {
                _echoing = true;
                _slider.Value = percent;
                _echoing = false;
            }

            ShowIcon(row, showIcons);
        }

        /// <summary>[S-1223] The row's meter: the device for the master row, else its session (or loudest of its group).</summary>
        public void ShowPeak(AudioPeaks peaks)
        {
            if (!_meterOn || _row is not { } row)
            {
                _meterScale.ScaleX = 0;
                return;
            }

            var peak = row.IsMaster ? peaks.Output : peaks.ByKey.GetValueOrDefault(row.Key);
            _meterScale.ScaleX = row.Muted ? 0 : AudioRules.Peak(peak);
        }

        /// <summary>[S-441] The app's own icon when there is one, else a glyph for what the row is.</summary>
        private void ShowIcon(MixerRow row, bool showIcons)
        {
            _iconSlot.Visibility = showIcons ? Visibility.Visible : Visibility.Collapsed;

            if (!showIcons)
            {
                return;
            }

            _glyph.Glyph = row.IsMaster ? Glyphs.Volume : row.IsSystem ? Glyphs.Bell : Glyphs.Apps;

            if (row.Path is not { } path)
            {
                _iconPath = null;
                _image.Source = null;
                _glyph.Visibility = Visibility.Visible;
                return;
            }

            if (string.Equals(_iconPath, path, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _iconPath = path;
            _image.Source = null;
            _glyph.Visibility = Visibility.Visible;
            _ = LoadIconAsync(path);
        }

        private async Task LoadIconAsync(string path)
        {
            var image = await AppIcons.GetAsync(path).ConfigureAwait(true);

            // The row may show another app by now.
            if (!string.Equals(_iconPath, path, StringComparison.OrdinalIgnoreCase) || image is null)
            {
                return;
            }

            _image.Source = image;
            _glyph.Visibility = Visibility.Collapsed;
        }

        private void OnSliderChanged(object sender, RangeBaseValueChangedEventArgs args)
        {
            if (_echoing || _row is not { } row || Math.Abs(args.NewValue - args.OldValue) < 0.5)
            {
                return;
            }

            _owner.OnLevel(row, MixerRules.ClampLevel(args.NewValue / 100));
        }

        private void OnMuteClicked(object sender, RoutedEventArgs args)
        {
            if (_row is { } row)
            {
                _owner.OnMute(row);
            }
        }

        private void OnRightTapped(object sender, RightTappedRoutedEventArgs args)
        {
            OpenMenu(args.GetPosition(Host));
            args.Handled = true;
        }

        /// <summary>[S-447] Mute or unmute the row, or put it back to 100% — by right-click or the Menu key.</summary>
        public void OpenMenu(Windows.Foundation.Point? at)
        {
            if (_row is not { } row)
            {
                return;
            }

            ItemMenu.ShowAt(Host, at,
            [
                new(row.Muted ? Glyphs.Volume3 : Glyphs.Mute, row.Muted ? Str.Mixer_Unmute.Text() : Str.Mixer_Mute.Text(), () => _owner.OnMute(row)),
                new(Glyphs.Reset, Str.Mixer_Reset.Text(), () => _owner.OnReset(row)),
            ]);
        }

        public void Detach()
        {
            _hover.Dispose();
            Host.RightTapped -= OnRightTapped;
            _mute.Click -= OnMuteClicked;
            _slider.ValueChanged -= OnSliderChanged;
            _slider.RemoveHandler(PointerPressedEvent, _grab);
            _slider.RemoveHandler(PointerReleasedEvent, _release);
            _slider.RemoveHandler(PointerCaptureLostEvent, _release);
            _slider.RemoveHandler(PointerCanceledEvent, _release);
        }
    }
}
