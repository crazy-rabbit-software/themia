using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Sdk.Kit;

namespace Themia.Sdk.WinUI.Kit;

/// <summary>
/// [S-12481] [S-7064] The state of every widget that shows a source's data, in the widget's own content (no
/// card has a header to carry it, [S-12480]): the problem glyph and the busy ring, standing over the
/// content's top end corner at the inset, taking no room of the content's. The problem glyph is the one
/// stale rule ([S-7042]): it shows only while a copy is on screen that the last attempt of some source
/// could not improve on, its tooltip one line per source and problem ("Work: No network · updated
/// 10:42"); a click on it opens settings › Accounts when the problem is one only the user can mend there. The
/// age is never drawn here: it is on the Refresh line. The ring shows only while a fetch for one of the
/// widget's own sources is out ([S-2661]). With neither it is collapsed.
/// <para>
/// A view adds it as its last child (<see cref="Place"/>): over every row and column of its grid, so it
/// stands over whatever is at the top end and never moves what the view lays out.
/// </para>
/// </summary>
public sealed partial class KitStatus : StackPanel
{
    private readonly IWidgetHost _host;
    private readonly FontIcon _problem;
    private readonly ProgressRing _ring;
    private bool _offersAccounts;

    public KitStatus(IWidgetHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        _host = host;
        Orientation = Orientation.Horizontal;
        Spacing = LineRules.PartGap;
        HorizontalAlignment = HorizontalAlignment.Right;
        VerticalAlignment = VerticalAlignment.Top;
        Margin = new Thickness(0, FamilySpace.S1, FamilySpace.Inset, 0);
        Canvas.SetZIndex(this, 1);
        Grid.SetRowSpan(this, Span);
        Grid.SetColumnSpan(this, Span);

        _problem = ToolLine.Problem(string.Empty);
        _problem.Tapped += OnProblemTapped;
        _ring = ToolLine.Busy();
        _ring.IsActive = false;
        Children.Add(_problem);
        Children.Add(_ring);
        Show([], busy: false);
    }

    /// <summary>Over every row and column a view's grid has.</summary>
    private const int Span = 64;

    /// <summary>
    /// Put the status over <paramref name="view"/>'s content: its last child, across its whole grid. [S-6754] A view that
    /// owns the card's top edge (a graph's plot) has no inset from the card over it: the status keeps it itself.
    /// </summary>
    public void Place(Panel view)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (Parent is null)
        {
            Margin = Margin with { Top = (view is IWidgetTopEdge { OwnsTopEdge: true } ? FamilySpace.Inset : 0) + FamilySpace.S1 };
            view.Children.Add(this);
            LeaveRoom();
        }
    }

    /// <summary>
    /// Show the state: the problem lines (none hides the glyph), whether a fetch is out and whether the
    /// problem is mended in settings › Accounts.
    /// </summary>
    public void Show(IReadOnlyList<string> problems, bool busy, bool offersAccounts = false)
    {
        ArgumentNullException.ThrowIfNull(problems);

        _offersAccounts = offersAccounts;
        _problem.Visibility = problems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        ToolTipService.SetToolTip(_problem, problems.Count > 0 ? string.Join("\n", problems) : null);
        _ring.IsActive = busy;
        _ring.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        Visibility = problems.Count > 0 || busy ? Visibility.Visible : Visibility.Collapsed;
        LeaveRoom();
    }

    /// <summary>
    /// [S-12813] The content under the status's corner leaves it room (a facts list's first row ends its value before
    /// the glyph, never under it): every part of the view that takes it (<see cref="IStatusRoom"/>) hears how wide it is.
    /// </summary>
    private void LeaveRoom()
    {
        var width = 0.0;
        if (Visibility == Visibility.Visible)
        {
            Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
            width = Math.Max(0, DesiredSize.Width - Margin.Left - Margin.Right) + FamilySpace.S2;
        }

        _room = width;
        if (Parent is Panel view)
        {
            foreach (var part in Rooms(view))
            {
                part.StatusRoom(width);
            }
        }
    }

    private double _room;

    /// <summary>[S-12834] Every part of the view that leaves the status room, however deep it stands (a list in the view's body).</summary>
    private static IEnumerable<IStatusRoom> Rooms(DependencyObject node)
    {
        IEnumerable<UIElement> children = node switch
        {
            Panel panel => panel.Children,
            Border { Child: { } child } => [child],
            _ => [],
        };

        foreach (var child in children)
        {
            if (child is IStatusRoom part)
            {
                yield return part;
            }

            foreach (var deeper in Rooms(child))
            {
                yield return deeper;
            }
        }
    }

    /// <summary>
    /// [S-12834] A part built after the status last spoke (a list a view draws anew) asks for its room when it stands in
    /// the view: the status of the nearest panel over it that has one.
    /// </summary>
    public static void Ask(FrameworkElement part)
    {
        ArgumentNullException.ThrowIfNull(part);
        if (part is not IStatusRoom room)
        {
            return;
        }

        for (DependencyObject? node = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(part); node is not null; node = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(node))
        {
            if (node is Panel panel && panel.Children.OfType<KitStatus>().FirstOrDefault() is { } status)
            {
                room.StatusRoom(status._room);
                return;
            }
        }
    }

    /// <summary>Nothing to say: the glyph hidden, the ring stopped.</summary>
    public void Clear() => Show([], busy: false);

    public void Detach() => Clear();

    private void OnProblemTapped(object sender, TappedRoutedEventArgs args)
    {
        if (!_offersAccounts)
        {
            return;
        }

        args.Handled = true;
        if (!_host.ShowAccounts())
        {
            Log.Info("kit", "nothing here opens settings › Accounts");
        }
    }
}

/// <summary>[S-12813] A part under a status's corner that leaves it room (a facts list's first row).</summary>
public interface IStatusRoom
{
    /// <summary>The status's width over the content's top end corner (0: none shown).</summary>
    void StatusRoom(double width);
}
