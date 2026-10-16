using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-6274] The drive card's Ring style: the used share as a ring (the bar's colours, [S-4653]) with
/// the share in its hole, and the drive's name and its space line beside the ring on a wide strip,
/// under it otherwise, as far as they fit (<see cref="RingFace"/>). Built when the style is chosen,
/// dropped when the bar is ([S-6005]); the drive and its walk are the card's as in the bar style.
/// </summary>
internal sealed partial class DriveView
{
    private RingParts? _ringParts;

    private sealed class RingParts
    {
        public readonly StackPanel Root = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        public readonly RingPart Ring = new();
        public readonly TextBlock Figure = Fluent.Text(FluentType.Subtitle);
        public readonly StackPanel Lines = new() { Spacing = Fluent.Space1, VerticalAlignment = VerticalAlignment.Center };
        public readonly TextBlock Name = Fluent.Text(FluentType.BodyStrong);
        public readonly TextBlock Space = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");

        public RingParts()
        {
            Figure.HorizontalAlignment = HorizontalAlignment.Center;
            Figure.VerticalAlignment = VerticalAlignment.Center;
            Microsoft.UI.Xaml.Documents.Typography.SetNumeralAlignment(Figure, FontNumeralAlignment.Tabular);
            Name.TextTrimming = TextTrimming.CharacterEllipsis;
            Space.TextTrimming = TextTrimming.CharacterEllipsis;

            Ring.Add(Figure);
            Lines.Children.Add(Name);
            Lines.Children.Add(Space);
            Root.Children.Add(Ring.Root);
            Root.Children.Add(Lines);
        }
    }

    /// <summary>[S-6005] The style's parts: the ring's built once it is chosen, let go when the bar is.</summary>
    private void UseStyle()
    {
        var ring = _config.Style == Core.Model.DriveStyle.Ring;
        _stack.Visibility = ring ? Visibility.Collapsed : Visibility.Visible;

        if (ring && _ringParts is null)
        {
            _ringParts = new RingParts();
            _ringParts.Root.Margin = WidgetLayout.SideInset;
            Children.Add(_ringParts.Root);
        }
        else if (!ring && _ringParts is not null)
        {
            Children.Remove(_ringParts.Root);
            _ringParts = null;
        }
    }

    /// <summary>[S-6274] The ring, its share and the lines that fit, for the card's area.</summary>
    private void LayRing(DriveEntry drive, WidgetArea area)
    {
        if (_ringParts is not { } parts)
        {
            return;
        }

        var locale = Strings.Current;
        var large = area.Least >= SizeClass.Expanded;
        var box = area.Deflate(Fluent.Space4, Fluent.Space2);
        var wide = area.Shape == AreaShape.Wide;
        var nameStep = large ? FluentType.Subtitle : FluentType.BodyStrong;
        var spaceStep = large ? FluentType.Body : FluentType.Caption;
        var plan = RingFace.Plan(box, wide, [nameStep.LineHeight, spaceStep.LineHeight], parts.Lines.Spacing);

        parts.Root.Orientation = plan.Beside ? Orientation.Horizontal : Orientation.Vertical;
        parts.Root.Spacing = plan.Lines > 0 ? RingFace.Gap : 0;
        parts.Ring.Resize(plan.Diameter);

        var used = drive.UsedFraction;
        parts.Ring.SetFraction(used);
        parts.Ring.SetBrush(FluentLayout.Themed(DriveRules.Pressure(drive) switch
        {
            MeterPressure.Critical => "SystemFillColorCriticalBrush",
            MeterPressure.Elevated => "SystemFillColorCautionBrush",
            _ => "AccentFillColorDefaultBrush",
        }, Colors.DodgerBlue));

        var bound = HeroBound.Of(Formatting.Percent(1));
        parts.Figure.Apply(FluentType.Strong(RingFace.Figure(plan.Diameter, bound)));
        parts.Figure.Text = used is { } share ? Formatting.Percent(locale, share) : "--";
        parts.Figure.Foreground = FluentLayout.Themed(used is null ? "TextFillColorDisabledBrush" : "TextFillColorPrimaryBrush", Colors.White);

        // [S-4671] Headerless, the body names the drive; a drive Windows cannot reach says why instead.
        parts.Name.Apply(nameStep);
        parts.Name.Text = drive.State == DriveState.Disconnected ? DriveRules.Description(locale, drive) : DriveRules.Title(locale, drive);
        parts.Space.Apply(spaceStep);
        parts.Space.Text = DriveRules.SpaceLine(locale, drive);

        var align = plan.Beside ? TextAlignment.Left : TextAlignment.Center;
        parts.Name.TextAlignment = align;
        parts.Space.TextAlignment = align;
        parts.Lines.Visibility = plan.Lines > 0 ? Visibility.Visible : Visibility.Collapsed;
        parts.Name.Visibility = plan.Lines >= 1 ? Visibility.Visible : Visibility.Collapsed;
        parts.Space.Visibility = plan.Lines >= 2 && drive.State != DriveState.Disconnected ? Visibility.Visible : Visibility.Collapsed;
        parts.Lines.MaxWidth = plan.Beside ? Math.Max(0, box.Width - plan.Diameter - RingFace.Gap) : box.Width;
    }
}
