using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.Mixer;
using Glyphs = Themia.App.Shell.Glyphs;

namespace Themia.App.Widgets;

internal sealed partial class MixerView
{
    /// <summary>
    /// [S-449] One row's visuals, rewritten in place for whichever row sits at its position: a Row block
    /// ([S-6711]) led by the app's icon (16), its name, and in the meter's place the Controls slider
    /// (rail Track, value the accent) and the mute, a 28 icon button whose glyph is the level's band.
    /// </summary>
    private sealed class RowVisual
    {
        private readonly MixerView _owner;
        private readonly bool _leads;
        private readonly Grid _lead = new() { Width = FamilyShape.Icon, Height = FamilyShape.Icon };
        private readonly Image _image = new() { Width = FamilyShape.Icon, Height = FamilyShape.Icon, Stretch = Stretch.Uniform };
        private readonly FontIcon _glyph = Family.Glyph(Glyphs.Apps, FamilyShape.Icon, ColourRole.TextSecondary);
        private readonly Grid _controls = new();
        private readonly Slider _slider = ControlsBlock.RailSlider(0, 100, 0);
        private readonly Button _mute;
        private readonly FontIcon _muteIcon;
        private readonly PointerEventHandler _grab;
        private readonly PointerEventHandler _release;

        private MixerRow? _row;
        private string? _iconPath;
        private LogoTheme _iconTheme;
        private MixerPlan? _plan;
        private bool _echoing;
        private bool _grabbed;

        public RowVisual(MixerView owner, bool leads)
        {
            _owner = owner;
            _leads = leads;
            Host = new RowBlock(RowKind.List, leads ? FamilyShape.Icon : 0) { Interactive = true };

            _lead.Children.Add(_glyph);
            _lead.Children.Add(_image);
            if (leads)
            {
                Host.Lead(_lead);
            }

            _mute = ControlsBlock.IconButton(Glyphs.Volume3, Str.Mixer_Mute.Text(), OnMute);
            _muteIcon = (FontIcon)_mute.Content;
            _mute.IsTabStop = false;

            _slider.IsTabStop = false;
            _slider.ValueChanged += OnSliderChanged;

            // Handled events too: the slider marks its own pointer events handled. Four of them, for
            // the same reason a row's hover needs four: the pointer can leave without a release.
            _grab = (_, _) => _grabbed = true;
            _release = (_, _) => _grabbed = false;
            _slider.AddHandler(UIElement.PointerPressedEvent, _grab, true);
            _slider.AddHandler(UIElement.PointerReleasedEvent, _release, true);
            _slider.AddHandler(UIElement.PointerCaptureLostEvent, _release, true);
            _slider.AddHandler(UIElement.PointerCanceledEvent, _release, true);

            _controls.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _controls.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(_mute, 1);
            _controls.Children.Add(_slider);
            _controls.Children.Add(_mute);

            Host.RightTapped += OnRightTapped;
            Host.ActualThemeChanged += OnThemeChanged;
        }

        public RowBlock Host { get; }

        /// <summary>Whether the row is drawn as quiet (an app holding a session but silent).</summary>
        public bool Dimmed { get; private set; }

        /// <summary>[S-1940] False while the row stands in for a device that is not there.</summary>
        public bool Available { get; private set; } = true;

        /// <summary>[S-449] The row's lines and its slider for this size (the meter's place: the slider, 8, the mute).</summary>
        public void Arrange(MixerPlan plan)
        {
            if (_plan == plan)
            {
                return;
            }

            _plan = plan;
            Host.Lines = plan.Lines;
            var slider = plan.Slider is 0 ? Visibility.Collapsed : Visibility.Visible;
            _slider.Visibility = slider;
            _mute.Margin = new Thickness(slider == Visibility.Visible ? FamilySpace.S2 : 0, 0, 0, 0);
            var mute = FamilyShape.IconButton;
            Host.Meter(_controls, plan.Slider switch
            {
                0 => mute,
                double.NaN => double.NaN,
                var width => width + FamilySpace.S2 + mute,
            });
        }

