using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-3918] The Switch style: the toggle's name with a Windows toggle switch and its state line, laid
/// out by <see cref="SwitchFace"/>. Drawn, not a <see cref="ToggleSwitch"/>: the tile stays the one
/// hit target (and the card's in edit mode), so the switch never takes a press for itself.
/// </summary>
internal sealed partial class QuickToggleView
{
    private readonly Grid _switchFace = new();
    private readonly StackPanel _swTitle = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly Grid _swControl = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly Grid _swTrack = new();
    private readonly Border _swTrackFill = new() { BorderThickness = new Thickness(1) };
    private readonly Ellipse _swKnob = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly FontIcon _swIcon = Fluent.Icon(Glyphs.Wifi, "TextFillColorPrimaryBrush", 24);
    private readonly TextBlock _swName = Fluent.Text(FluentType.Body);
    private readonly TextBlock _swState = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");

    private readonly Brush _trackOff = FluentLayout.Themed("ControlAltFillColorSecondaryBrush", Colors.Gray);
    private readonly Brush _trackOffHover = FluentLayout.Themed("ControlAltFillColorTertiaryBrush", Colors.Gray);
    private readonly Brush _trackOffPressed = FluentLayout.Themed("ControlAltFillColorQuarternaryBrush", Colors.Gray);
    private readonly Brush _trackDisabled = FluentLayout.Themed("ControlAltFillColorDisabledBrush", Colors.Gray);
    private readonly Brush _trackEdge = FluentLayout.Themed("ControlStrongStrokeColorDefaultBrush", Colors.Gray);
    private readonly Brush _trackEdgeDisabled = FluentLayout.Themed("ControlStrongStrokeColorDisabledBrush", Colors.Gray);
    private readonly Brush _knobOff = FluentLayout.Themed("TextFillColorSecondaryBrush", Colors.Gray);

    private SwitchFacePlan _swPlan;

    private void BuildSwitch()
    {
        _swName.TextTrimming = TextTrimming.CharacterEllipsis;
        _swName.TextWrapping = TextWrapping.NoWrap;
        _swName.MaxLines = 1;
        _swState.TextTrimming = TextTrimming.CharacterEllipsis;
        _swState.MaxLines = 1;
        _swState.VerticalAlignment = VerticalAlignment.Center;
        _swIcon.HorizontalAlignment = HorizontalAlignment.Center;
        _swIcon.VerticalAlignment = VerticalAlignment.Center;

        _swTrack.Children.Add(_swTrackFill);
        _swTrack.Children.Add(_swKnob);
        _swTrack.VerticalAlignment = VerticalAlignment.Center;

        _swControl.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _swControl.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _swControl.ColumnSpacing = SwitchFace.Gap;
        _swControl.Children.Add(_swTrack);
        _swControl.Children.Add(_swState);

        _swTitle.Children.Add(_swIcon);
        _swTitle.Children.Add(_swName);

        _switchFace.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _switchFace.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _switchFace.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _switchFace.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _switchFace.Children.Add(_swTitle);
        _switchFace.Children.Add(_swControl);
        _switchFace.IsHitTestVisible = false;
    }

