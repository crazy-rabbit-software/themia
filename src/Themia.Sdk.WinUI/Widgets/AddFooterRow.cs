using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Themia.App.Shell;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;

namespace Themia.App.Widgets;

/// <summary>
/// [S-3850] The one "Add …" row a list widget adds from (Habits, Tasks): a Row block ([S-6711]) of 32
/// leading with the + glyph, its name the line ("Add a task") in the secondary role, right under the
/// list's last row, the way a Windows 11 list ends in its add row ([S-12834]: the room left over is the
/// foot's, never between the rows and the add row, WR-5). A tap
/// opens the widget's own text box in place of the line: the one <see cref="InlineBox"/>, standing in the
/// line's words, so the row keeps its height, glyph, inset and type ([S-13950]); whether it shows at all is
/// the widget's plan's (where it can add, [S-2440]).
/// <para>
/// The view owns the editor (what Enter, Tab and Escape do is its own rule); this owns the shape, so
/// every widget's add row has the same height, glyph, type and inset.
/// </para>
/// </summary>
public sealed class AddFooterRow
{
    private readonly RowBlock _row = new(RowKind.List);
    private readonly Action _onAdd;
    private IDisposable? _hover;

    public AddFooterRow(string label, Action onAdd)
    {
        _onAdd = onAdd;
        _row.LeadGlyph(Glyphs.Add);

        // [S-12834] Right under the rows (the view lays it in the grid row after theirs; the foot takes the rest).
        Element = new Border
        {
            Child = _row,
            VerticalAlignment = VerticalAlignment.Top,
            Visibility = Visibility.Collapsed,
        };

        ItemMark.Mark(Element);
        ItemFill.Paint(Element, ItemRole.Row);

        // [S-12830] [S-14500] A row of the list's: at its optimal until its list lays it, then stretched as the rows over it
        // (ItemStretch.RowsAbove; the list sets its height).
        Element.Loaded += (_, _) =>
        {
            if (double.IsNaN(Element.Height))
            {
                Element.Height = ItemStretch.Optimal(FamilyShape.RowHeight(1), ItemGrids.Of(Element));
            }
        };
        Element.Tapped += OnTapped;
        _hover = Fluent.Hover(Element);
        Label = label;
    }

    /// <summary>The row to place in the view's last grid row.</summary>
    public Border Element { get; }

    /// <summary>The line it shows ("Add a task"); set again when the language changes.</summary>
    public string Label
    {
        get => _row.NameText.Text;
        set
        {
            _row.Show(value);
            _row.NameText.Foreground = Family.Brush(ColourRole.TextSecondary);
            AutomationProperties.SetName(Element, value);
        }
    }

    /// <summary>[S-13950] The line's words: what the view's <see cref="InlineBox"/> stands in for, in their own box.</summary>
    public TextBlock Words => _row.NameText;

    /// <summary>The row's block: its ring says where the keys are while the box is open.</summary>
    public RowBlock Row => _row;

    /// <summary>Apply the pass: shown (or kept while it holds an editor).</summary>
    public void Fit(bool show) => Element.Visibility = show ? Visibility.Visible : Visibility.Collapsed;

    public void Detach()
    {
        Element.Tapped -= OnTapped;
        _hover?.Dispose();
        _hover = null;
    }

    private void OnTapped(object sender, TappedRoutedEventArgs args)
    {
        args.Handled = true;
        _onAdd();
    }
}
