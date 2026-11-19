using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.App.Widgets;
using Themia.Core.Desktop;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Shell;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Windows.System;
using P = Themia.Sdk.Kit.ShellMetrics.Page;

namespace Themia.App.Shell;

/// <summary>[S-13700] What the Unplaced icons page shows now: the view's name, its unplaced items and the fences they can go in.</summary>
/// <param name="ViewName">The view shown on the page's display.</param>
/// <param name="Holds">Whether the view shows desktop icons at all (<see cref="DesktopViews.Holds"/>).</param>
internal sealed record UnplacedModel(string ViewName, bool Holds, IReadOnlyList<DesktopItem> Items, IReadOnlyList<(string Id, string Name)> Fences);

/// <summary>What the Unplaced icons page does, given by its display (or a render's stand-in).</summary>
/// <param name="Read">[S-13700] The page's model as the desktop and the view are now.</param>
/// <param name="Place">[S-13703] Place these items on the view at its next free cells.</param>
/// <param name="PutIn">[S-13704] Put these items into the fence of that id.</param>
/// <param name="Drag">[S-13705] These items were dragged off the page: their placement follows the pointer.</param>
/// <param name="Icon">The shell's icon of an item at so many physical pixels.</param>
internal sealed record UnplacedCommands(
    Func<UnplacedModel> Read,
    Action<IReadOnlyList<string>> Place,
    Action<string, IReadOnlyList<string>> PutIn,
    Action<IReadOnlyList<string>> Drag,
    Func<DesktopItem, int, Task<ImageSource?>> Icon);

/// <summary>
/// [S-13700]..[S-13706] The Unplaced icons page's live part (the page's one line, drawn as the Appearance's preview is,
/// <see cref="IPagePreview"/>): under the words saying which view it is about, a search box once the list is long
/// (<see cref="UnplacedItems.Searches"/>), then one card of the items, each its shell icon and name and a button that
/// places it, selected as files are (<see cref="ItemSelection"/>: click, Ctrl/Shift+click, the arrows, Ctrl+A, the one
/// look <see cref="SelectionChrome"/>), dragged off onto the desktop to place them where they are let go; under it the
/// selection's actions: Place on this view and Put in each fence. Drawn again from <see cref="UnplacedCommands.Read"/>
/// each time the page is (<see cref="Refresh"/>): a file added to or gone from the desktop, an icon placed or removed.
/// </summary>
internal sealed partial class UnplacedPageView : StackPanel, IPagePreview
{
    private const string Scope = "unplaced-page";

    /// <summary>The shell icon's box in a row (Windows Settings' 20 icon box is too small for a file's picture).</summary>
    private const double IconBox = 24;

    private readonly UnplacedCommands _commands;
    private readonly ItemSelection _selection = new(DesktopItemKeys.Comparer);
    private readonly TextBlock _caption = Fluent.Text(FamilyType.Caption, "TextFillColorSecondaryBrush");
    private readonly TextBox _search;
    private readonly StackPanel _rows = new();
    private readonly Border _card;
    private readonly TextBlock _note = Fluent.Text(FamilyType.Body, "TextFillColorSecondaryBrush");
    private readonly Border _noteCard;
    private readonly StackPanel _foot = new() { Spacing = P.FootGap };
    private readonly List<(DesktopItem Item, Border Chrome)> _shown = [];
    private UnplacedModel _model = new(string.Empty, true, [], []);

    /// <summary>The item a press is on, where it began, and whether it became a drag.</summary>
    private (string Key, Windows.Foundation.Point At)? _press;

    public UnplacedPageView(UnplacedCommands commands)
    {
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        Spacing = P.CardGap;

        _caption.TextWrapping = TextWrapping.Wrap;
        _caption.Margin = new Thickness(0, 0, 0, P.HeadingGap);
        Children.Add(_caption);

        _search = new TextBox
        {
            PlaceholderText = Str.Unplaced_Page_Search.Text(),
            IsSpellCheckEnabled = false,
            IsTextPredictionEnabled = false,
            Margin = new Thickness(0, 0, 0, P.HeadingGap),
            Visibility = Visibility.Collapsed,
        };
        AutomationProperties.SetName(_search, Str.Unplaced_Page_Search.Text());
        _search.TextChanged += (_, _) => Draw();
        Children.Add(_search);

        // [S-13703] The selection's actions over the list, as a command bar: in reach however long the list is.
        _foot.Margin = new Thickness(0, 0, 0, P.HeadingGap);
        Children.Add(_foot);

        _card = Card(_rows);
        _card.KeyDown += OnKey;
        _card.IsTabStop = true;
        _card.UseSystemFocusVisuals = true;
        AutomationProperties.SetName(_card, Str.Unplaced_Page_Name.Text());
        Children.Add(_card);

        _note.TextWrapping = TextWrapping.Wrap;
        _noteCard = Card(new Border { Child = _note, Padding = new Thickness(P.IconAt, P.EndInset, P.EndInset, P.EndInset) });
        Children.Add(_noteCard);

        Refresh();
    }

