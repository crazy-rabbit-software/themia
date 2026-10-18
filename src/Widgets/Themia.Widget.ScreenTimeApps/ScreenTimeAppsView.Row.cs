using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Widget.ScreenTimeApps;
using Glyphs = Themia.App.Shell.Glyphs;

namespace Themia.App.Widgets;

/// <summary>The list's rows, reused by position as the data changes.</summary>
internal sealed partial class ScreenTimeAppsView
{
    /// <summary>
    /// [S-3350] One row: the app's icon (or an initial chip in its colour), its name, then the
    /// figures — average, share, and the time, always the last column. [S-3353] The row is lit,
    /// dimmed, marked selected and ringed for the keyboard from the view's links, never from its own
    /// pointer alone, so a hover in the chart lights it here.
    /// </summary>
    private sealed class AppRow
    {
        private const double IconSize = Fluent.IconSize;

        private static readonly Brush TimeBrush = ThemeBrush.Of("TextFillColorSecondaryBrush", Colors.Gray);

        private readonly ScreenTimeAppsView _owner;
        private readonly Grid _icon = new() { Width = IconSize, Height = IconSize, VerticalAlignment = VerticalAlignment.Center };
        private readonly Image _image = new() { Width = IconSize, Height = IconSize, Stretch = Stretch.Uniform };
        private readonly Border _chip = new() { Width = IconSize, Height = IconSize, CornerRadius = new CornerRadius(Fluent.ControlRadius) };
        private readonly TextBlock _initial = new()
        {
            FontSize = FluentType.Caption.FontSize,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        private readonly TextBlock _name = Fluent.Text(FluentType.Body);
        private readonly TextBlock _average = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
        private readonly TextBlock _percent = Fluent.Text(FluentType.Caption, "TextFillColorTertiaryBrush");
        private readonly TextBlock _time = Fluent.Text(FluentType.Body, "TextFillColorSecondaryBrush");

        /// <summary>Fluent's list selection pill, in the row's left padding.</summary>
        private readonly Border _pill = new()
        {
            Width = 3,
            Height = Fluent.IconSize,
            CornerRadius = new CornerRadius(1.5),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(-Fluent.Space3, 0, 0, 0),
            Visibility = Visibility.Collapsed,
        };

        private ScreenTimeSegment? _segment;
        private string? _iconPath;
        private LogoTheme _iconTheme;

        public AppRow(ScreenTimeAppsView owner)
        {
            _owner = owner;

            _initial.Foreground = ThemeBrush.Of("TextOnAccentFillColorPrimaryBrush", Colors.White);
            _chip.Child = _initial;
            _icon.Children.Add(_chip);
            _icon.Children.Add(_image);
            _pill.Background = ScreenTimeLook.Accent;

            foreach (var figure in new[] { _average, _percent, _time })
            {
                figure.VerticalAlignment = VerticalAlignment.Center;
                figure.Margin = new Thickness(Fluent.Space2, 0, 0, 0);
                Typography.SetNumeralAlignment(figure, FontNumeralAlignment.Tabular);
            }

            _time.TextAlignment = TextAlignment.Right;
            _name.Margin = new Thickness(Fluent.Space2, 0, 0, 0);
            _name.TextTrimming = TextTrimming.CharacterEllipsis;

            var grid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Auto },
                },
            };

            SetColumn(_icon, 0);
            SetColumn(_name, 1);
            SetColumn(_average, 2);
            SetColumn(_percent, 3);
            SetColumn(_time, 4);
            grid.Children.Add(_pill);
            grid.Children.Add(_icon);
            grid.Children.Add(_name);
            grid.Children.Add(_average);
            grid.Children.Add(_percent);
            grid.Children.Add(_time);

