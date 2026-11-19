using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Shapes;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using A = Themia.Sdk.Kit.ShellMetrics.AddPage;

namespace Themia.App.Shell;

/// <summary>
/// [S-13610] [S-13616] The add page's rail: Start, each subject and More as a vertical list, drawn as Karpo's
/// side list drew its items (karpo-app, its <c>SettingsPanel.Paint</c> <c>Nav</c>, NavigationView's look): each
/// item 36 tall, 4 apart, no fill at rest, the subtle fills under the pointer and pressed; the chosen one on the
/// subtle fill with the accent's 3 × 16 pill at its start; its glyph at 16, its name at 48 in Body, its count at
/// its end in Caption and secondary. Compact, the glyphs alone, the name and count in the tooltip. One tab stop:
/// the arrows walk it (<see cref="AddPageKeys.Rail"/>).
/// </summary>
internal sealed partial class AddRail : StackPanel
{
    private readonly List<Item> _items = [];

    public AddRail(IReadOnlyList<AddRailItem> items, Action<AddFolderId> choose)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(choose);
        Spacing = A.RailItemGap;
        TabFocusNavigation = KeyboardNavigationMode.Once;
        XYFocusKeyboardNavigation = XYFocusKeyboardNavigationMode.Disabled;
        AutomationProperties.SetName(this, Str.AddMenu_Subjects.Text());
        foreach (var item in items)
        {
            var view = new Item(item, () => choose(item.Id));
            _items.Add(view);
            Children.Add(view);
        }
    }

    /// <summary>The items' ids, in order.</summary>
    public IReadOnlyList<AddFolderId> Ids => [.. _items.Select(i => i.Id)];

    /// <summary>[S-13616] The glyphs alone (the rail 48 wide).</summary>
    public bool Compact
    {
        set
        {
            foreach (var item in _items)
            {
                item.Compact = value;
            }
        }
    }

    /// <summary>Mark <paramref name="id"/> chosen (null: none, a search is shown).</summary>
    public void Choose(AddFolderId? id)
    {
        foreach (var item in _items)
        {
            item.Chosen = item.Id == id;
        }
    }

    /// <summary>The item the keyboard is on, or null.</summary>
    public int? Focused
    {
        get
        {
            var at = _items.FindIndex(i => i.FocusState != FocusState.Unfocused);
            return at >= 0 ? at : null;
        }
    }

    /// <summary>Put the keyboard on the item <paramref name="at"/>.</summary>
    public bool FocusItem(int at)
    {
        if (at < 0 || at >= _items.Count)
        {
            return false;
        }

        _items[at].StartBringIntoView(new BringIntoViewOptions { AnimationDesired = false });
        return _items[at].Focus(FocusState.Keyboard);
    }

    /// <summary>A rail item: a subtle button of its glyph, name and count, the pill on the chosen one.</summary>
    private sealed partial class Item : Button
    {
        private readonly TextBlock _name;
        private readonly TextBlock _count;
        private readonly string _said;

        private readonly Rectangle _pill = new()
        {
            Width = A.RailPillWidth,
            Height = A.RailPillHeight,
            RadiusX = A.RailPillRadius,
            RadiusY = A.RailPillRadius,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Fill = Family.Brush(ColourRole.Accent),
            IsHitTestVisible = false,
        };

        public Item(AddRailItem item, Action choose)
        {
            Id = item.Id;
            var glyph = Fluent.Icon(item.Glyph, "TextFillColorPrimaryBrush");
            glyph.HorizontalAlignment = HorizontalAlignment.Left;
            glyph.VerticalAlignment = VerticalAlignment.Center;
            glyph.Margin = new Thickness(A.RailIconAt, 0, 0, 0);

            var title = item.Title.Text();
            var count = item.Count > 0 ? LocaleFormat.Integer(item.Count) : string.Empty;
            _count = Fluent.Text(FamilyType.Caption, "TextFillColorSecondaryBrush");
            _count.Text = count;
            _count.HorizontalAlignment = HorizontalAlignment.Right;
            _count.VerticalAlignment = VerticalAlignment.Center;
            _count.Margin = new Thickness(0, 0, A.RailCountEnd, 0);

            _name = Fluent.Text(FamilyType.Body);
            _name.Text = title;
            _name.TextTrimming = TextTrimming.CharacterEllipsis;
            _name.TextWrapping = TextWrapping.NoWrap;
            _name.VerticalAlignment = VerticalAlignment.Center;

            // The name runs from 48 to the count's start, cut short there.
            var words = new Grid
            {
                Margin = new Thickness(A.RailNameAt, 0, 0, 0),
                ColumnSpacing = A.RailCountEnd,
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = GridLength.Auto },
                },
                Children = { _name, _count },
            };
            Grid.SetColumn(_count, 1);

            Content = new Grid { Children = { _pill, glyph, words } };
            Height = A.RailItem;
            HorizontalAlignment = HorizontalAlignment.Stretch;
            HorizontalContentAlignment = HorizontalAlignment.Stretch;
            VerticalContentAlignment = VerticalAlignment.Stretch;
            Padding = new Thickness(0);
            CornerRadius = new CornerRadius(ShellMetrics.Page.CardRadius);
            PageTabButton.Subtle(this);
            Tag = "rail:" + item.Id;
            _said = count.Length > 0 ? $"{title}, {Str.Groups_Add_Count.Plural(item.Count)}" : title;
            AutomationProperties.SetName(this, _said);
            Click += (_, _) => choose();
            Chosen = false;
        }

        public AddFolderId Id { get; }

        public bool Chosen
        {
            set
            {
                _pill.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                Background = value
                    ? ThemeBrush.Of("SubtleFillColorSecondaryBrush", Colors.Gray)
                    : ThemeBrush.Of("SubtleFillColorTransparentBrush", Colors.Transparent);
                AutomationProperties.SetItemStatus(this, value ? "selected" : string.Empty);
            }
        }

        public bool Compact
        {
            set
            {
                _name.Visibility = value ? Visibility.Collapsed : Visibility.Visible;
                _count.Visibility = value ? Visibility.Collapsed : Visibility.Visible;
                ToolTipService.SetToolTip(this, value ? _said : null);
            }
        }
    }
}
