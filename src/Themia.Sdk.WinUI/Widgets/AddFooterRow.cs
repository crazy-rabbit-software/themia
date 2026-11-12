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
/// leading with the + glyph, its name the line ("Add a task") in the secondary role, pinned to the
/// bottom of the content box under the list, the way a Windows 11 list ends in its add row. A tap
/// opens the widget's own text box in place of the line; whether it shows at all is
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

        Element = new Border { Child = _row, VerticalAlignment = VerticalAlignment.Bottom, Visibility = Visibility.Collapsed };
        ItemFill.Paint(Element, ItemRole.Row);
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

    /// <summary>
    /// Put the view's editor where the line is; returns what it hid. Taking the editor down is the
    /// view's: removing the box from its parent and showing what it hid again brings the line back.
    /// </summary>
    public IReadOnlyList<UIElement> Open(TextBox box)
    {
        ArgumentNullException.ThrowIfNull(box);

        var line = (UIElement)_row.NameText.Parent;
        line.Visibility = Visibility.Collapsed;
        Grid.SetColumn(box, 1);
        Grid.SetColumnSpan(box, 3);
        _row.Children.Add(box);
        Element.Visibility = Visibility.Visible;
        return [line];
    }

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
