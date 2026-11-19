using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;

namespace Themia.Widget.Fence;

/// <summary>
/// [S-10900]..[S-10915] A fence as Fences draws one, in Themia's look: its own title bar (the name, how many items, a
/// click rolls it up) over its own small grid of the desktop's items, each in the desktop's one cell at the icon size in
/// force (<see cref="DesktopCell"/>), in reading order (<see cref="FenceGrid"/>), scrolling with the bottom fade
/// (<see cref="ScrollFade"/>) when they are more than it shows.
/// <para>
/// Its items are the desktop's own ([S-13011]): every gesture on one (a press, the keys, typing, the menu, F2's commit, a
/// double-click, a drag) is handed to the host's one desktop selection under the item's key, as a desktop icon's are
/// (<see cref="IWidgetHost.DesktopItems"/>), and each draws the one item selection's look on a card (<see cref="SelectionLook.Rows"/>).
/// What is the fence's own (where it draws its items, rolling up, its title, drops into it) is asked of
/// <see cref="IDesktopFences"/>. A press on the title bar is the card's (it moves the fence); a press in the body is the
/// items' (it never moves the fence).
/// </para>
/// <para>
/// Split by concern: this file owns the lifecycle, the title bar and the layout; <c>.Items.cs</c> the cells and their
/// gestures; <c>.Drop.cs</c> drops into the fence.
/// </para>
/// </summary>
internal sealed partial class FenceView : Grid, IWidgetView, IWidgetKeys, IWidgetSurfaceAware, IWidgetContextActions, IWidgetTopEdge
{
    private const string Scope = "fence";

    private readonly IWidgetHost _host;

    private readonly Grid _titleBar = new() { Height = FenceGrid.TitleHeight, Padding = new Thickness(FamilySpace.S3, 0, FamilySpace.S3, 0) };
    private readonly TextBlock _title = Family.Text(FamilyType.BodyStrong);
    private readonly TextBlock _count = Family.Text(FamilyType.Caption, ColourRole.TextSecondary);
    private readonly FontIcon _chevron = Family.Glyph(Glyphs.ChevronDown, FamilyShape.Icon, ColourRole.TextSecondary);
    private readonly Border _divider = new() { Height = FamilyShape.Hairline, VerticalAlignment = VerticalAlignment.Bottom };

    private readonly ScrollViewer _scroll = new()
    {
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
        VerticalScrollMode = ScrollMode.Enabled,
        HorizontalScrollMode = ScrollMode.Disabled,
        ZoomMode = ZoomMode.Disabled,
    };

