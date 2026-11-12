using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.Drive;

namespace Themia.App.Widgets;

/// <summary>
/// [S-6274] The drive reading's Ring style on the family's blocks: the used share as the Chart block's
/// ring (stroke 2, the bar's role, [S-6717]) with the share at 28 in its hole, start-aligned, as large as
/// the box allows with the space line 8 under it (<see cref="DriveFace.Ring"/>). Built when the style is chosen, dropped when the bar is ([S-6005]);
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
        public readonly ReadoutBlock Share = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
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

    /// <summary>[S-6274] The ring as large as the content box takes with its line under it, the share at 28 in its hole.</summary>
    private void LayRing(DriveEntry drive)
    {
        if (_ringParts is not { } parts || !_area.IsMeasured)
        {
            return;
        }

        var locale = Strings.Current;
        var width = Math.Max(0, _area.Width - (2 * FamilySpace.Inset));
        var ring = DriveFace.Ring(width, Math.Max(0, _area.Height - FamilySpace.Inset));
        _figure.Visibility = Visibility.Collapsed;
        parts.Root.Visibility = Visibility.Visible;
        parts.Root.Margin = Family.SideInset;

        var share = ReadoutRules.Split(Formatting.Percent(locale, drive.UsedFraction ?? 0));
        parts.Box.Width = ring;
        parts.Box.Height = ring;
        parts.Share.Use(FamilyType.Big28, centred: true);
        parts.Share.Show(share.Value, share.Unit);
        parts.Ring.Show(drive.UsedFraction ?? 0);
        parts.Ring.Role(DriveRules.Role(drive));

        parts.Line.Text = DriveRules.SpaceLine(locale, drive);
        parts.Line.MaxWidth = width;
    }
}
