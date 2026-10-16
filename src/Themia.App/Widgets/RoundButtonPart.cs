using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-3917] [S-1919] The round button, one part for the toggles' and the power widgets' Round style:
/// a circle holding the icon with the name under it, laid out by <see cref="RoundButtonFace"/>. It
/// only draws: the view that owns it keeps the whole tile as the hit target, decides the brushes
/// from its own state and paints them here, so a click, a key and edit mode behave as in every
/// other style.
/// </summary>
internal sealed partial class RoundButtonPart : Grid
{
    private readonly Grid _circle = new() { HorizontalAlignment = HorizontalAlignment.Center };
    private readonly Border _disc = new() { BorderThickness = new Thickness(1) };
    private readonly FontIcon _icon;
    private readonly TextBlock _label = Fluent.Text(FluentType.Body);
    private RoundButtonPlan _plan;

    public RoundButtonPart(string glyph)
    {
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;
        IsHitTestVisible = false;

        _icon = Fluent.Icon(glyph, "TextFillColorPrimaryBrush", 32);
        _icon.HorizontalAlignment = HorizontalAlignment.Center;
        _icon.VerticalAlignment = VerticalAlignment.Center;
        _circle.Children.Add(_disc);
        _circle.Children.Add(_icon);

        _label.HorizontalAlignment = HorizontalAlignment.Center;
        _label.TextAlignment = TextAlignment.Center;
        _label.VerticalAlignment = VerticalAlignment.Top;
        _label.TextTrimming = TextTrimming.CharacterEllipsis;

        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        SetRow(_label, 1);
        Children.Add(_circle);
        Children.Add(_label);
    }

    /// <summary>Something drawn over the circle at its size (the power widget's draining arm ring).</summary>
    public void Overlay(FrameworkElement element)
    {
        ArgumentNullException.ThrowIfNull(element);
        _circle.Children.Add(element);
    }

    /// <summary>Size the circle, the icon and the label slot for <paramref name="plan"/>.</summary>
    public void Lay(RoundButtonPlan plan, double maxLabelWidth)
    {
        _plan = plan;
        _circle.Width = plan.Diameter;
        _circle.Height = plan.Diameter;
        _disc.CornerRadius = new CornerRadius(plan.Diameter / 2);
        _icon.FontSize = plan.IconSize;

        foreach (var child in _circle.Children)
        {
            if (child is FrameworkElement overlay && overlay != _disc && overlay != _icon)
            {
                overlay.Width = plan.Diameter;
                overlay.Height = plan.Diameter;
            }
        }

        if (plan.Label is null)
        {
            _label.Visibility = Visibility.Collapsed;
            return;
        }

        _label.Visibility = Visibility.Visible;
        _label.Margin = new Thickness(0, RoundButtonFace.LabelGap, 0, 0);
        _label.Height = plan.LabelSlot;
        _label.MaxWidth = Math.Max(0, maxLabelWidth);
    }

    /// <summary>
    /// Paint one state: the circle's fill and edge, the icon, the label's text and colour. A
    /// <paramref name="sentence"/> is drawn on the plan's sentence step, wrapping where planned.
    /// </summary>
    public void Paint(Brush fill, Brush edge, string glyph, Brush icon, string text, Brush label, bool sentence = false)
    {
        // [S-6546] On a fill of its own the circle is no item, nor is the icon over it.
        ItemFill.Release(_disc);
        ItemFill.Release(_icon);
        _disc.Background = fill;
        PaintFace(edge, glyph, icon, text, label, sentence);
    }

    /// <summary>
    /// [S-6513] Paint one state with the circle as an item: a tile of the card's item layer in
    /// <paramref name="face"/>, rather than a fill of its own (the accent, the critical tint).
    /// </summary>
    public void Paint(ItemState face, Brush edge, string glyph, Brush icon, string text, Brush label, bool sentence = false)
    {
        ItemFill.Paint(_disc, ItemRole.Tile, face);

        // [S-6546] The icon is laid over the circle, not inside it: it is the item's text all the same.
        ItemFill.OnItem(_icon);
        PaintFace(edge, glyph, icon, text, label, sentence);
    }

    private void PaintFace(Brush edge, string glyph, Brush icon, string text, Brush label, bool sentence)
    {
        _disc.BorderBrush = edge;
        _icon.Glyph = glyph;
        _icon.Foreground = icon;
        _label.Text = text;
        _label.Foreground = label;

        if (_plan.Label is not { } step)
        {
            return;
        }

        if (sentence)
        {
            _label.Apply(_plan.Sentence);
            _label.MaxLines = _plan.SentenceLines;
            _label.TextWrapping = _plan.SentenceLines > 1 ? TextWrapping.WrapWholeWords : TextWrapping.NoWrap;
        }
        else
        {
            _label.Apply(step);
            _label.MaxLines = 1;
            _label.TextWrapping = TextWrapping.NoWrap;
        }
    }
}