    /// <summary>[S-3918] Arrange the switch's parts for the plan: icon over switch, stacked, or a Settings row.</summary>
    private void LaySwitch(SwitchFacePlan plan, double width)
    {
        _swPlan = plan;
        var row = plan.Layout == SwitchLayout.Row;

        _switchFace.HorizontalAlignment = row ? HorizontalAlignment.Stretch : HorizontalAlignment.Center;
        _switchFace.VerticalAlignment = VerticalAlignment.Center;
        _switchFace.RowSpacing = SwitchFace.Gap;
        _switchFace.ColumnSpacing = row ? 2 * SwitchFace.Gap : 0;

        // The title: icon and name side by side in a row, stacked otherwise.
        Grid.SetRow(_swTitle, 0);
        Grid.SetColumn(_swTitle, 0);
        Grid.SetColumnSpan(_swTitle, row ? 1 : 2);
        _swTitle.Orientation = row ? Orientation.Horizontal : Orientation.Vertical;
        _swTitle.Spacing = SwitchFace.Gap;
        _swTitle.HorizontalAlignment = row ? HorizontalAlignment.Left : HorizontalAlignment.Center;

        Grid.SetRow(_swControl, row ? 0 : 1);
        Grid.SetColumn(_swControl, row ? 1 : 0);
        Grid.SetColumnSpan(_swControl, row ? 1 : 2);
        _swControl.HorizontalAlignment = row ? HorizontalAlignment.Right : HorizontalAlignment.Center;

        // A Settings row reads "On [switch]"; a standalone switch reads "[switch] On", as Windows draws each.
        Grid.SetColumn(_swState, row ? 0 : 1);
        Grid.SetColumn(_swTrack, row ? 1 : 0);

        _swIcon.Visibility = plan.IconSize > 0 ? Visibility.Visible : Visibility.Collapsed;
        _swIcon.FontSize = Math.Max(16, plan.IconSize);

        _swTrack.Width = plan.TrackWidth;
        _swTrack.Height = plan.TrackHeight;
        _swTrackFill.CornerRadius = new CornerRadius(plan.TrackHeight / 2);

        var nameRoom = row
            ? Math.Max(0, width - plan.TrackWidth - (plan.IconSize > 0 ? plan.IconSize + SwitchFace.Gap : 0) - (3 * SwitchFace.Gap)
                - (plan.State is { } s ? FluentType.EstimateWidth(s, QuickToggleRules.LongestStateChars(Strings.Current)) : 0))
            : Math.Max(0, width);

        if (plan.Name is { } name)
        {
            _swName.Visibility = Visibility.Visible;
            _swName.Apply(name);
            _swName.TextAlignment = row ? TextAlignment.Left : TextAlignment.Center;
            _swName.HorizontalAlignment = row ? HorizontalAlignment.Left : HorizontalAlignment.Center;
            _swName.VerticalAlignment = VerticalAlignment.Center;
            _swName.MaxWidth = nameRoom;
        }
        else
        {
            _swName.Visibility = Visibility.Collapsed;
        }

        if (plan.State is { } state)
        {
            _swState.Visibility = Visibility.Visible;
            _swState.Apply(state);
        }
        else
        {
            _swState.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>
    /// [S-3918] Windows' toggle switch: off, an outlined track with the knob left; on, the accent
    /// track with the knob right; under the pointer the knob grows and (as a Settings card) the tile
    /// takes the subtle fill; unavailable, the disabled track and knob with "Unavailable" beside it.
    /// </summary>
    private void PaintSwitch(Locale locale, ToggleLook look)
    {
        var on = look == ToggleLook.On;
        var live = look is ToggleLook.On or ToggleLook.Off;

        Background = live ? (_pressed ? _pressedFill : _hover ? _hoverFill : _rest) : _rest;

        Brush track;
        Brush edge;
        Brush knob;
        Brush text;

        switch (look)
        {
            case ToggleLook.On:
                track = _pressed ? _accentPressed : _hover ? _accentHover : _accent;
                edge = track;
                knob = _onAccent;
                text = _primary;
                break;

            case ToggleLook.Unavailable:
                track = _trackDisabled;
                edge = _trackEdgeDisabled;
                knob = _disabled;
                text = _disabled;
                break;

            case ToggleLook.Unknown:
                track = _trackOff;
                edge = _trackEdge;
                knob = _knobOff;
                text = _secondary;
                break;

            default:
                track = _pressed ? _trackOffPressed : _hover ? _trackOffHover : _trackOff;
                edge = _trackEdge;
                knob = _knobOff;
                text = _primary;
                break;
        }

        _swTrackFill.Background = track;
        _swTrackFill.BorderBrush = edge;

        var size = live && (_hover || _pressed) ? _swPlan.KnobHover : _swPlan.Knob;
        var margin = (_swPlan.TrackHeight - size) / 2;
        _swKnob.Width = size;
        _swKnob.Height = size;
        _swKnob.Fill = knob;
        _swKnob.HorizontalAlignment = on ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        _swKnob.Margin = new Thickness(margin, 0, margin, 0);

        _swIcon.Glyph = Glyph(_toggle, on: look != ToggleLook.Off);
        _swIcon.Foreground = look == ToggleLook.Unavailable ? _disabled : look == ToggleLook.Unknown ? _secondary : _primary;
        _swName.Text = QuickToggleRules.Name(locale, _toggle);
        _swName.Foreground = text;
        _swState.Text = QuickToggleRules.StateText(locale, look);
        _swState.Foreground = look == ToggleLook.Unavailable ? _disabled : _secondary;
    }
}
