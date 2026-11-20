using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.App.Widgets;

/// <summary>
/// [S-12835] A list's face with nothing in it (owner, 2026-11-19, WR-10: "it should show the default values ... and not
/// just not show the graph ... also for other widgets"; "No data, no account, not set up, offline: the widget keeps its
/// face (its graph, its rows, its month, its reading as "--") and says why in a compact line"): the rows the list keeps
/// a place for, under the compact line that says why, each the family's row with its defaults, "--" for its name and
/// "--" for its value, drawn unavailable as a facts list's missing fact is ([S-12813]). [S-14810] At the list's one row
/// size (their edges on whole device pixels), the first right under what stands over them, touching, as many as the room
/// reaches, the last cut by the card's foot; each one of the list's items (a Row block marks itself), so the face is
/// judged as the list's rows are. They take no pointer: a row to come, not a control.
/// </summary>
public sealed partial class EmptyRows : StackPanel
{
    private double _pitch = double.NaN;
    private double _room;

    public EmptyRows()
    {
        IsHitTestVisible = false;

        // Stretched: the slot's height is the room the rows fill (it asks for none itself).
        VerticalAlignment = VerticalAlignment.Stretch;
        SizeChanged += (_, args) => Room(args.NewSize.Height);
    }

    /// <summary>
    /// [S-12835] The rows under a state's compact line that stands on the card's top edge (a <c>StatusBlock</c> over the
    /// view's list): they start right under the line's whole cells.
    /// </summary>
    public static EmptyRows UnderStatus()
    {
        var rows = new EmptyRows { Visibility = Visibility.Collapsed };
        rows.Loaded += (_, _) => rows.Margin = new Thickness(0, LineRules.EdgeRow(ItemGrids.Of(rows)) - FamilySpace.Inset, 0, 0);
        return rows;
    }

    /// <summary>The list's row pitch (its rows' height as laid); unset, a one-line row's optimal on the card's grid.</summary>
    public double Pitch
    {
        get => _pitch;
        set
        {
            if (!value.Equals(_pitch))
            {
                _pitch = value;
                Fill();
            }
        }
    }

    /// <summary>[S-12813] What a row with nothing in it says for its name and its value: the family's default.</summary>
    public const string Placeholder = "--";

    /// <summary>The rows shown now.</summary>
    public int Count => Children.Count;

    /// <summary>[S-12835] [S-14810] How many rows of <paramref name="pitch"/> <paramref name="room"/> reaches: the last cut by its end.</summary>
    public static int Reaches(double room, double pitch)
        => !double.IsFinite(room) || !double.IsFinite(pitch) || pitch <= 0 || room <= LayoutMath.Tolerance ? 0 : (int)Math.Ceiling((room - LayoutMath.Tolerance) / pitch);

    /// <summary>The room under what stands over the rows (the panel's slot): the rows follow it.</summary>
    public void Room(double height)
    {
        _room = double.IsFinite(height) ? Math.Max(0, height) : 0;
        Fill();
    }

    /// <summary>The panel asks for no height of its own: it takes the slot it is given and stands its rows in it.</summary>
    protected override Windows.Foundation.Size MeasureOverride(Windows.Foundation.Size availableSize)
    {
        base.MeasureOverride(availableSize);
        return new Windows.Foundation.Size(double.IsFinite(availableSize.Width) ? availableSize.Width : 0, 0);
    }

    protected override Windows.Foundation.Size ArrangeOverride(Windows.Foundation.Size finalSize)
    {
        // [S-14810] Each row its one fixed size from the slot's top, its edges on whole device pixels; the last cut by the slot.
        var reach = new RowReach(PitchShown, Math.Max(1, Children.Count), XamlRoot?.RasterizationScale ?? 1);
        for (var i = 0; i < Children.Count; i++)
        {
            var top = reach.Start(i);
            Children[i].Arrange(new Windows.Foundation.Rect(0, top, finalSize.Width, reach.Start(i + 1) - top));
        }

        return finalSize;
    }

    /// <summary>[S-14810] The rows' one size: the list's pitch; unset, a one-line row's optimal on the card's grid.</summary>
    private double PitchShown => double.IsFinite(_pitch) && _pitch > 0 ? _pitch : LineRules.EdgeRow(ItemGrids.Of(this));

    private void Fill()
    {
        // [S-14810] As many as the room reaches at the pitch, the last cut by the slot's end.
        var count = Reaches(_room, PitchShown);
        var reach = new RowReach(PitchShown, Math.Max(1, count), XamlRoot?.RasterizationScale ?? 1);
        if (count == Children.Count)
        {
            // The same rows at another pitch: each takes its size.
            for (var i = 0; i < count; i++)
            {
                if (Children[i] is FrameworkElement row && !row.Height.Equals(reach.Height(i)))
                {
                    row.Height = reach.Height(i);
                }
            }

            InvalidateArrange();
            return;
        }

        Children.Clear();
        for (var i = 0; i < count; i++)
        {
            var row = new Themia.Sdk.WinUI.Kit.RowBlock(RowKind.List, lead: 0) { Height = reach.Height(i), Available = false };
            row.Show(Placeholder, Placeholder);
            Children.Add(row);
        }

        InvalidateArrange();
    }
}