    private readonly Canvas _canvas = new() { Background = Fluent.Clear(), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
    private readonly TextBlock _empty = Family.Text(FamilyType.Caption, ColourRole.TextTertiary);
    private readonly ScrollFade _fade;

    private IWidgetSurface? _surface;
    private FenceConfig _config = new();
    private bool _attached;

    /// <summary>[S-10913] The fence opened by the pointer resting on it (hover peek): it rolls up again when the pointer leaves.</summary>
    private bool _peeking;

    /// <summary>[S-10910] The title's rename box while it shows.</summary>
    private TextBox? _titleBox;

    /// <summary>[S-10912] A click on the title that a double-click may follow: the double-click takes its roll-up back.</summary>
    private bool _titleToggled;

    public FenceView(IWidgetHost host)
    {
        _host = host;
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        _titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _title.VerticalAlignment = VerticalAlignment.Center;
        _title.TextTrimming = TextTrimming.CharacterEllipsis;
        _title.TextWrapping = TextWrapping.NoWrap;
        _count.VerticalAlignment = VerticalAlignment.Center;
        _count.Margin = new Thickness(FamilySpace.S2, 0, 0, 0);
        _chevron.VerticalAlignment = VerticalAlignment.Center;
        _chevron.Margin = new Thickness(FamilySpace.S2, 0, 0, 0);
        SetColumn(_count, 1);
        SetColumn(_chevron, 2);
        _titleBar.Children.Add(_title);
        _titleBar.Children.Add(_count);
        _titleBar.Children.Add(_chevron);
        _divider.Background = Family.Brush(ColourRole.Track);
        _titleBar.Children.Add(_divider);
        SetColumnSpan(_divider, 3);

        // Clear, not null: the bar takes the press (the card's move) and the click (the roll-up).
        _titleBar.Background = Fluent.Clear();
        Children.Add(_titleBar);

        _empty.HorizontalAlignment = HorizontalAlignment.Center;
        _empty.VerticalAlignment = VerticalAlignment.Center;
        _empty.TextAlignment = TextAlignment.Center;
        _empty.TextWrapping = TextWrapping.Wrap;
        _empty.Margin = new Thickness(FamilySpace.S3);
        _empty.IsHitTestVisible = false;
        _scroll.Content = _canvas;
        var body = new Grid { Background = Fluent.Clear() };
        body.Children.Add(_scroll);
        body.Children.Add(_empty);
        SetRow(body, 1);
        Children.Add(body);
        _body = body;

        _fade = new ScrollFade(_scroll, _canvas);

        // [S-13021] Items glide to a new place (a drop, the preview stepping aside, a reorder) rather than jump.
        _canvas.ChildrenTransitions = new Microsoft.UI.Xaml.Media.Animation.TransitionCollection { new Microsoft.UI.Xaml.Media.Animation.RepositionThemeTransition() };
        SizeChanged += (_, _) => Layout();
        _scroll.ViewChanged += (_, _) => ReportBoxes();
    }

    private readonly Grid _body;

    /// <summary>[S-10901] The title bar is the card's top edge: the card adds no inset above it.</summary>
    public bool OwnsTopEdge => true;

    /// <summary>The fence's widget id: what it is asked by. Null in a render.</summary>
    private string? WidgetId => _surface?.WidgetId;

    /// <summary>Whether gestures act: on the desktop, outside edit mode, never in a preview.</summary>
    private bool Acts => _attached && _surface?.EditMode != true && _surface?.IsPreview != true && !_fixture.On && !_host.IsPreview && WidgetId is not null;

    private IDesktopFences Fences => _host.DesktopItems.Fences;

    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        Detach();
        _attached = true;
        _host.DesktopItems.Changed += OnDeskChanged;
        Strings.Changed += OnLanguage;
        _titleBar.Tapped += OnTitleTapped;
        _titleBar.DoubleTapped += OnTitleDoubleTapped;
        _body.PointerPressed += OnBodyPressed;
        PointerEntered += OnPointerEntered;
        PointerExited += OnPointerExited;
        ApplyConfig(config);
    }

    public void ApplyConfig(WidgetConfig config)
    {
        var before = _config;
        _config = config as FenceConfig ?? new FenceConfig();
        if (!ReferenceEquals(before, _config) || _order is null || !SameItems())
        {
            _order = null;
        }

        Render();

        // [S-13023] A search fence searches when shown, and again when its rules or query changed.
        SearchIfNeeded();
    }

    public void Detach()
    {
        if (!_attached)
        {
            return;
        }

        _attached = false;
        _host.DesktopItems.Changed -= OnDeskChanged;
        Strings.Changed -= OnLanguage;
        _titleBar.Tapped -= OnTitleTapped;
        _titleBar.DoubleTapped -= OnTitleDoubleTapped;
        _body.PointerPressed -= OnBodyPressed;
        PointerEntered -= OnPointerEntered;
        PointerExited -= OnPointerExited;
        CloseTitleBox(commit: false);
        CancelSearch();
        _searchedFor = null;
        ClearCells();
        if (WidgetId is { } id)
        {
            Fences.Laid(id, []);
        }
    }

    private void OnLanguage(Locale locale) => Render();

    /// <summary>The listing, the selection, a rename box or a drag changed: the items may have new names, icons or looks.</summary>
    private void OnDeskChanged()
    {
        if (_order is not null && !SameItems())
        {
            _order = null;
        }

        Render();
    }