    /// <summary>[S-13706] The page is drawn again: the desktop and the view as they are now; the selection keeps what is still listed.</summary>
    public void Refresh()
    {
        try
        {
            _model = _commands.Read();
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "the unplaced icons could not be read; the page shows none", ex);
            _model = new(string.Empty, true, [], []);
        }

        _caption.Text = Str.Unplaced_Page_Caption.Format(("view", _model.ViewName));
        _search.Visibility = UnplacedItems.Searches(_model.Items.Count) ? Visibility.Visible : Visibility.Collapsed;
        Draw();
    }

    /// <summary>A render's hand: these items selected as Ctrl+clicks select them.</summary>
    internal void DebugSelect(IReadOnlyList<string> keys)
    {
        foreach (var key in keys)
        {
            _selection.Click(_selection.IndexOf(key), ctrl: true, shift: false);
        }

        Paint();
    }

    /// <summary>A render's hand: words typed into the search box.</summary>
    internal void DebugSearch(string text)
    {
        _search.Text = text;
        Draw();
    }

    public void Dispose()
    {
        // ignore: the page holds no subscription; its display follows the desktop and asks it to draw again.
    }

    /// <summary>[S-13702] The rows of what the search matches, the selection kept over them, and the selection's actions.</summary>
    private void Draw()
    {
        var query = _search.Visibility == Visibility.Visible ? _search.Text : null;
        var items = UnplacedItems.Matching(_model.Items, query);
        _selection.SetOrder([.. items.Select(i => i.Key)]);

        _rows.Children.Clear();
        _shown.Clear();
        for (var i = 0; i < items.Count; i++)
        {
            var row = Row(items[i]);
            _rows.Children.Add(row.Chrome);
            _shown.Add(row);
        }

        _card.Visibility = items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        _noteCard.Visibility = items.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
        _note.Text = !_model.Holds ? Str.Unplaced_Page_NoneHere.Text()
            : _model.Items.Count == 0 ? Str.Unplaced_Page_None.Text()
            : Str.AddMenu_NoResults.Format(("query", (query ?? string.Empty).Trim()));
        Paint();
    }

    /// <summary>One item's row: its shell icon at 16, its name at 48, its place button at the card's end.</summary>
    private (DesktopItem Item, Border Chrome) Row(DesktopItem item)
    {
        var icon = new Image { Width = IconBox, Height = IconBox, Stretch = Stretch.Uniform };
        _ = LoadIconAsync(item, icon);

        var name = Fluent.Text(FamilyType.Body);
        name.Text = item.Name;
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        name.TextWrapping = TextWrapping.NoWrap;
        name.VerticalAlignment = VerticalAlignment.Center;

        var place = Subtle(new Button
        {
            Content = UiLanguage.Mirrored(new FontIcon { Glyph = Glyphs.Add, FontSize = Fluent.IconSize }),
            Width = P.Control,
            Height = P.Control,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            IsEnabled = _model.Holds,
            Tag = item.Key + "#place",
        });
        var placeWords = Str.Unplaced_Page_PlaceOne.Text();
        ToolTipService.SetToolTip(place, placeWords);
        AutomationProperties.SetName(place, placeWords);
        place.Click += (_, _) => Do(() => _commands.Place(UnplacedItems.Acting(_selection.Selected, item.Key)));

        var grid = new Grid
        {
            Height = P.OneLine,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(P.NameAt) },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
            },
            Padding = new Thickness(0, 0, P.EndInset - Fluent.Space2, 0),
            Background = ThemeBrush.Of(ColourRoles.Key(ColourRole.Clear), Colors.Transparent),
        };
        // Centred in the 48 before the name: its middle where a row's 16 icon at 16 has its own.
        icon.HorizontalAlignment = HorizontalAlignment.Center;
        icon.VerticalAlignment = VerticalAlignment.Center;
        grid.Children.Add(icon);
        Grid.SetColumn(name, 1);
        grid.Children.Add(name);
        Grid.SetColumn(place, 2);
        grid.Children.Add(place);

        var chrome = SelectionChrome.Wrap(grid);
        chrome.Tag = item.Key;
        AutomationProperties.SetName(chrome, item.Name);
        chrome.PointerPressed += (_, e) => OnPressed(item.Key, chrome, e);
        chrome.PointerMoved += (_, e) => OnMoved(chrome, e);
        chrome.PointerReleased += (_, e) => OnReleased(item.Key, chrome, e);
        chrome.PointerCaptureLost += (_, _) => _press = null;
        chrome.DoubleTapped += (_, e) =>
        {
            e.Handled = true;
            Do(() => _commands.Place([item.Key]));
        };
        return (item, chrome);
    }

    /// <summary>[S-6621] A left press selects as a file's does (Ctrl toggles, Shift the range); it may become a drag.</summary>
    private void OnPressed(string key, UIElement row, PointerRoutedEventArgs e)
    {
        var point = e.GetCurrentPoint(row);
        if (!point.Properties.IsLeftButtonPressed || IsOnButton(e.OriginalSource as DependencyObject, row))
        {
            return;
        }

        var ctrl = (e.KeyModifiers & VirtualKeyModifiers.Control) != 0;
        var shift = (e.KeyModifiers & VirtualKeyModifiers.Shift) != 0;
        _selection.Press(_selection.IndexOf(key), ctrl, shift);
        _press = (key, e.GetCurrentPoint(this).Position);
        _ = row.CapturePointer(e.Pointer);
        _ = _card.Focus(FocusState.Pointer);
        Paint();
        e.Handled = true;
    }

    /// <summary>[S-13705] Past the drag's dead zone the press drags what it acts on off the page, onto the desktop.</summary>
    private void OnMoved(UIElement row, PointerRoutedEventArgs e)
    {
        if (_press is not { } press || !e.GetCurrentPoint(row).Properties.IsLeftButtonPressed || !_model.Holds)
        {
            return;
        }

        var now = e.GetCurrentPoint(this).Position;
        if (!CardGestures.Travelled(now.X - press.At.X, now.Y - press.At.Y))
        {
            return;
        }

        _press = null;
        _selection.CancelPress();
        row.ReleasePointerCapture(e.Pointer);
        var keys = UnplacedItems.Acting(_selection.Selected, press.Key);
        e.Handled = true;

        // [S-2346] The placement closes the page: never inside the pointer's own event on it.
        if (!DispatcherQueue.TryEnqueue(() => Do(() => _commands.Drag(keys))))
        {
            Log.Warn(Scope, "the drag off the unplaced icons page could not be started");
        }
    }

    private void OnReleased(string key, UIElement row, PointerRoutedEventArgs e)
    {
        if (_press is null)
        {
            return;
        }

        _press = null;
        row.ReleasePointerCapture(e.Pointer);
        _selection.Release(_selection.IndexOf(key));
        Paint();
        e.Handled = true;
    }

    /// <summary>[S-6623] The list's keys as a file list's: the arrows, Home/End, Ctrl+A, Ctrl+Space; Enter places the selection.</summary>
    private void OnKey(object sender, KeyRoutedEventArgs e)
    {
        var shift = SelectionInput.ShiftHeld();
        var ctrl = (Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control) & Windows.UI.Core.CoreVirtualKeyStates.Down) != 0;
        if (WidgetKeys.FromVirtualKey((int)e.Key, shift, ctrl, alt: false) is not { } key)
        {
            return;
        }

        if (key == WidgetKey.Enter)
        {
            if (_selection.Count > 0 && _model.Holds)
            {
                Do(() => _commands.Place(_selection.Selected));
            }

            e.Handled = true;
            return;
        }

        if (_selection.Key(key, shift, columns: 1, pageRows: Math.Max(1, (int)(ActualHeight / P.OneLine))))
        {
            Paint();
            if (_selection.Focus is { } focus && _shown.Find(s => DesktopItemKeys.Comparer.Equals(s.Item.Key, focus)).Chrome is { } row)
            {
                row.StartBringIntoView();
            }

            e.Handled = true;
        }
    }

    /// <summary>Every row's mark as the selection has it, and the foot's actions for it.</summary>
    private void Paint()
    {
        var active = _card.FocusState != FocusState.Unfocused || _selection.Count > 0;
        foreach (var (item, chrome) in _shown)
        {
            SelectionChrome.Paint(chrome, new SelectionCue(_selection.IsSelected(item.Key), false, _selection.IsFocus(item.Key), _selection.Cues, active));
        }

        DrawFoot();
    }

    /// <summary>
    /// [S-13703] [S-13704] Under the list: how many are selected and what to do with them (Place, Put in each fence), or
    /// how to drag them while none is.
    /// </summary>
    private void DrawFoot()
    {
        _foot.Children.Clear();
        _foot.Visibility = _shown.Count == 0 || (!_model.Holds && _model.Fences.Count == 0) ? Visibility.Collapsed : Visibility.Visible;
        if (_foot.Visibility == Visibility.Collapsed)
        {
            return;
        }

        var selected = _selection.Selected;
        var said = Fluent.Text(FamilyType.Caption, "TextFillColorSecondaryBrush");
        said.TextWrapping = TextWrapping.Wrap;
        said.Text = selected.Count > 0 ? Str.Unplaced_Page_Selected.Plural(selected.Count) : Str.Unplaced_Page_Hint.Text();
        _foot.Children.Add(said);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = P.FootGap };
        if (_model.Holds)
        {
            var place = Action(selected.Count > 1 ? Str.Unplaced_Page_PlaceMany.Plural(selected.Count) : Str.Unplaced_Page_PlaceOne.Text(), Glyphs.Add, selected.Count > 0,
                () => _commands.Place(selected), "#place");
            FluentLayout.ApplyStyle(place, "AccentButtonStyle");
            buttons.Children.Add(place);
        }

        foreach (var (id, name) in _model.Fences)
        {
            buttons.Children.Add(Action(Str.Unplaced_Page_PutIn.Format(("fence", name)), Glyphs.DesktopIcons, selected.Count > 0, () => _commands.PutIn(id, selected), "#fence:" + id));
        }

        _foot.Children.Add(new ScrollViewer
        {
            Content = buttons,
            HorizontalScrollMode = ScrollMode.Enabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollMode = ScrollMode.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            IsTabStop = false,
        });
    }

    /// <summary>A standard button of a glyph and words, 32 tall, its words in from its sides.</summary>
    private Button Action(string words, string glyph, bool enabled, Action run, string tag)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = Fluent.Space2 };
        content.Children.Add(UiLanguage.Mirrored(new FontIcon { Glyph = glyph, FontSize = Fluent.IconSize, VerticalAlignment = VerticalAlignment.Center }));
        var text = Fluent.Text(FamilyType.Body);
        text.ClearValue(TextBlock.ForegroundProperty);
        text.Text = words;
        text.VerticalAlignment = VerticalAlignment.Center;
        content.Children.Add(text);
        var button = new Button
        {
            Content = content,
            MinWidth = P.ActionLeast,
            Height = P.Control,
            Padding = new Thickness(P.Label, 0, P.Label, 0),
            IsEnabled = enabled,
            Tag = "unplaced" + tag,
        };
        AutomationProperties.SetName(button, words);
        button.Click += (_, _) => Do(run);
        return button;
    }

    /// <summary>A page card: the cards' fill and rim, radius 4.</summary>
    private static Border Card(UIElement child) => new()
    {
        Child = child,
        Background = ShellBrush.Of(ShellRole.CardFill),
        BorderBrush = ShellBrush.Of(ShellRole.CardStroke),
        BorderThickness = new Thickness(FamilyShape.Hairline),
        CornerRadius = new CornerRadius(P.CardRadius),
        Padding = new Thickness(Fluent.Space1),
    };

    /// <summary>No fill and no rim at rest, the subtle fills under the pointer and pressed (a row's icon button).</summary>
    private static Button Subtle(Button button)
    {
        var clear = ThemeBrush.Of("SubtleFillColorTransparentBrush", Colors.Transparent);
        button.BorderThickness = new Thickness(0);
        button.Resources["ButtonBackground"] = clear;
        button.Resources["ButtonBackgroundPointerOver"] = ThemeBrush.Of("SubtleFillColorSecondaryBrush", Colors.Gray);
        button.Resources["ButtonBackgroundPressed"] = ThemeBrush.Of("SubtleFillColorTertiaryBrush", Colors.DimGray);
        button.Resources["ButtonBackgroundDisabled"] = clear;
        button.Padding = new Thickness(0);
        return button;
    }

    private static bool IsOnButton(DependencyObject? source, UIElement row)
    {
        for (var node = source; node is not null && !ReferenceEquals(node, row); node = VisualTreeHelper.GetParent(node))
        {
            if (node is ButtonBase)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The item's shell icon at the row's box in physical pixels; the box stays empty when the shell has none.</summary>
    private async Task LoadIconAsync(DesktopItem item, Image image)
    {
        try
        {
            var scale = XamlRoot?.RasterizationScale is > 0 and var s ? s : 1.0;
            if (await _commands.Icon(item, (int)Math.Ceiling(IconBox * scale)).ConfigureAwait(true) is { } source)
            {
                image.Source = source;
            }
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, "a desktop item's icon could not be loaded for the unplaced icons page", ex);
        }
    }

    private static void Do(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "an action of the unplaced icons page failed", ex);
        }
    }
}
