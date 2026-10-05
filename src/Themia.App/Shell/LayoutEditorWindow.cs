using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Themia.App.Interop;
using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Windows.Graphics;

namespace Themia.App.Shell;

/// <summary>
/// [S-1411]..[S-1414] The app-layout editor (original <c>LayoutEditor.tsx</c>): one window for the
/// whole app, opened on demand from a layout's item menu or its settings row, styled like the
/// Windows 11 Settings window — Mica, the tall title bar, a page title, rows on cards.
/// <para>
/// The page is a name row and a picture of the display: panes that split side by side or one
/// above the other, dividers that drag (5–95 %), and in each pane what it opens — an app from the
/// Start menu, a web address, a file or a folder — chosen in the pane itself (original S-60).
/// </para>
/// <para>
/// <b>It never holds the layout.</b> It holds the item's id and finds the item in the host's state
/// for every edit, writing through <see cref="AppHost.Mutate"/> — the one write path — so an undo,
/// an import or the widget being deleted can never leave it editing a copy nobody sees. If the item
/// goes, the page says so.
/// </para>
/// </summary>
internal sealed class LayoutEditorWindow
{
    private const string Scope = "layout-editor";

    /// <summary>The gap between panes, which is also the divider's grab area.</summary>
    private const double DividerSize = 8;

    /// <summary>The tallest the pane picture gets.</summary>
    private const double MaxCanvasHeight = 440;

    /// <summary>What a pane can open, labelled in the current language (built per pane, not cached).</summary>
    private static (LayoutSource Value, string Label)[] Sources() =>
    [
        (LayoutSource.App, Str.Shortcut_KindApp.Text()),
        (LayoutSource.Url, Str.WidgetsC_Shortcuts_Kind_Web.Text()),
        (LayoutSource.File, Str.Shortcut_ChipFile.Text()),
        (LayoutSource.Folder, Str.Shortcut_KindFolder.Text()),
    ];

    private readonly AppHost _host;
    private readonly Window _window = new();
    private readonly Grid _root = new();
    private readonly Grid _titleBar = new() { Height = 48 };
    private readonly TextBlock _caption = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) };
    private readonly TextBlock _summary = new();
    private readonly Border _canvas = new();
    private readonly Border _canvasCard;
    private readonly StackPanel _page = new() { Spacing = 4, MaxWidth = 1000 };
    private readonly InfoBar _gone = new()
    {
        Severity = InfoBarSeverity.Warning,
        IsClosable = false,
    };

    /// <summary>The name box: made anew with the page on a language change, since its row is.</summary>
    private TextBox _name = new();

    /// <summary>The source chosen for an empty pane (switching it clears the assignment, S-60).</summary>
    private readonly Dictionary<string, LayoutSource> _paneSource = new(StringComparer.Ordinal);

    private string? _itemId;
    private string _rendered = string.Empty;
    private double _aspect = 16.0 / 9;
    private bool _closed;
    private IReadOnlyList<InstalledApp> _apps = [];
    private Task? _appsLoading;

    public LayoutEditorWindow(AppHost host)
    {
        _host = host;
        _canvasCard = FluentLayout.Card(_canvas);
        BuildChrome();
        ConfigureWindow();
        _host.StateChanged += OnStateChanged;
        Strings.Changed += OnLanguage;
    }

    /// <summary>Raised once, when the window has closed.</summary>
    public event Action? Closed;

    /// <summary>Show the editor on an item, bringing the window forward (and retargeting it, S-58).</summary>
    public void Show(string itemId)
    {
        if (_closed)
        {
            return;
        }

        if (_itemId != itemId)
        {
            _paneSource.Clear();
        }

        _itemId = itemId;
        _rendered = string.Empty;
        Render();
        _window.Activate();
        LoadApps();
    }

    public void Close() => _window.Close();

#if DEBUG
    /// <summary>The window, for the debug render (<c>--layout-editor-png</c>).</summary>
    internal Window DebugWindow => _window;

    /// <summary>The content root, for the debug render.</summary>
    internal FrameworkElement DebugRoot => _root;

    /// <summary>Retarget without activating, for the debug render.</summary>
    internal void DebugShow(string itemId)
    {
        _itemId = itemId;
        _rendered = string.Empty;
        Render();
    }
