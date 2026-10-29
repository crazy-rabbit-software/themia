using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.Drive;

namespace Themia.App.Widgets;

/// <summary>
/// [S-6274] The drive card's Ring style on the family's blocks: the used share as the Chart block's
/// ring (stroke 2, the bar's role, [S-6717]) with the share in its hole, start-aligned, as large as its
/// column allows with the space line 8 under it, and the facts beside it on an expanded width
/// (<see cref="DriveFace.Ring"/>). Built when the style is chosen, dropped when the bar is ([S-6005]);
/// the drive and its walk are the card's as in the bar style.
/// </summary>
internal sealed partial class DriveView
{
    private RingParts? _ringParts;

    private sealed class RingParts
    {
        public readonly StackPanel Root = new() { VerticalAlignment = VerticalAlignment.Top };
        public readonly Grid Box = new() { HorizontalAlignment = HorizontalAlignment.Left };
        public readonly ChartRing Ring = new();
        public readonly ReadoutBlock Share = new();
        public readonly TextBlock Line = Family.Text(FamilyType.Body, ColourRole.TextSecondary);

        public RingParts()
        {
            Line.Margin = new Thickness(0, FamilySpace.S2, 0, 0);
            Line.TextTrimming = TextTrimming.CharacterEllipsis;
            Line.HorizontalAlignment = HorizontalAlignment.Left;
            Box.Children.Add(Ring);
            Box.Children.Add(Share);
            Root.Children.Add(Box);
            Root.Children.Add(Line);
        }
    }

    /// <summary>[S-6005] The style's parts: the ring's built once it is chosen, let go when the bar is.</summary>
    private void UseStyle()
    {
        var ring = _config.Style == Core.Model.DriveStyle.Ring;
        _figure.Visibility = ring ? Visibility.Collapsed : Visibility.Visible;

        if (ring && _ringParts is null)
        {
            _ringParts = new RingParts();
            Children.Add(_ringParts.Root);
        }
        else if (!ring && _ringParts is not null)
        {
            Children.Remove(_ringParts.Root);
            _ringParts = null;
        }
    }

    /// <summary>[S-6274] The ring, its share, its line and the facts that fit, for the view's area.</summary>
    private void LayRing(DriveEntry drive, WidgetArea area)
    {
        if (_ringParts is not { } parts)
        {
            return;
        }

        var locale = Strings.Current;
        var plan = DriveFace.Ring(area);
        Columns(plan.Side, plan.Column);
        _figure.Visibility = Visibility.Collapsed;
        parts.Root.Visibility = Visibility.Visible;
        parts.Root.Margin = plan.Side ? new Thickness(FamilySpace.Inset, 0, 0, 0) : Family.SideInset;

        var share = ReadoutRules.Split(Formatting.Percent(locale, drive.UsedFraction ?? 0));
        var ringed = plan.Ring > 0;
        parts.Ring.Visibility = ringed ? Visibility.Visible : Visibility.Collapsed;
        parts.Box.Width = ringed ? plan.Ring : double.NaN;
        parts.Box.Height = ringed ? plan.Ring : double.NaN;
        parts.Share.HorizontalAlignment = ringed ? HorizontalAlignment.Center : HorizontalAlignment.Left;
        parts.Share.VerticalAlignment = ringed ? VerticalAlignment.Center : VerticalAlignment.Top;
        parts.Share.Use(plan.Step, centred: ringed);
        parts.Share.Show(share.Value, share.Unit);
        parts.Ring.Show(drive.UsedFraction ?? 0);
        parts.Ring.Role(DriveRules.Role(drive));

        parts.Line.Visibility = plan.Line ? Visibility.Visible : Visibility.Collapsed;
        parts.Line.Text = DriveRules.SpaceLine(locale, drive);
        parts.Line.MaxWidth = plan.Width;

        Facts(drive, locale, plan.Side, under: false, plan.Facts);
    }
}
