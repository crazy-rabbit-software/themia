using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.App.Widgets;

/// <summary>
/// [S-3918] The Switch style: the toggle's name with a Windows toggle switch and its state line, laid
/// out by <see cref="SwitchFace"/>. Drawn, not a <see cref="ToggleSwitch"/>: the tile stays the one
/// hit target (and the card's in edit mode), so the switch never takes a press for itself.
/// </summary>
public sealed partial class QuickToggleView
{
    private readonly Canvas _switchFace = new();
    private readonly Grid _swTrack = new();
    private readonly Border _swTrackFill = new() { BorderThickness = new Thickness(1) };
    private readonly Ellipse _swKnob = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly FontIcon _swIcon = Fluent.Icon(Glyphs.Wifi, "TextFillColorPrimaryBrush", 24);
    private readonly TextBlock _swName = Fluent.Text(FluentType.Body);
    private readonly TextBlock _swState = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");

    private readonly Brush _trackOff = ThemeBrush.Of("ControlAltFillColorSecondaryBrush", Colors.Gray);
    private readonly Brush _trackOffHover = ThemeBrush.Of("ControlAltFillColorTertiaryBrush", Colors.Gray);
    private readonly Brush _trackOffPressed = ThemeBrush.Of("ControlAltFillColorQuarternaryBrush", Colors.Gray);
    private readonly Brush _trackDisabled = ThemeBrush.Of("ControlAltFillColorDisabledBrush", Colors.Gray);
    private readonly Brush _trackEdge = ThemeBrush.Of("ControlStrongStrokeColorDefaultBrush", Colors.Gray);
    private readonly Brush _trackEdgeDisabled = ThemeBrush.Of("ControlStrongStrokeColorDisabledBrush", Colors.Gray);
    private readonly Brush _knobOff = ThemeBrush.Of("TextFillColorSecondaryBrush", Colors.Gray);

    private SwitchFacePlan _swPlan;

    private void BuildSwitch()
    {
        foreach (var text in (TextBlock[])[_swName, _swState])
        {
            text.TextTrimming = TextTrimming.CharacterEllipsis;
            text.TextWrapping = TextWrapping.NoWrap;
            text.MaxLines = 1;
            text.VerticalAlignment = VerticalAlignment.Center;
        }

        _swIcon.HorizontalAlignment = HorizontalAlignment.Center;
        _swIcon.VerticalAlignment = VerticalAlignment.Center;

        _swTrack.Children.Add(_swTrackFill);
        _swTrack.Children.Add(_swKnob);

        // Every part is placed where the plan says ([S-3918]): the centring is Core's rule, not the panels'.
        _switchFace.Children.Add(_swIcon);
        _switchFace.Children.Add(_swName);
        _switchFace.Children.Add(_swState);
        _switchFace.Children.Add(_swTrack);
        _switchFace.HorizontalAlignment = HorizontalAlignment.Stretch;
        _switchFace.VerticalAlignment = VerticalAlignment.Stretch;
        _switchFace.IsHitTestVisible = false;
    }

    /// <summary>[S-3918] Place the switch's parts at the plan's boxes: a centred column or a Settings row.</summary>
    private void LaySwitch(SwitchFacePlan plan)
    {
        _swPlan = plan;
        var row = plan.Layout == SwitchLayout.Row;

        Place(_swIcon, plan.Icon);
        _swIcon.FontSize = Math.Max(16, plan.IconSize);

        Place(_swTrack, plan.Track);
        _swTrackFill.CornerRadius = new CornerRadius(plan.TrackHeight / 2);

        // Line boxes: the text centred in a column's full width, left in a row's room before the switch.
        var align = row ? TextAlignment.Left : TextAlignment.Center;
        Line(_swName, plan.Name, plan.NameBox, align);
        Line(_swState, plan.State, plan.StateBox, align);
    }

    private static void Place(FrameworkElement element, Themia.Core.Geometry.PixelRect box)
    {
        element.Visibility = box.Width > 0 ? Visibility.Visible : Visibility.Collapsed;
        element.Width = box.Width;
        element.Height = box.Height;
        Canvas.SetLeft(element, box.X);
        Canvas.SetTop(element, box.Y);
    }

    private static void Line(TextBlock text, TypeStep? step, Themia.Core.Geometry.PixelRect box, TextAlignment align)
    {
        if (step is not { } shown)
        {
            text.Visibility = Visibility.Collapsed;
            return;
        }

        text.Apply(shown);
        text.LineStackingStrategy = LineStackingStrategy.BlockLineHeight;
        text.TextAlignment = align;
        Place(text, box);
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

        // [S-6513] The tile is an item of the card's item layer, lit under the pointer while it works.
        ItemFill.Paint(this, ItemRole.Row, live ? ItemFace() : look == ToggleLook.Unavailable ? ItemState.Disabled : ItemState.Rest);

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