    // ---- keys ([S-13011]) ---------------------------------------------------------------------------

    /// <summary>The keys with the lease are the desktop's, from this fence (so a paste or a new folder lands here); F2 with no item of it focused renames the fence.</summary>
    public bool HandleKey(WidgetKey key)
    {
        if (key == WidgetKey.Escape || !Acts || _titleBox is not null)
        {
            return false;
        }

        // [S-13023] A search fence's files are its own: its keys work on them.
        if (Searches && key != WidgetKey.Rename)
        {
            return FoundKey(key);
        }

        if (_host.DesktopItems.Key(key, SelectionInput.ShiftHeld(), WidgetId))
        {
            return true;
        }

        if (key == WidgetKey.Rename)
        {
            OpenTitleBox();
            return true;
        }

        return false;
    }

    public bool HandleText(char character) => Acts && _titleBox is null && !Searches && _host.DesktopItems.Type(character);

    public bool KeepsSelectionWhenLeft => true;

    // ---- the card's menu ([S-10911]) ---------------------------------------------------------------

    /// <summary>[S-10911] The fence's own commands on its card's menu: Paste and New folder into it, Rename, Roll up, Remove fence.</summary>
    public IReadOnlyList<WidgetAction> ContextActions
    {
        get
        {
            if (!Acts || WidgetId is not { } id)
            {
                return [];
            }

            // [S-13023] A search fence holds no file: nothing is pasted or made in it; it searches again.
            if (Searches)
            {
                return
                [
                    new(Glyphs.Refresh, Str.Common_Refresh.Text(), () => SearchIfNeeded(again: true)),
                    new(Glyphs.Rename, FenceStr.Fence_Rename.Text(), OpenTitleBox),
                    new(_config.RolledUp ? Glyphs.ChevronDown : Glyphs.ChevronUp, (_config.RolledUp ? FenceStr.Fence_Unroll : FenceStr.Fence_RollUp).Text(), () => Fences.RollUp(id, !_config.RolledUp)),
                    new(Glyphs.Delete, FenceStr.Fence_Remove.Text(), () => _ = RemoveAsync(id)),
                ];
            }

            return
            [
                new(Glyphs.Paste, Str.CtxMenu_Paste.Text(), () => Fences.Paste(id)),
                new(Glyphs.NewFolder, Str.Shell_Files_NewFolder.Text(), () => Fences.NewFolder(id)),
                new(Glyphs.Rename, FenceStr.Fence_Rename.Text(), OpenTitleBox),
                new(_config.RolledUp ? Glyphs.ChevronDown : Glyphs.ChevronUp, (_config.RolledUp ? FenceStr.Fence_Unroll : FenceStr.Fence_RollUp).Text(), () => Fences.RollUp(id, !_config.RolledUp)),
                new(Glyphs.Delete, FenceStr.Fence_Remove.Text(), () => _ = RemoveAsync(id)),
            ];
        }
    }

