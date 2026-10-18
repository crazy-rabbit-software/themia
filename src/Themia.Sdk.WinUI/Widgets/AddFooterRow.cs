using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Themia.App.Shell;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-3850] The one "Add …" row a list widget adds from (Habits, Tasks): a full-width list row
/// holding a plus and a line of secondary text, pinned to the bottom of the content box under the
/// list, the way a Windows 11 list ends in its add row. A tap opens the widget's own text box in
/// place of the line; whether it shows at all is <see cref="AddFooter.Show"/>.
/// <para>
/// The view owns the editor (what Enter, Tab and Escape do is its own rule); this owns the shape, so
/// every widget's add row has the same height, glyph, type and inset.
/// </para>
/// </summary>
public sealed class AddFooterRow
{
    private readonly Grid _body;
    private readonly TextBlock _label;
    private readonly Action _onAdd;
    private IDisposable? _hover;

    public AddFooterRow(string label, Action onAdd)
    {
        _onAdd = onAdd;

        var plus = Fluent.Icon(Glyphs.Add, "TextFillColorSecondaryBrush");
        plus.Margin = new Thickness(Fluent.Space1 / 2, 0, Fluent.Space3 + (Fluent.Space1 / 2), 0);

        _label = Fluent.Text(FluentType.Body, "TextFillColorSecondaryBrush");
        _label.VerticalAlignment = VerticalAlignment.Center;
        _label.TextTrimming = TextTrimming.CharacterEllipsis;

        _body = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
            },
        };

        Grid.SetColumn(_label, 1);
        _body.Children.Add(plus);
        _body.Children.Add(_label);

        Element = Fluent.Row(_body);
        Element.VerticalAlignment = VerticalAlignment.Bottom;
        Element.Visibility = Visibility.Collapsed;
        Element.Tapped += OnTapped;
        _hover = Fluent.Hover(Element);
        Label = label;
    }

    /// <summary>The row to place in the view's last grid row.</summary>
    public Border Element { get; }

    /// <summary>The line it shows ("Add a task"); set again when the language changes.</summary>
    public string Label
    {
        get => _label.Text;
        set
        {
            _label.Text = value;
            AutomationProperties.SetName(Element, value);
        }
    }

    /// <summary>
    /// Put the view's editor where the line is. Taking the editor down is the view's: removing the
    /// box from its parent and showing the parent's children again brings the line back.
    /// </summary>
    public void Open(TextBox box)
    {
        Grid.SetColumn(box, 1);
        _label.Visibility = Visibility.Collapsed;
        _body.Children.Add(box);
        Element.Visibility = Visibility.Visible;
    }

    /// <summary>Apply the pass: shown (or kept while it holds an editor), the list's row height and side inset.</summary>
    public void Fit(bool show, bool compact, double inset)
    {
        Element.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        Element.Padding = new Thickness(inset, 0, inset, 0);
        Fluent.SetDensity(Element, compact);
    }

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