            Host = Fluent.Row(grid, compact: true);
            RowFocus.Prepare(Host);
            Host.PointerEntered += OnEntered;
            Host.PointerExited += OnLeft;
            Host.PointerCanceled += OnLeft;
            Host.PointerCaptureLost += OnLeft;
            Host.Tapped += OnTapped;
            Host.RightTapped += OnRightTapped;
            Host.ActualThemeChanged += OnThemeChanged;
        }

        public Border Host { get; }

        public string? Key => _segment?.Key;

        public void Show(ScreenTimeSegment segment, string? average, string? percent, ScreenTimeGoal? goal, bool name)
        {
            _segment = segment;
            var colour = ScreenTimeLook.For(segment.Key);

            // [S-3352] Too narrow for a name: the icon and the time, the name stays in the tooltip.
            _name.Text = segment.Name;
            _name.Visibility = name ? Visibility.Visible : Visibility.Collapsed;
            _average.Text = average ?? string.Empty;
            _average.Visibility = average is null ? Visibility.Collapsed : Visibility.Visible;
            _percent.Text = percent ?? string.Empty;
            _percent.Visibility = percent is null ? Visibility.Collapsed : Visibility.Visible;
            _time.Text = ScreenTimeRules.FormatDuration(segment.Seconds);

            // [S-457] Over its own daily limit, the app's time says so in the critical colour.
            _time.Foreground = goal is { Over: true } ? ScreenTimeLook.Critical : TimeBrush;

            var tip = $"{segment.Name} — {_time.Text}";
            if (average is not null)
            {
                tip += $" · {average}";
            }

            if (goal is { } g)
            {
                tip += $" · {(g.Over ? ScreenTimeAppsStr.WidgetsC_ScreenTime_LimitOverTip : ScreenTimeAppsStr.WidgetsC_ScreenTime_LimitTip).Format(("limit", ScreenTimeRules.FormatLimit(g.Limit)))}";
            }

            ToolTipService.SetToolTip(Host, tip);
            ShowIcon(segment, colour);
        }

        /// <summary>[S-3353] The row against the view's links.</summary>
        public void Mark(ScreenTimeEmphasis emphasis, bool selected, bool cursor)
        {
            Host.Opacity = emphasis == ScreenTimeEmphasis.Dimmed ? 0.45 : 1;
            // [S-6513] Selected keeps the lighter Fluent step it always had (SubtleFillColorTertiary).
            ItemFill.Paint(Host, ItemRole.Row, emphasis == ScreenTimeEmphasis.Lit ? ItemState.Hover
                : selected ? ItemState.SelectedHover
                : ItemState.Rest);
            _pill.Visibility = selected ? Visibility.Visible : Visibility.Collapsed;
            RowFocus.Mark(Host, cursor);
        }

        /// <summary>The app's icon; while it loads, or when it has none, a chip of its colour with its initial.</summary>
        private void ShowIcon(ScreenTimeSegment segment, Brush colour)
        {
            _chip.Background = colour;
            _initial.Text = segment.IsOther ? string.Empty : ScreenTimeRules.Initial(segment.Name);

            if (segment.Path is not { } path)
            {
                _iconPath = null;
                _image.Source = null;
                _chip.Visibility = Visibility.Visible;
                return;
            }

            if (string.Equals(_iconPath, path, StringComparison.OrdinalIgnoreCase) && _image.Source is not null && _iconTheme == AppIconSource.ThemeOf(Host))
            {
                return;
            }

            _iconPath = path;
            _image.Source = null;
            _chip.Visibility = Visibility.Visible;
            _ = LoadIconAsync(path);
        }

        /// <summary>[S-3965] A packaged app's logo is chosen per theme: a theme change asks again.</summary>
        private void OnThemeChanged(FrameworkElement sender, object args)
        {
            if (_iconPath is { } path && _iconTheme != AppIconSource.ThemeOf(Host))
            {
                _ = LoadIconAsync(path);
            }
        }

        private async Task LoadIconAsync(string path)
        {
            var theme = AppIconSource.ThemeOf(Host);
            _iconTheme = theme;
            var image = await AppIconSource.GetAsync(path, theme).ConfigureAwait(true);

            if (image is null || !string.Equals(_iconPath, path, StringComparison.OrdinalIgnoreCase) || _iconTheme != theme)
            {
                return;
            }

            _image.Source = image;
            _chip.Visibility = Visibility.Collapsed;
        }

        private void OnEntered(object sender, PointerRoutedEventArgs args)
        {
            if (_segment is { } segment)
            {
                _owner.Hover(segment.Key);
            }
        }

        private void OnLeft(object sender, PointerRoutedEventArgs args) => _owner.Hover(null);

        private void OnTapped(object sender, TappedRoutedEventArgs args)
        {
            args.Handled = true;

            if (_segment is { } segment)
            {
                _owner.Select(segment.Key);
            }
        }

        private void OnRightTapped(object sender, RightTappedRoutedEventArgs args)
        {
            if (_segment is not { IsOther: false } segment)
            {
                return;
            }

            ItemMenu.Show(Host, args, [new ItemMenuEntry(Glyphs.Hide, Str.Screentime_HideApp.Text(), () => ScreenTimeLook.Hide(_owner._source, segment.Key))]);
        }

        public void Detach()
        {
            Host.PointerEntered -= OnEntered;
            Host.PointerExited -= OnLeft;
            Host.PointerCanceled -= OnLeft;
            Host.PointerCaptureLost -= OnLeft;
            Host.Tapped -= OnTapped;
            Host.RightTapped -= OnRightTapped;
            Host.ActualThemeChanged -= OnThemeChanged;
        }
    }
}
