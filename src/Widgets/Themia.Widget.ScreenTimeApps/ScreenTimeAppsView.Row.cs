using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.ScreenTimeApps;
using Glyphs = Themia.App.Shell.Glyphs;

namespace Themia.App.Widgets;

/// <summary>The list's rows, reused by position as the data changes.</summary>
internal sealed partial class ScreenTimeAppsView
{
    /// <summary>
    /// [S-3350] One row, the family's Row block ([S-6711]): the app's icon as its lead (or, while it
    /// loads and when it has none, a 16 dot in the app's tone, [S-2272]), its name (primary), its time as the
    /// value (secondary; Critical over its own limit), and on a second line the share and a window's
    /// average when the tier gives rows two. [S-3353] The row is lit, dimmed, filled as selected and
    /// ringed for the keyboard from the view's links, never from its own pointer alone, so a hover in
    /// the chart lights it here.
    /// </summary>
    private sealed class AppRow
    {
        private readonly ScreenTimeAppsView _owner;
        private readonly Grid _icon = new() { VerticalAlignment = VerticalAlignment.Center };
        private readonly Image _image = new() { Stretch = Stretch.Uniform };

        /// <summary>[S-2272] The app's tone as a dot: where it has no icon, and under it while the icon loads.</summary>
        private readonly Border _dot = new()
        {
            Width = FamilyShape.Icon,
            Height = FamilyShape.Icon,
            CornerRadius = Family.Round(FamilyShape.Icon, FamilyShape.Icon),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        private ScreenTimeSegment? _segment;
        private ScreenTimeGoal? _goal;
        private string? _iconPath;
        private LogoTheme _iconTheme;

        /// <param name="lead">The lead's size (<see cref="ScreenTimeAppsPlan.Lead"/>): a row of another lead is a new row.</param>
        public AppRow(ScreenTimeAppsView owner, double lead)
        {
            _owner = owner;
            Lead = lead;
            _icon.Width = lead;
            _icon.Height = lead;
            _image.Width = lead;
            _image.Height = lead;
            _icon.Children.Add(_dot);
            _icon.Children.Add(_image);

            Host = new RowBlock(RowKind.List, lead);
            Host.Lead(_icon);
            Host.PointerEntered += OnEntered;
            Host.PointerExited += OnLeft;
            Host.PointerCanceled += OnLeft;
            Host.PointerCaptureLost += OnLeft;
            Host.Tapped += OnTapped;
            Host.RightTapped += OnRightTapped;
            Host.ActualThemeChanged += OnThemeChanged;
        }

        public RowBlock Host { get; }

        public string? Key => _segment?.Key;

        /// <summary>The lead's size the row was made for.</summary>
        public double Lead { get; }

        public void Show(ScreenTimeSegment segment, ScreenTimeTone tone, string? figures, ScreenTimeGoal? goal, ScreenTimeAppsPlan plan)
        {
            // [S-2272] The dot is the app's tone: the accent at its rank's step, its alpha on the dot.
            _dot.Background = ScreenTimeLook.Brush(tone);
            _dot.Opacity = tone.Alpha;
            _segment = segment;
            _goal = goal;
            var time = ScreenTimeRules.FormatDuration(segment.Seconds);

            // [S-3352] Too narrow for a name: the icon and the time, the name stays in the tooltip.
            Host.Lines = plan.Lines;
            Host.Show(plan.Names ? segment.Name : string.Empty, time, figures);

            var tip = $"{segment.Name} — {time}";
            if (figures is not null)
            {
                tip += $" · {figures}";
            }

            if (goal is { } g)
            {
                tip += $" · {(g.Over ? ScreenTimeAppsStr.WidgetsC_ScreenTime_LimitOverTip : ScreenTimeAppsStr.WidgetsC_ScreenTime_LimitTip).Format(("limit", ScreenTimeRules.FormatLimit(g.Limit)))}";
            }

            ToolTipService.SetToolTip(Host, tip);
            ShowIcon(segment);
        }

        /// <summary>
        /// [S-3353] The row against the view's links: the focused one in the item layer's hover fill,
        /// the selected one in its selected fill, every other row's texts in the tertiary role while
        /// something is focused; the keyboard cursor's ring.
        /// </summary>
        public void Mark(ScreenTimeEmphasis emphasis, bool selected, bool cursor)
        {
            var dim = emphasis == ScreenTimeEmphasis.Dimmed;
            Host.NameText.Foreground = Family.Brush(dim ? ColourRole.TextTertiary : ColourRole.TextPrimary);
            Host.Second.Foreground = Family.Brush(dim ? ColourRole.TextTertiary : ColourRole.TextSecondary);

            // [S-457] Over its own daily limit, the app's time says so in the critical role.
            Host.ValueText.Foreground = Family.Brush(_goal is { Over: true } ? ColourRole.Critical : dim ? ColourRole.TextTertiary : ColourRole.TextSecondary);

            // [S-6513] Selected keeps the item layer's lighter step (SubtleFillColorTertiary).
            ItemFill.Paint(Host, ItemRole.Row, emphasis == ScreenTimeEmphasis.Lit ? ItemState.Hover
                : selected ? ItemState.SelectedHover
                : ItemState.Rest);
            Host.Focused = cursor;
        }

        /// <summary>The app's icon at the lead's size; while it loads, or when it has none, the dot of its colour.</summary>
        private void ShowIcon(ScreenTimeSegment segment)
        {
            if (segment.Path is not { } path)
            {
                _iconPath = null;
                _image.Source = null;
                _dot.Visibility = Visibility.Visible;
                return;
            }

            if (string.Equals(_iconPath, path, StringComparison.OrdinalIgnoreCase) && _image.Source is not null && _iconTheme == AppIconSource.ThemeOf(Host))
            {
                return;
            }

            _iconPath = path;
            _image.Source = null;
            _dot.Visibility = Visibility.Visible;
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
            _dot.Visibility = Visibility.Collapsed;
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
