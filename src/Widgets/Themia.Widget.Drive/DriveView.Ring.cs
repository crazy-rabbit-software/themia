using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.Drive;

namespace Themia.App.Widgets;

/// <summary>
/// [S-6274] The drive reading's Ring style on the family's blocks: the used share as the Chart block's ring (stroke 2, the
/// bar's role, [S-6717]) with the share at 28 in its hole and the space line 8 under the ring. [S-15749] The ring, its share
/// and its line are one block (WR-7: "A reading and its caption stay together and scale up"), planned at the natural ring
/// (<see cref="DriveFace.PlannedRing"/>) and scaled as one (<see cref="ReadingFit"/>) until it fills the card's height or
/// its width stops it, then centred on the other axis (owner, 2026-11-20: "Centred is fine"); down to its line at 9 (WR-14).
/// The ring had grown alone while its share and line kept their size. Built when the style is chosen, dropped when the bar
/// is ([S-6005]); the drive and its walk are the card's as in the bar style.
/// </summary>
internal sealed partial class DriveView
{
    private RingParts? _ringParts;

    private sealed class RingParts
    {
        public readonly ReadingFit Root = new();
        public readonly Grid Box = new();
        public readonly ChartRing Ring = new();
        public readonly ReadoutBlock Share = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        public readonly TextBlock Line = Family.Text(FamilyType.Body, ColourRole.TextSecondary);

        public RingParts()
        {
            Line.Margin = new Thickness(0, FamilySpace.S2, 0, 0);
            Line.TextAlignment = TextAlignment.Center;
            Box.Children.Add(Ring);
            Box.Children.Add(Share);
            Root.Parts.Add(Box);
            Root.Parts.Add(Line);
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

    /// <summary>[S-15749] The ring at its planned size with its share at 28 in its hole and its line under it: the block scales as one.</summary>
    private void LayRing(DriveEntry drive)
    {
        if (_ringParts is not { } parts)
        {
            return;
        }

        var locale = Strings.Current;
        _figure.Visibility = Visibility.Collapsed;
        parts.Root.Visibility = Visibility.Visible;

        var share = ReadoutRules.Split(Formatting.Percent(locale, drive.UsedFraction ?? 0));
        parts.Box.Width = DriveFace.PlannedRing;
        parts.Box.Height = DriveFace.PlannedRing;
        parts.Share.Use(FamilyType.Big28, centred: true);
        parts.Share.Show(share.Value, share.Unit);
        parts.Ring.Show(drive.UsedFraction ?? 0);
        parts.Ring.Role(DriveRules.Role(drive));
        parts.Line.Text = DriveRules.SpaceLine(locale, drive);
        parts.Root.Refit();
    }
}