#endif

    // ---- chrome -----------------------------------------------------------------------------------

    private void BuildChrome()
    {
        _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var icon = new Image
        {
            Width = 16,
            Height = 16,
            Margin = new Thickness(16, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Source = new BitmapImage
            {
                DecodePixelType = DecodePixelType.Logical,
                DecodePixelWidth = 16,
                UriSource = new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", "themia-logo.png")),
            },
        };

        FluentLayout.ApplyStyle(_caption, "CaptionTextBlockStyle");

        _titleBar.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Children = { icon, _caption } });

        _summary.Margin = new Thickness(0, 30, 0, 6);
        FluentLayout.ApplyStyle(_summary, "SettingsSectionHeaderTextBlockStyle");

        _canvas.SizeChanged += (_, e) => FitCanvas(e.NewSize.Width);

        _gone.IsOpen = false;

        BuildPage();

        var scroll = new ScrollViewer
        {
            Content = new Border { Child = _page, Padding = new Thickness(36, 8, 36, 36) },
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        };

        Grid.SetRow(_titleBar, 0);
        Grid.SetRow(scroll, 1);
        _root.Children.Add(_titleBar);
        _root.Children.Add(scroll);

        // [S-1411] Escape closes, as the original's window did.
        _root.KeyDown += (_, e) =>
        {
            if (e.Key == Windows.System.VirtualKey.Escape)
            {
                Close();
                e.Handled = true;
            }
        };

        _window.Content = _root;
    }

    /// <summary>
    /// The page's words and the rows that carry them, built again on a language change. The parts
    /// that hold state — the "removed" bar, the summary, the pane picture's card — are kept and
    /// moved over; the name box is made new because the row that frames it is.
    /// </summary>
    private void BuildPage()
    {
        _page.Children.Clear();

        _caption.Text = ShortcutRules.LayoutKindLabel();
        _window.Title = ShortcutRules.LayoutKindLabel();
        _gone.Title = Str.WidgetsC_Shortcuts_LayoutEditor_Gone_Title.Text();
        _gone.Message = Str.WidgetsC_Shortcuts_LayoutEditor_Gone_Message.Text();

        var title = new TextBlock { Text = Str.WidgetsC_Shortcuts_LayoutEditor_Title.Text(), Margin = new Thickness(0, 4, 0, 4) };
        FluentLayout.ApplyStyle(title, "TitleTextBlockStyle");

        _name = new TextBox { Width = 280, PlaceholderText = ShortcutRules.LayoutKindLabel() };
        var name = _name;
        name.KeyDown += (_, e) =>
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                CommitName();
                e.Handled = true;
            }
        };
        name.LostFocus += (_, _) => CommitName();

        var done = new Button { Content = Str.Common_Done.Text(), HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        FluentLayout.ApplyStyle(done, "AccentButtonStyle");
        done.Click += (_, _) => Close();

        _page.Children.Add(title);
        _page.Children.Add(FluentLayout.Note(Str.WidgetsC_Shortcuts_LayoutEditor_Note.Text()));
        _page.Children.Add(_gone);
        _page.Children.Add(FluentLayout.Header(Str.Common_General.Text()));
        _page.Children.Add(FluentLayout.Row(Glyphs.Rename, Str.Connect_Field_Name.Text(), Str.WidgetsC_Shortcuts_LayoutEditor_Name_Desc.Text(), _name));
        _page.Children.Add(_summary);
        _page.Children.Add(_canvasCard);
        _page.Children.Add(done);
    }

    /// <summary>The language changed: the page's words, then the panes (their controls carry words too).</summary>
    private void OnLanguage(Locale locale)
    {
        if (_closed)
        {
            return;
        }

        BuildPage();
        _rendered = string.Empty;
        Render();
    }

    private void ConfigureWindow()
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);
        var dpi = Win32.GetDpiForWindow(hwnd);
        var scale = dpi > 0 ? dpi / 96.0 : 1.0;
        _window.AppWindow.Resize(new SizeInt32((int)Math.Round(1000 * scale), (int)Math.Round(760 * scale)));

        AppIcon.Apply(_window.AppWindow);

        _window.SystemBackdrop = new MicaBackdrop();
        _window.ExtendsContentIntoTitleBar = true;
        _window.SetTitleBar(_titleBar);
        _window.AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;

        // The pane picture has the shape of the display a layout usually opens on.
        try
        {
            var work = DisplayArea.Primary.WorkArea;
            _aspect = work.Height > 0 ? Math.Clamp((double)work.Width / work.Height, 1.2, 2.6) : _aspect;
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, "could not read the primary display's shape; using 16:9", ex);
        }

        ApplyTheme();

        _window.Closed += (_, _) =>
        {
            _closed = true;
            _host.StateChanged -= OnStateChanged;
            Strings.Changed -= OnLanguage;
            Closed?.Invoke();
        };
    }

    private void ApplyTheme()
    {
        _root.RequestedTheme = DesignApplier.Theme;

        try
        {
            _window.AppWindow.TitleBar.PreferredTheme = DesignApplier.IsDark ? TitleBarTheme.Dark : TitleBarTheme.Light;
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, "could not theme the caption buttons", ex);
        }
    }

    /// <summary>
    /// The picture keeps the display's shape: as wide as the card, unless that would make it taller
    /// than <see cref="MaxCanvasHeight"/>, when it is that tall and centred — the whole page then
    /// fits the window's default size.
    /// </summary>
    private void FitCanvas(double width)
    {
        var inner = Math.Max(200, width - _canvas.Padding.Left - _canvas.Padding.Right);
        var height = Math.Round(inner / _aspect);
        var wide = inner;

        if (height > MaxCanvasHeight)
        {
            height = MaxCanvasHeight;
            wide = Math.Round(height * _aspect);
        }

        if (_canvas.Child is FrameworkElement child && (Math.Abs(child.Height - height) > 0.5 || Math.Abs(child.Width - wide) > 0.5))
        {
            child.Height = height;
            child.Width = wide;
            child.HorizontalAlignment = HorizontalAlignment.Center;
        }
    }

    // ---- state ------------------------------------------------------------------------------------

    private void OnStateChanged()
    {
        if (_closed)
        {
            return;
        }

        ApplyTheme();
        Render();
    }

    private (WidgetInstance Widget, ShortcutItem Item)? Target() => _itemId is null ? null : _host.FindShortcut(_itemId);

    /// <summary>
    /// Draw the page for the item as it is now. Skipped when nothing it shows changed, so an
    /// unrelated save never rebuilds the panes under an open drop-down.
    /// </summary>
    private void Render()
    {
        if (Target() is not var (_, item) || item.Layout is not { } layout)
        {
            _gone.IsOpen = true;
            _canvas.Child = null;
            _rendered = string.Empty;
            return;
        }

        var fingerprint = item.Label + "|" + Fingerprint(layout);

        if (fingerprint == _rendered)
        {
            return;
        }

        _rendered = fingerprint;
        _gone.IsOpen = false;

        if (_root.XamlRoot is not { } xamlRoot || !ReferenceEquals(FocusManager.GetFocusedElement(xamlRoot), _name))
        {
            _name.Text = item.Label;
        }

        _summary.Text = Str.WidgetsC_Shortcuts_LayoutEditor_Panes.Format(("apps", LayoutPlan.Summary(layout)));

        var single = layout.Kind == LayoutNodeKind.Pane;
        var host = new Grid { Height = 360 };
        host.Children.Add(Build(layout, single));
        _canvas.Child = host;
        _canvas.Padding = new Thickness(12);
        FitCanvas(_canvas.ActualWidth);
    }

    private static string Fingerprint(LayoutNode node) => node.Kind == LayoutNodeKind.Split
        ? $"S{node.Id}:{node.Direction}:{node.Ratio:0.####}({(node.A is null ? "-" : Fingerprint(node.A))},{(node.B is null ? "-" : Fingerprint(node.B))})"
        : $"P{node.Id}:{node.Assignment?.Source}:{node.Assignment?.Target}:{node.Assignment?.Label}:{node.Assignment?.ReuseIfRunning}";

    /// <summary>Change the item's layout through the host's one write path, then redraw.</summary>
    private void Edit(Func<LayoutNode, LayoutNode?> change)
    {
        if (Target() is not var (_, item) || item.Layout is not { } layout)
        {
            Render();
            return;
        }

        LayoutNode? next = null;
        _host.Mutate(_ =>
        {
            next = change(layout);

            if (next is not null)
            {
                item.Layout = next;
            }
        });

        if (next is null)
        {
            return;
        }

        Render();
    }

    private void CommitName()
    {
        if (Target() is not var (_, item))
        {
            return;
        }

        var name = _name.Text.Trim();

        if (name != item.Label)
        {
            _host.Mutate(_ => item.Label = name);
        }
    }

    // ---- the pane picture ([S-1412]) ----------------------------------------------------------------

    private FrameworkElement Build(LayoutNode node, bool single)
    {
        if (node.Kind == LayoutNodeKind.Pane || node.A is null || node.B is null)
        {
            return BuildPane(node, single);
        }

        var grid = new Grid();
        var vertical = node.Direction == SplitDirection.Vertical;
        var ratio = LayoutTree.ClampRatio(node.Ratio);

        var first = new GridLength(ratio, GridUnitType.Star);
        var second = new GridLength(1 - ratio, GridUnitType.Star);

        if (vertical)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = first });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(DividerSize) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = second });
        }
        else
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = first });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(DividerSize) });
            grid.RowDefinitions.Add(new RowDefinition { Height = second });
        }

        var a = Build(node.A, single: false);
        var b = Build(node.B, single: false);
        var divider = BuildDivider(node, grid, vertical);

        Place(a, 0, vertical);
        Place(divider, 1, vertical);
        Place(b, 2, vertical);

        grid.Children.Add(a);
        grid.Children.Add(divider);
        grid.Children.Add(b);
        return grid;
    }

    private static void Place(FrameworkElement element, int index, bool vertical)
    {
        if (vertical)
        {
            Grid.SetColumn(element, index);
        }
        else
        {
            Grid.SetRow(element, index);
        }
    }

    /// <summary>
    /// [S-1412] A divider: drags to set the split's share (held to 5–95 %), live while dragging and
    /// saved once on release.
    /// </summary>
    private FrameworkElement BuildDivider(LayoutNode split, Grid grid, bool vertical)
    {
        var grip = new FontIcon
        {
            Glyph = vertical ? Glyphs.GripperVertical : Glyphs.GripperHorizontal,
            FontSize = 10,
            Foreground = FluentLayout.Themed("TextFillColorTertiaryBrush", Colors.Gray),
        };

        var divider = new Border
        {
            Background = FluentLayout.Themed("SubtleFillColorTransparentBrush", Colors.Transparent),
            CornerRadius = new CornerRadius(DividerSize / 2),
            Child = grip,
        };

        ToolTipService.SetToolTip(divider, Str.LayoutEditor_DragResize.Text());
        var dragging = false;
        var ratio = LayoutTree.ClampRatio(split.Ratio);

        divider.PointerEntered += (_, _) => divider.Background = FluentLayout.Themed("SubtleFillColorSecondaryBrush", Colors.Gray);
        divider.PointerExited += (_, _) =>
        {
            if (!dragging)
            {
                divider.Background = FluentLayout.Themed("SubtleFillColorTransparentBrush", Colors.Transparent);
            }
        };

        divider.PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(divider).Properties.IsLeftButtonPressed && divider.CapturePointer(e.Pointer))
            {
                dragging = true;
                e.Handled = true;
            }
        };

        divider.PointerMoved += (_, e) =>
        {
            if (!dragging)
            {
                return;
            }

            var point = e.GetCurrentPoint(grid).Position;
            var total = (vertical ? grid.ActualWidth : grid.ActualHeight) - DividerSize;

            if (total <= 0)
            {
                return;
            }

            ratio = LayoutTree.ClampRatio(((vertical ? point.X : point.Y) - DividerSize / 2) / total);

            if (vertical)
            {
                grid.ColumnDefinitions[0].Width = new GridLength(ratio, GridUnitType.Star);
                grid.ColumnDefinitions[2].Width = new GridLength(1 - ratio, GridUnitType.Star);
            }
            else
            {
                grid.RowDefinitions[0].Height = new GridLength(ratio, GridUnitType.Star);
                grid.RowDefinitions[2].Height = new GridLength(1 - ratio, GridUnitType.Star);
            }

            e.Handled = true;
        };

        void Release()
        {
            if (!dragging)
            {
                return;
            }

            dragging = false;
            divider.ReleasePointerCaptures();
            divider.Background = FluentLayout.Themed("SubtleFillColorTransparentBrush", Colors.Transparent);
            var id = split.Id;
            Edit(root => LayoutTree.SetRatio(root, id, ratio) ? root : null);
        }

        divider.PointerReleased += (_, _) => Release();
        divider.PointerCaptureLost += (_, _) => Release();

        return divider;
    }

    /// <summary>
    /// [S-1413] One pane: split and remove at its top right, and in its middle what it opens — the
    /// icon and name when assigned, the source, the target control and "Clear" (original S-59/S-60).
    /// The middle shrinks with the pane rather than clipping.
    /// </summary>
    private FrameworkElement BuildPane(LayoutNode pane, bool single)
    {
        var assignment = pane.Assignment;
        var source = assignment?.Source ?? (_paneSource.TryGetValue(pane.Id, out var chosen) ? chosen : LayoutSource.App);

        var tools = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(4),
            Spacing = 2,
        };

        tools.Children.Add(Tool(Glyphs.SplitSideBySide, Str.LayoutEditor_SplitVertical.Text(), () => Edit(root => Split(root, pane.Id, SplitDirection.Vertical))));
        tools.Children.Add(Tool(Glyphs.SplitStacked, Str.LayoutEditor_SplitHorizontal.Text(), () => Edit(root => Split(root, pane.Id, SplitDirection.Horizontal))));

        if (!single)
        {
            tools.Children.Add(Tool(Glyphs.Dismiss, Str.LayoutEditor_RemovePane.Text(), () => Edit(root => LayoutTree.Remove(root, pane.Id).Root)));
        }

        var middle = new StackPanel
        {
            Spacing = 8,
            Width = 260,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        if (assignment is not null)
        {
            middle.Children.Add(AssignedHeader(assignment));
        }
        else
        {
            var hint = Fluent.Text(FluentType.Body, "TextFillColorSecondaryBrush");
            hint.Text = Str.WidgetsC_Shortcuts_LayoutEditor_EmptyPane.Text();
            hint.HorizontalAlignment = HorizontalAlignment.Center;
            middle.Children.Add(hint);
        }

        var sourceBox = FluentLayout.Choice(Sources(), source, value =>
        {
            if (value == source)
            {
                return;
            }

            // [S-1413] Switching the source clears what the pane opened.
            _paneSource[pane.Id] = value;
            _rendered = string.Empty;

            if (pane.Assignment is not null)
            {
                Edit(root => LayoutTree.Assign(root, pane.Id, null) ? root : null);
            }
            else
            {
                Render();
            }
        });

        sourceBox.HorizontalAlignment = HorizontalAlignment.Stretch;
        middle.Children.Add(sourceBox);
        middle.Children.Add(TargetControl(pane, source, assignment));

        if (assignment is not null)
        {
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            if (source is LayoutSource.App or LayoutSource.File)
            {
                var reuse = new CheckBox { Content = Str.WidgetsC_Shortcuts_LayoutEditor_Reuse.Text(), IsChecked = assignment.ReuseIfRunning };
                reuse.Click += (_, _) =>
                {
                    var next = assignment.Clone();
                    next.ReuseIfRunning = reuse.IsChecked == true;
                    Edit(root => LayoutTree.Assign(root, pane.Id, next) ? root : null);
                };
                row.Children.Add(reuse);
            }

            var clear = new Button { Content = Str.Common_Clear.Text(), VerticalAlignment = VerticalAlignment.Center };
            clear.Click += (_, _) => Edit(root => LayoutTree.Assign(root, pane.Id, null) ? root : null);
            Grid.SetColumn(clear, 1);
            row.Children.Add(clear);
            middle.Children.Add(row);
        }

        var body = new Grid();
        body.Children.Add(new Viewbox
        {
            Child = middle,
            StretchDirection = StretchDirection.DownOnly,
            Margin = new Thickness(12, 36, 12, 12),
        });
        body.Children.Add(tools);

        return new Border
        {
            Child = body,
            CornerRadius = new CornerRadius(Fluent.ControlRadius * 2),
            BorderThickness = new Thickness(1),
            BorderBrush = FluentLayout.Themed("CardStrokeColorDefaultBrush", Colors.Gray),
            Background = assignment is null
                ? FluentLayout.Themed("SubtleFillColorSecondaryBrush", Colors.Gray)
                : FluentLayout.Themed("LayerFillColorDefaultBrush", Colors.Gray),
        };
    }

    private static LayoutNode? Split(LayoutNode root, string paneId, SplitDirection direction)
    {
        var (next, changed) = LayoutTree.Split(root, paneId, direction);

        if (!changed)
        {
            Log.Info(Scope, "a pane at the nesting limit was not split");
        }

        return changed ? next : null;
    }

    private static Button Tool(string glyph, string tooltip, Action action)
    {
        var button = FluentLayout.IconCommand(glyph, tooltip);
        button.Width = 28;
        button.Height = 28;
        button.Background = FluentLayout.Themed("SubtleFillColorTransparentBrush", Colors.Transparent);
        button.BorderThickness = new Thickness(0);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, tooltip);
        button.Click += (_, _) => action();
        return button;
    }

    /// <summary>The assigned app's icon and name, centred above the controls.</summary>
    private static FrameworkElement AssignedHeader(LayoutAssignment assignment)
    {
        var glyph = Fluent.Icon(assignment.Source switch
        {
            LayoutSource.Url => Glyphs.Globe,
            LayoutSource.Folder => Glyphs.Folder,
            LayoutSource.File => Glyphs.File,
            _ => Glyphs.Apps,
        }, "TextFillColorSecondaryBrush", 24);

        var image = new Image { Width = 32, Height = 32, Visibility = Visibility.Collapsed };
        var icon = new Grid { Width = 32, Height = 32, HorizontalAlignment = HorizontalAlignment.Center };
        icon.Children.Add(glyph);
        icon.Children.Add(image);

        var name = Fluent.Text(FluentType.BodyStrong);
        name.Text = Label(assignment);
        name.TextAlignment = TextAlignment.Center;
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        name.HorizontalAlignment = HorizontalAlignment.Stretch;

        if (assignment.Source != LayoutSource.Url && LaunchTargets.Classify(assignment.Target) is { Kind: LaunchKind.LocalPath } local
            && LaunchTargets.MayProbe(local))
        {
            _ = LoadIconAsync(local.Value, image, glyph);
        }

        var stack = new StackPanel { Spacing = 4 };
        stack.Children.Add(icon);
        stack.Children.Add(name);
        return stack;
    }

    private static async Task LoadIconAsync(string path, Image image, FontIcon glyph)
    {
        try
        {
            if (await AppIcons.GetAsync(path, 64, null).ConfigureAwait(true) is { } source)
            {
                image.Source = source;
                image.Visibility = Visibility.Visible;
                glyph.Visibility = Visibility.Collapsed;
            }
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, "could not load a pane's icon", ex);
        }
    }

    /// <summary>What a pane's app is called: its name, else the target's own.</summary>
    internal static string Label(LayoutAssignment assignment)
        => assignment.Label.Length > 0 ? assignment.Label : LaunchTargets.DisplayName(null, assignment.Target);

    // ---- what a pane opens ([S-1413]) -------------------------------------------------------------

    private FrameworkElement TargetControl(LayoutNode pane, LayoutSource source, LayoutAssignment? assignment)
        => source == LayoutSource.App ? AppPicker(pane, assignment) : TargetBox(pane, source, assignment);

    /// <summary>A searchable list of the Start menu's apps (original: the searchable app drop-down).</summary>
    private FrameworkElement AppPicker(LayoutNode pane, LayoutAssignment? assignment)
    {
        var box = new AutoSuggestBox
        {
            PlaceholderText = Str.LayoutEditor_SearchApps.Text(),
            Text = assignment?.Source == LayoutSource.App ? Label(assignment) : string.Empty,
            QueryIcon = new SymbolIcon(Symbol.Find),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        void Suggest(string query)
        {
            var q = query.Trim();
            box.ItemsSource = _apps
                .Where(app => q.Length == 0 || app.Name.Contains(q, StringComparison.OrdinalIgnoreCase))
                .Take(30)
                .Select(app => app.Name)
                .ToList();
        }

        box.GotFocus += (_, _) => Suggest(box.Text == Label(assignment ?? new LayoutAssignment()) ? string.Empty : box.Text);
        box.TextChanged += (_, e) =>
        {
            if (e.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                Suggest(box.Text);
            }
        };

        void Choose(string? name)
        {
            if (name is null || _apps.FirstOrDefault(app => string.Equals(app.Name, name, StringComparison.OrdinalIgnoreCase)) is not { Name.Length: > 0 } app)
            {
                return;
            }

            var next = new LayoutAssignment { Source = LayoutSource.App, Target = app.Target, Label = app.Name, ReuseIfRunning = assignment?.ReuseIfRunning ?? true };
            Edit(root => LayoutTree.Assign(root, pane.Id, next) ? root : null);
        }

        box.SuggestionChosen += (_, e) => Choose(e.SelectedItem as string);
        box.QuerySubmitted += (_, e) => Choose(e.ChosenSuggestion as string ?? (box.ItemsSource as List<string>)?.FirstOrDefault());
        return box;
    }

    /// <summary>
    /// A web address, file or folder: typed (checked by the launch rule, the reason shown when it is
    /// refused) or, for a file or folder, picked with Browse. Applied on Enter or leaving the box; an
    /// empty box clears the pane.
    /// </summary>
    private FrameworkElement TargetBox(LayoutNode pane, LayoutSource source, LayoutAssignment? assignment)
    {
        var box = new TextBox
        {
            Text = assignment?.Source == source ? assignment.Target : string.Empty,
            PlaceholderText = source switch
            {
                LayoutSource.Url => "https://",
                LayoutSource.Folder => Str.WidgetsC_Shortcuts_LayoutEditor_FolderPlaceholder.Text(),
                _ => Str.WidgetsC_Shortcuts_LayoutEditor_FilePlaceholder.Text(),
            },
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        var verdict = FluentLayout.Verdict();
        verdict.HorizontalAlignment = HorizontalAlignment.Stretch;
        verdict.TextAlignment = TextAlignment.Center;

        void Commit()
        {
            var text = box.Text.Trim();

            if (text.Length == 0)
            {
                Edit(root => LayoutTree.Assign(root, pane.Id, null) ? root : null);
                return;
            }

            var target = LaunchTargets.Classify(text);
            var wanted = source == LayoutSource.Url ? LaunchKind.Web : LaunchKind.LocalPath;

            if (target.Kind != wanted)
            {
                FluentLayout.Refuse(verdict, target.IsValid
                    ? (source == LayoutSource.Url ? Str.WidgetsC_Shortcuts_LayoutEditor_NeedWeb.Text() : Str.WidgetsC_Shortcuts_LayoutEditor_NeedPath.Text())
                    : LaunchTargets.RefusalMessage(target.Refusal));
                return;
            }

            var next = new LayoutAssignment { Source = source, Target = target.Value, ReuseIfRunning = assignment?.ReuseIfRunning ?? true };
            Edit(root => LayoutTree.Assign(root, pane.Id, next) ? root : null);
        }

        box.KeyDown += (_, e) =>
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                Commit();
                e.Handled = true;
            }
        };
        box.LostFocus += (_, _) =>
        {
            if (box.Text.Trim() != (assignment?.Target ?? string.Empty))
            {
                Commit();
            }
        };

        var stack = new StackPanel { Spacing = 4 };

        if (source == LayoutSource.Url)
        {
            stack.Children.Add(box);
        }
        else
        {
            var browse = new Button { Content = Str.WidgetUi_Browse.Text(), VerticalAlignment = VerticalAlignment.Top };
            browse.Click += async (_, _) =>
            {
                if (await PickAsync(source == LayoutSource.Folder).ConfigureAwait(true) is { } picked)
                {
                    box.Text = picked;
                    Commit();
                }
            };

            var row = new Grid { ColumnSpacing = 6 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(browse, 1);
            row.Children.Add(box);
            row.Children.Add(browse);
            stack.Children.Add(row);
        }

        stack.Children.Add(verdict);
        return stack;
    }

    /// <summary>The shell's file or folder picker, owned by this window; null when cancelled.</summary>
    private async Task<string?> PickAsync(bool folder)
    {
        try
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);

            if (folder)
            {
                var picker = new Windows.Storage.Pickers.FolderPicker();
                picker.FileTypeFilter.Add("*");
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
                return (await picker.PickSingleFolderAsync())?.Path;
            }

            var files = new Windows.Storage.Pickers.FileOpenPicker();
            files.FileTypeFilter.Add("*");
            WinRT.Interop.InitializeWithWindow.Initialize(files, hwnd);
            return (await files.PickSingleFileAsync())?.Path;
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "the picker could not be shown", ex);
            return null;
        }
    }

    /// <summary>The Start menu's apps, read once on the thread pool (a slow profile disk must not freeze the window).</summary>
    private void LoadApps()
    {
        if (_appsLoading is not null)
        {
            return;
        }

        _appsLoading = LoadAppsAsync();
    }

    private async Task LoadAppsAsync()
    {
        try
        {
            _apps = await Task.Run(() => InstalledApps.Read(2000, CancellationToken.None)).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, "could not read the Start menu's apps", ex);
        }
    }
}
