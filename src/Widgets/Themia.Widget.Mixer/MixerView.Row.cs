using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
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
    /// [S-449] [S-7074] The mixer's look on the kit's list: a Row block ([S-6711]) led by the app's icon (16), its name,
    /// what it is doing on the second line, and in the meter's place the Controls slider (rail Track, value the accent)
    /// and the mute, an icon button whose glyph is the level's band. A row is rewritten in place for whichever app stands
    /// in its place: each snapshot is fresh row data, and drawing the row again would destroy the slider under the
    /// user's pointer mid-drag (the original hit exactly that).
    /// </summary>
    private sealed class MixerLook(MixerView owner) : IKitLook
    {
        private readonly Dictionary<RowBlock, Controls> _rows = [];

        public RowBlock Draw(KitItem item, KitDraw draw)
        {
            var row = new RowBlock(RowKind.List, draw.Plan.Lead ? FamilyShape.Icon : 0) { Interactive = true };
            var controls = new Controls(owner, row, draw.Plan.Lead);
            _rows[row] = controls;
            controls.Show(item, draw);
            return row;
        }

        public bool Update(RowBlock row, KitItem was, KitItem item, KitDraw draw)
        {
            if (!_rows.TryGetValue(row, out var controls))
            {
                return false;
            }

            controls.Show(item, draw);
            return true;
        }

        public void Dropped(RowBlock row)
        {
            if (_rows.Remove(row, out var controls))
            {
                controls.Detach();
            }
        }

        /// <summary>The widget goes: every row's handlers come off.</summary>
        public void Forget()
        {
            foreach (var controls in _rows.Values)
            {
                controls.Detach();
            }

            _rows.Clear();
        }
    }

    /// <summary>One row's lead, slider and mute, rewritten for whichever app the row stands for.</summary>
    private sealed class Controls
    {
        private readonly MixerView _owner;
        private readonly RowBlock _host;
        private readonly bool _leads;
        private readonly Grid _lead = new() { Width = FamilyShape.Icon, Height = FamilyShape.Icon, VerticalAlignment = VerticalAlignment.Center };
        private readonly FontIcon _glyph = Family.Glyph(Glyphs.Apps, FamilyShape.Icon, ColourRole.TextSecondary);
        private readonly Grid _controls = new();
        private readonly Slider _slider = ControlsBlock.RailSlider(0, 100, 0);
        private readonly Button _mute;
        private readonly FontIcon _muteIcon;
        private readonly PointerEventHandler _grab;
        private readonly PointerEventHandler _release;
        private readonly AppIconLead _appIcon;

        private MixerRow? _row;
        private bool _echoing;
        private bool _grabbed;

        public Controls(MixerView owner, RowBlock host, bool leads)
        {
            _owner = owner;
            _host = host;
            _leads = leads;

            _appIcon = new AppIconLead(_glyph, () => _host);
            _appIcon.Image.Width = FamilyShape.Icon;
            _appIcon.Image.Height = FamilyShape.Icon;
            _lead.Children.Add(_glyph);
            _lead.Children.Add(_appIcon.Image);
            if (leads)
            {
                host.Lead(_lead);
            }

            _mute = ControlsBlock.IconButton(Glyphs.Volume3, Str.Mixer_Mute.Text(), OnMute);
            _muteIcon = (FontIcon)_mute.Content;
            _mute.IsTabStop = false;

            _slider.IsTabStop = false;
            _slider.ValueChanged += OnSliderChanged;

            // Handled events too: the slider marks its own pointer events handled. Four of them, for the same reason a
            // row's hover needs four: the pointer can leave without a release.
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

            host.ActualThemeChanged += OnThemeChanged;
        }

        /// <param name="item">The app's item; its row's level, mute and standing are the view's (<see cref="MixerView.RowOf"/>).</param>
        public void Show(KitItem item, KitDraw draw)
        {
            if (_owner.RowOf(item.Id) is not { } shown)
            {
                return;
            }

            var (row, dimmed, available) = shown;
            _row = row;
            var plan = MixerLayout.Plan(draw.Plan.ColumnWidth > 0 ? draw.Plan.ColumnWidth : MixerLayout.NaturalWidth, _leads, draw.Plan.Lines, 1, 1, 0);

            _host.Lines = draw.Plan.Lines;
            _host.Height = draw.Height;
            _host.Available = available;
            _host.Show(draw.Title(item), second: draw.Plan.Subtitle ? item.Subtitle : null);

            // [S-11805] On a one-line row (28) the mute is the 24 button inset 2 in the row, so the rows' buttons stand 4
            // apart and read as separate; on a two-line row (40) it is the 28 button.
            var mute = MixerLayout.Mute(draw.Plan.Lines);
            _mute.Width = mute;
            _mute.Height = mute;
            _mute.Margin = new Thickness(FamilySpace.S2, 0, 0, 0);
            _host.Meter(_controls, plan.Slider + FamilySpace.S2 + mute);

            // [S-1228] An app holding a session but silent says so.
            ToolTipService.SetToolTip(_host, !available ? null : dimmed ? MixerStr.WidgetsB_Mixer_NotPlaying.Format(("name", draw.Title(item))) : draw.Title(item));
            UnavailableLook.Enable(available, _slider, _mute);
            _muteIcon.Foreground = Family.Brush(available ? ColourRole.TextPrimary : ColourRole.TextDisabled);
            _glyph.Foreground = Family.Brush(available ? ColourRole.TextSecondary : ColourRole.TextDisabled);

            // [S-2771] The level is the slider's position (and the mute glyph's band), never a number.
            ControlsBlock.Reglyph(_mute, AudioLook.OutputGlyph(row.Level, row.Muted), row.Muted ? Str.Mixer_Unmute.Text() : Str.Mixer_Mute.Text());
            ToolTipService.SetToolTip(_mute, !available ? null : row.Muted ? Str.Mixer_Unmute.Text() : Str.Mixer_Mute.Text());

            // A snapshot is the machine's truth, but not under a held thumb: writing it there would snap the slider back to
            // where the app was a moment ago.
            if (!_grabbed)
            {
                _echoing = true;
                _slider.Value = MixerRules.DisplayPercent(row);
                _echoing = false;
            }

            // [S-441] The app's own icon when there is one, else a glyph for what the row is.
            if (_leads)
            {
                _glyph.Glyph = row.IsSystem ? Glyphs.Bell : Glyphs.Apps;
                _appIcon.Show(row.Path);
            }
        }

        /// <summary>[S-3965] A packaged app's logo is chosen per theme: a theme change asks again.</summary>
        private void OnThemeChanged(FrameworkElement sender, object args) => _appIcon.ThemeChanged();

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

        public void Detach()
        {
            _host.ActualThemeChanged -= OnThemeChanged;
            _slider.ValueChanged -= OnSliderChanged;
            _slider.RemoveHandler(UIElement.PointerPressedEvent, _grab);
            _slider.RemoveHandler(UIElement.PointerReleasedEvent, _release);
            _slider.RemoveHandler(UIElement.PointerCaptureLostEvent, _release);
            _slider.RemoveHandler(UIElement.PointerCanceledEvent, _release);
        }
    }
}