    /// <summary>
    /// [S-13009] Remove fence: never a file. With items, it asks first ("Put the 3 items back on the desktop?", Put back
    /// the action); the answer puts them back where it stood, Cancel keeps the fence. An empty fence goes at once.
    /// </summary>
    private async Task RemoveAsync(string id)
    {
        try
        {
            var count = _config.Items.Count;
            if (count == 0 || await ConfirmFlyout.AskAsync(_titleBar, FenceStr.Fence_PutBack.Plural(count), FenceStr.Fence_PutBack_Action.Text()).ConfigureAwait(true))
            {
                Fences.Remove(id, putBack: true);
            }
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "removing a fence failed", ex);
        }
    }

    // ---- the title bar ([S-10910], [S-10912], [S-10913]) ------------------------------------------

    /// <summary>[S-10912] A click on the title rolls the fence up or unrolls it (the setting's); one that peeked it open keeps it open.</summary>
    private void OnTitleTapped(object sender, TappedRoutedEventArgs e)
    {
        _titleToggled = false;
        if (!Acts || _titleBox is not null || WidgetId is not { } id)
        {
            return;
        }

        if (_peeking)
        {
            _peeking = false;
            return;
        }

        if (_config.RollUpOnClick)
        {
            _titleToggled = true;
            Fences.RollUp(id, !_config.RolledUp);
        }
    }

    /// <summary>[S-10910] A double-click on the title renames the fence (the click before it rolled nothing for good: taken back).</summary>
    private void OnTitleDoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (!Acts || WidgetId is not { } id)
        {
            return;
        }

        e.Handled = true;
        if (_titleToggled)
        {
            _titleToggled = false;
            Fences.RollUp(id, !_config.RolledUp);
        }

        OpenTitleBox();
    }

    /// <summary>[S-10913] The pointer rests on a rolled-up fence: it opens while the pointer is over it.</summary>
    private void OnPointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (Acts && _config.RolledUp && _config.HoverPeek && WidgetId is { } id && !_peeking)
        {
            _peeking = true;
            Fences.RollUp(id, false);
        }
    }

    /// <summary>[S-10913] The pointer left a fence it peeked open: it rolls up again (unless a drag or a rename holds it).</summary>
    private void OnPointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (_peeking && WidgetId is { } id && _titleBox is null && _renameCell is null && !Fences.DeskDragRunning)
        {
            _peeking = false;
            Fences.RollUp(id, true);
        }
    }

    /// <summary>[S-10910] The title becomes a box (F2, a double-click, the menu's Rename): Enter or a click elsewhere keeps it, Escape takes it back.</summary>
    private void OpenTitleBox()
    {
        if (_titleBox is not null || (!Acts && !_fixture.On))
        {
            return;
        }

        var box = new TextBox
        {
            Text = _config.Name.Length > 0 ? _config.Name : FenceStr.Fence_Label.Text(),
            AcceptsReturn = false,
            IsSpellCheckEnabled = false,
            MinHeight = 0,
            Padding = new Thickness(FamilySpace.S1, 0, FamilySpace.S1, 0),
            VerticalAlignment = VerticalAlignment.Center,
            FontSize = FamilyType.Body.FontSize,
            MaxLength = FenceConfig.MaxName,
        };
        _titleBox = box;
        _title.Visibility = Visibility.Collapsed;
        _titleBar.Children.Add(box);
        if (_fixture.On)
        {
            box.Loaded += (_, _) => box.SelectAll();
            return;
        }

        box.GotFocus += (_, _) => _surface?.RequestTextInput(true);
        box.LostFocus += (_, _) => CloseTitleBox(commit: true);
        box.KeyDown += (_, args) =>
        {
            if (args.Key == Windows.System.VirtualKey.Enter)
            {
                args.Handled = true;
                CloseTitleBox(commit: true);
            }
            else if (args.Key == Windows.System.VirtualKey.Escape)
            {
                args.Handled = true;
                CloseTitleBox(commit: false);
            }
        };
        box.Loaded += (_, _) =>
        {
            box.Focus(FocusState.Programmatic);
            box.SelectAll();
        };
    }

    private void CloseTitleBox(bool commit)
    {
        if (_titleBox is not { } box)
        {
            return;
        }

        _titleBox = null;
        _titleBar.Children.Remove(box);
        _title.Visibility = Visibility.Visible;
        _surface?.RequestTextInput(false);
        if (commit && WidgetId is { } id)
        {
            var name = FenceConfig.CleanName(box.Text);
            Fences.Rename(id, string.Equals(name, FenceStr.Fence_Label.Text(), StringComparison.Ordinal) ? string.Empty : name);
        }
    }

    /// <summary>
    /// [S-13011] A press on the body is the items', never the card's move: the fence moves by its title bar. [S-13020] A left
    /// press on its empty body starts the desktop's band there, as on empty desktop: it selects this fence's items (and the
    /// free icons and other fences' it reaches) by the desktop's rules.
    /// </summary>
    private void OnBodyPressed(object sender, PointerRoutedEventArgs e)
    {
        if (!Acts)
        {
            return;
        }

        e.Handled = true;

        // [S-13023] A search fence's files are in no desktop selection: a press on its empty body clears its own.
        if (Searches)
        {
            if (_picks.Clear())
            {
                PaintCells();
            }

            return;
        }

        if (WidgetId is { } id && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _ = Fences.Band(id, e);
        }
    }

    // ---- drawing ------------------------------------------------------------------------------------

    /// <summary>[S-10901] [S-10902] The title bar and the items as the config and the desktop say now.</summary>
    private void Render()
    {
        var items = ShownItems();
        _title.Text = _config.Name.Length > 0 ? _config.Name : FenceStr.Fence_Label.Text();
        _count.Text = FenceStr.Fence_Count.Plural(items.Count);
        _chevron.Glyph = _config.RolledUp ? Glyphs.ChevronDown : Glyphs.ChevronUp;
        _divider.Visibility = _config.RolledUp ? Visibility.Collapsed : Visibility.Visible;
        _body.Visibility = _config.RolledUp ? Visibility.Collapsed : Visibility.Visible;
        _empty.Text = Searches && !_fixture.On ? SearchEmptyText() : FenceStr.Fence_Empty.Text();
        _empty.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        SyncCells(items);
        Layout();
        PaintCells();
    }

    /// <summary>[S-10902] The cells on the fence's own grid at the width it has now, then where they are told to the host.</summary>
    private void Layout()
    {
        var cell = CellSize;
        var width = Math.Max(0, ActualWidth);
        var columns = FenceGrid.Columns(width, cell);

        // [S-13021] While the desktop's drag is over the fence, its preview's order: the dragged ones at their slots.
        var order = _preview ?? _shown;
        for (var i = 0; i < order.Count; i++)
        {
            if (_cells.TryGetValue(order[i], out var view) || _ghosts.TryGetValue(order[i], out view))
            {
                var r = FenceGrid.Cell(i, columns, cell, width);
                Canvas.SetLeft(view.Root, r.X);
                Canvas.SetTop(view.Root, r.Y);
                view.Root.Width = r.Width;
                view.Root.Height = r.Height;
            }
        }

        _canvas.Width = width;
        _canvas.Height = FenceGrid.ContentHeight(order.Count, columns, cell);
        _fade.Refresh();
        ReportBoxes();
    }

    /// <summary>[S-13011] Where the items shown now stand in the card, told to the host for the band, the arrows and the drag; none while rolled up.</summary>
    private void ReportBoxes()
    {
        if (!_attached || WidgetId is not { } id || _fixture.On)
        {
            return;
        }

        // [S-13023] A search fence's files are no desktop items: nothing of it is in the desktop's selection.
        if (_config.RolledUp || _scroll.ViewportHeight <= 0 || Searches)
        {
            Fences.Laid(id, []);
            return;
        }

        var cell = CellSize;
        var width = Math.Max(0, ActualWidth);
        var columns = FenceGrid.Columns(width, cell);
        var top = FenceGrid.TitleHeight - _scroll.VerticalOffset;
        var bottom = FenceGrid.TitleHeight + _scroll.ViewportHeight;
        var boxes = new List<FenceItemBox>(_shown.Count);
        for (var i = 0; i < _shown.Count; i++)
        {
            var r = FenceGrid.Cell(i, columns, cell, width);
            var y = top + r.Y;
            if (y + r.Height > FenceGrid.TitleHeight && y < bottom)
            {
                boxes.Add(new FenceItemBox(_shown[i], r.X, y, r.Width, r.Height));
            }
        }

        Fences.Laid(id, boxes);
    }

    /// <summary>
    /// [S-5593] [S-13019] The box an item stands in: a desktop icon's on the widget grid of the fence's display (the host's),
    /// so the items keep the free icons' spacing; in a picture the desktop's plain cell.
    /// </summary>
    private SizeDip CellSize => WidgetId is { } id && !_fixture.On && !_host.IsPreview ? Fences.ItemBox(id) : DesktopCell.Grid(DesktopCell.Current);
}