        /// <param name="available">[S-1940] False for the stand-in row of a mixer with no device: every part disabled, the view says why.</param>
        public void Show(MixerRow row, bool dimmed, bool available)
        {
            _row = row;
            Dimmed = dimmed;
            Available = available;

            var name = RowName(row);
            var names = _plan?.Names ?? true;
            Host.Available = available;
            Host.Show(names ? name : string.Empty, second: MixerFill.Detail(row));
            ToolTipService.SetToolTip(Host, !available ? null : dimmed ? MixerStr.WidgetsB_Mixer_NotPlaying.Format(("name", name)) : name);
            UnavailableLook.Enable(available, _slider, _mute);
            _muteIcon.Foreground = Family.Brush(available ? ColourRole.TextPrimary : ColourRole.TextDisabled);
            _glyph.Foreground = Family.Brush(available ? ColourRole.TextSecondary : ColourRole.TextDisabled);

            // [S-2771] The level is the slider's position (and the mute glyph's band), never a number.
            var percent = MixerRules.DisplayPercent(row);
            ControlsBlock.Reglyph(_mute, AudioLook.OutputGlyph(row.Level, row.Muted), row.Muted ? Str.Mixer_Unmute.Text() : Str.Mixer_Mute.Text());
            ToolTipService.SetToolTip(_mute, !available ? null : row.Muted ? Str.Mixer_Unmute.Text() : Str.Mixer_Mute.Text());

            // A snapshot is the machine's truth, but not under a held thumb: writing it there would
            // snap the slider back to where the app was a moment ago.
            if (!_grabbed)
            {
                _echoing = true;
                _slider.Value = percent;
                _echoing = false;
            }

            ShowIcon(row);
        }

        /// <summary>[S-441] The app's own icon when there is one, else a glyph for what the row is.</summary>
        private void ShowIcon(MixerRow row)
        {
            if (!_leads)
            {
                return;
            }

            _glyph.Glyph = row.IsSystem ? Glyphs.Bell : Glyphs.Apps;

            if (row.Path is not { } path)
            {
                _iconPath = null;
                _image.Source = null;
                _glyph.Visibility = Visibility.Visible;
                return;
            }

            if (string.Equals(_iconPath, path, StringComparison.OrdinalIgnoreCase) && _iconTheme == AppIconSource.ThemeOf(Host))
            {
                return;
            }

            _iconPath = path;
            _image.Source = null;
            _glyph.Visibility = Visibility.Visible;
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

            // The row may show another app (or theme) by now.
            if (!string.Equals(_iconPath, path, StringComparison.OrdinalIgnoreCase) || _iconTheme != theme || image is null)
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

        private void OnMute()
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
            if (_row is not { } row || !Available)
            {
                return;
            }

            ItemMenu.ShowAt(Host, at,
            [
                new(row.Muted ? Glyphs.Volume3 : Glyphs.Mute, row.Muted ? Str.Mixer_Unmute.Text() : Str.Mixer_Mute.Text(), () => _owner.OnMute(row)),
                new(Glyphs.Reset, Str.Mixer_Reset.Format(("percent", LocaleFormat.Percent(Strings.Current, 100))), () => _owner.OnReset(row)),
            ]);
        }

        public void Detach()
        {
            Host.Detach();
            Host.RightTapped -= OnRightTapped;
            Host.ActualThemeChanged -= OnThemeChanged;
            _slider.ValueChanged -= OnSliderChanged;
            _slider.RemoveHandler(UIElement.PointerPressedEvent, _grab);
            _slider.RemoveHandler(UIElement.PointerReleasedEvent, _release);
            _slider.RemoveHandler(UIElement.PointerCaptureLostEvent, _release);
            _slider.RemoveHandler(UIElement.PointerCanceledEvent, _release);
        }
    }
}
