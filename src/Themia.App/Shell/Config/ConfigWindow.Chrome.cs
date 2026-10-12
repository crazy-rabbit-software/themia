using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Themia.App.Interop;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Windows.Graphics;

namespace Themia.App.Shell.Config;

/// <summary>
/// The window chrome, as Windows 11 Settings draws it: the tall title bar (back, the app's mark and
/// name, "Find a setting" in the middle), the navigation pane, the breadcrumb title over the page,
/// window setup, theme and the navigation entry points. [S-1540], [S-1541], [S-1546]
/// </summary>
internal sealed partial class ConfigWindow
{
    /// <summary>Settings' tall title bar: 48 DIPs, room for the search box.</summary>
    private const double TitleBarHeight = 48;

    /// <summary>Settings' page margins: 36 either side of the cards, the cards at most 1000 wide.</summary>
    private const double PageSide = 36;

    /// <summary>
    /// Below this the pane folds away behind the button in the title bar, as Settings does. Settings
    /// has no icon-only rail, so the compact threshold is the same width: never compact.
    /// </summary>
    private const double ExpandedPaneWidth = 1008;

    private readonly Button _back = new();
    private readonly Button _paneToggle = new();
    private readonly AutoSuggestBox _search = new();

    /// <summary>
    /// Title bar, then the navigation with the page under its breadcrumb.
    /// <para>
    /// A plain <see cref="Grid"/> handed to <c>SetTitleBar</c>, not the SDK's <c>TitleBar</c>
    /// control: an earlier build found that control crashes the process at startup and at exit, and this is
    /// the shape that works. Its buttons and the search box are carved out of the drag region with
    /// <see cref="InputNonClientPointerSource"/> so they take clicks.
    /// </para>
    /// </summary>
    private FrameworkElement BuildChrome()
    {
        BuildTitleBar();

        _nav.PaneDisplayMode = NavigationViewPaneDisplayMode.Auto;
        _nav.ExpandedModeThresholdWidth = ExpandedPaneWidth;
        _nav.CompactModeThresholdWidth = ExpandedPaneWidth;
        _nav.IsSettingsVisible = false;
        _nav.IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed;
        _nav.IsPaneToggleButtonVisible = false;
        _nav.IsTitleBarAutoPaddingEnabled = false;
        _nav.OpenPaneLength = 300;
        _nav.ItemInvoked += OnItemInvoked;
        _nav.DisplayModeChanged += (_, args) => OnPaneModeChanged(args.DisplayMode);

        // One surface, as Settings is: the page sits on the same Mica as the pane, not on the
        // raised content layer NavigationView draws by default (that is Task Manager's look).
        _nav.Resources["NavigationViewContentBackground"] = new SolidColorBrush(Colors.Transparent);
        _nav.Resources["NavigationViewContentGridBorderBrush"] = new SolidColorBrush(Colors.Transparent);
        _nav.Resources["NavigationViewExpandedPaneBackground"] = new SolidColorBrush(Colors.Transparent);

        // The page title: Settings' breadcrumb, still while the cards scroll under it.
        _crumbs.Margin = new Thickness(PageSide, 0, PageSide, 20);
        _crumbs.MinHeight = 40;

        _pageColumn.MaxWidth = Application.Current.Resources.TryGetValue("SettingsPageMaxWidth", out var max) && max is double width ? width : 1000;
        _pageColumn.HorizontalAlignment = HorizontalAlignment.Stretch;

        // Left-aligned up to the maximum width, the way Settings leaves the slack on the right.
        var column = new Grid
        {
            Padding = new Thickness(PageSide, 0, PageSide, PageSide),
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MaxWidth = _pageColumn.MaxWidth + (2 * PageSide) },
                new ColumnDefinition { Width = GridLength.Auto },
            },
            Children = { _pageColumn },
        };

        _bodyHost.HorizontalScrollMode = ScrollMode.Disabled;
        _bodyHost.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        _bodyHost.Content = column;

        Grid.SetRow(_bodyHost, 1);

        _nav.Content = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
            },
            Children = { _crumbs, _bodyHost },
        };

        Grid.SetRow(_titleBar, 0);
        Grid.SetRow(_nav, 1);

        var root = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = new GridLength(TitleBarHeight) },
                new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
            },
            Children = { _titleBar, _nav },
        };

        AttachHistoryKeys(root);
        AttachWindowKeys(root);
        return root;
    }

    private void BuildTitleBar()
    {
        _back.Content = UiLanguage.Mirrored(new FontIcon { Glyph = Glyphs.Back, FontSize = 12 });
        _back.Click += (_, _) => GoBack();

        _paneToggle.Content = new FontIcon { Glyph = "", FontSize = 14 };
        _paneToggle.Visibility = Visibility.Collapsed;
        ApplyChromeText();
        _paneToggle.Click += (_, _) => _nav.IsPaneOpen = !_nav.IsPaneOpen;

        foreach (var button in new[] { _back, _paneToggle })
        {
            // Settings' title-bar buttons: borderless, a subtle fill only under the pointer.
            button.Width = 40;
            button.Height = 36;
            button.Padding = new Thickness(0);
            button.VerticalAlignment = VerticalAlignment.Center;
            button.Background = new SolidColorBrush(Colors.Transparent);
            button.BorderThickness = new Thickness(0);

            // Disabled (nothing to go back to) is just a dimmed arrow, not a grey plate.
            button.Resources["ButtonBackgroundDisabled"] = new SolidColorBrush(Colors.Transparent);
            button.Resources["ButtonBorderBrushDisabled"] = new SolidColorBrush(Colors.Transparent);
        }

        var icon = new Image
        {
            Width = 16,
            Height = 16,
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Source = new BitmapImage
            {
                DecodePixelType = DecodePixelType.Logical,
                DecodePixelWidth = 16,
                UriSource = new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", "themia-logo.png")),
            },
        };

        var caption = new TextBlock
        {
            Text = "Themia",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(16, 0, 0, 0),
        };

        FluentLayout.ApplyStyle(caption, "CaptionTextBlockStyle");

        var identity = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(4, 0, 0, 0),
            Children = { _back, _paneToggle, icon, caption },
        };

        BuildSearch();

        Grid.SetColumn(identity, 0);
        Grid.SetColumn(_search, 1);

        _titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star), MaxWidth = 520 });
        _titleBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _titleBar.Children.Add(identity);
        _titleBar.Children.Add(_search);

        // The interactive parts are carved out of the drag region whenever they move.
        _titleBar.SizeChanged += (_, _) => UpdateDragRegions();
        _back.SizeChanged += (_, _) => UpdateDragRegions();
        _search.SizeChanged += (_, _) => UpdateDragRegions();

        UpdateBackButton();
    }

    /// <summary>
    /// [S-1113] Windows draws the caption buttons at the right edge whatever the language, so a
    /// title bar flowing right to left keeps their width clear at its start (the right) for the back
    /// button and the name. Left to right, the empty third column already holds them.
    /// </summary>
    private void ApplyCaptionInset()
    {
        if (_hwnd == 0)
        {
            return;
        }

        var appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(_hwnd));
        var dpi = Win32.GetDpiForWindow(_hwnd);
        var scale = dpi > 0 ? dpi / 96.0 : 1.0;
        var padding = UiLanguage.Flow == FlowDirection.RightToLeft
            ? new Thickness(appWindow.TitleBar.RightInset / scale, 0, 0, 0)
            : new Thickness(0);

        if (_titleBar.Padding != padding)
        {
            _titleBar.Padding = padding;
            _titleBar.UpdateLayout();
            UpdateDragRegions();
        }
    }

    /// <summary>
    /// The title bar's buttons and search box take clicks; the rest of it drags the window.
    /// Rects are physical pixels, relative to the window.
    /// </summary>
    private void UpdateDragRegions()
    {
        if (_hwnd == 0 || _titleBar.XamlRoot is not { } root)
        {
            return;
        }

        var scale = root.RasterizationScale;
        var rects = new List<RectInt32>();

        foreach (var element in new FrameworkElement[] { _back, _paneToggle, _search })
        {
            if (element.Visibility != Visibility.Visible || element.ActualWidth <= 0)
            {
                continue;
            }

            var bounds = element.TransformToVisual(null).TransformBounds(
                new Windows.Foundation.Rect(0, 0, element.ActualWidth, element.ActualHeight));

            rects.Add(new RectInt32(
                (int)Math.Round(bounds.X * scale),
                (int)Math.Round(bounds.Y * scale),
                (int)Math.Round(bounds.Width * scale),
                (int)Math.Round(bounds.Height * scale)));
        }

        try
        {
            var source = InputNonClientPointerSource.GetForWindowId(Win32Interop.GetWindowIdFromWindow(_hwnd));
            source.SetRegionRects(NonClientRegionKind.Passthrough, [.. rects]);
        }
        catch (Exception ex)
        {
            // The buttons stop taking clicks, the window still drags and closes. Worth a line.
            Log.Warn(Scope, "could not set the title bar's passthrough regions", ex);
        }
    }

    /// <summary>
    /// Narrow: the pane folds away and its button appears in the title bar beside back, as in
    /// Settings. Wide: no button at all — Settings shows none while the pane is open.
    /// </summary>
    private void OnPaneModeChanged(NavigationViewDisplayMode mode)
    {
        _paneToggle.Visibility = mode == NavigationViewDisplayMode.Expanded ? Visibility.Collapsed : Visibility.Visible;
        UpdateDragRegions();
    }

    // -----------------------------------------------------------------------------------------
    // The breadcrumb title
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// [S-1541] "Desktop › Clock": the parents as quiet, clickable crumbs, the page itself in the
    /// title's full weight. Built from text blocks rather than the SDK's BreadcrumbBar so no
    /// collection crosses into WinRT, which Native AOT would need marshalling code for.
    /// </summary>
    private void RenderCrumbs(string title)
    {
        _crumbs.Children.Clear();

        foreach (var (name, target) in Parents(_target))
        {
            var text = new TextBlock { Text = name };
            FluentLayout.ApplyStyle(text, "SettingsCrumbTextBlockStyle");

            var crumb = new HyperlinkButton
            {
                Content = text,
                Padding = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Bottom,
            };

            crumb.Click += (_, _) => Navigate(target);
            _crumbs.Children.Add(crumb);

            var chevron = UiLanguage.Mirrored(new FontIcon
            {
                Glyph = Glyphs.ChevronRight,
                FontSize = 14,
                Margin = new Thickness(11, 0, 11, 6),
                VerticalAlignment = VerticalAlignment.Bottom,
            });

            _crumbs.Children.Add(chevron);
        }

        var current = new TextBlock { Text = title, VerticalAlignment = VerticalAlignment.Bottom };
        FluentLayout.ApplyStyle(current, "SettingsPageTitleTextBlockStyle");
        AutomationProperties.SetHeadingLevel(current, Microsoft.UI.Xaml.Automation.Peers.AutomationHeadingLevel.Level1);
        _crumbs.Children.Add(current);
    }

    /// <summary>
    /// [S-1831] The pages above this one: a widget sits under Views and its view, "Views › Desktop ›
    /// Clock". Views opens the page; the view's crumb opens it with that view's group open.
    /// </summary>
    private IEnumerable<(string Name, ConfigTarget Target)> Parents(ConfigTarget target)
    {
        if (target is ConfigTarget.Widget widget && _host.Find(widget.WidgetId) is var (view, _) && view is not null)
        {
            yield return (Str.App_Views.Text(), new ConfigTarget.App(AppPage.Views));
            yield return (view.Name, ConfigTarget.ForView(view.Id));
        }
    }

    /// <summary>A page's name as the navigation shows it, for a page that did not name itself.</summary>
    private string NavTitle(ConfigTarget target)
    {
        var entry = target.NavEntry();
        foreach (var item in Entries())
        {
            if (item.Tag is ConfigTarget tag && tag == entry && item.Content is string text)
            {
                return text;
            }
        }

        return Str.Common_Settings.Text();
    }

    // -----------------------------------------------------------------------------------------
    // Window
    // -----------------------------------------------------------------------------------------

    private void ConfigureWindow()
    {
        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);
        var appWindow = AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(_hwnd));

        // AppWindow sizes are physical pixels, so a fixed size would be a postage stamp on a 4K
        // display at 150%.
        var dpi = Win32.GetDpiForWindow(_hwnd);
        var scale = dpi > 0 ? dpi / 96.0 : 1.0;
        appWindow.Resize(new SizeInt32((int)Math.Round(1100 * scale), (int)Math.Round(780 * scale)));

        AppIcon.Apply(appWindow);

        _window.SystemBackdrop = new MicaBackdrop();
        _window.ExtendsContentIntoTitleBar = true;
        _window.SetTitleBar(_titleBar);

        // Settings' title bar is the tall one; the caption buttons stretch to its height.
        appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        _titleBar.SizeChanged += (_, _) => ApplyCaptionInset();

        ApplyTheme();

        _window.Closed += (_, _) =>
        {
            _closed = true;
            _host.StateChanged -= OnStateChanged;
            Themia.Core.Localization.Strings.Changed -= OnLanguageChanged;
            Closed?.Invoke();
        };
    }

    private void ApplyTheme()
    {
        if (_window.Content is not FrameworkElement root)
        {
            return;
        }

        // [S-822] The engine's effective theme, resolved even for "use system setting": the brushes
        // code hands out are that theme's, so the window's own controls have to be the same one.
        root.RequestedTheme = DesignApplier.Theme;

        // The caption buttons follow the same choice, or they draw dark glyphs on a dark page.
        try
        {
            _window.AppWindow.TitleBar.PreferredTheme = DesignApplier.IsDark ? TitleBarTheme.Dark : TitleBarTheme.Light;
        }
        catch (Exception ex)
        {
            Log.Warn(Scope, "could not theme the caption buttons", ex);
        }
    }

    public void Activate() => _window.Activate();

    public void Close() => _window.Close();

    /// <summary>
    /// The centre of this window in physical screen pixels, or null when it is closed or hidden. A
    /// minimised window reports a position off every display, which then matches none. [S-1702]
    /// </summary>
    public (int X, int Y)? Centre()
    {
        if (_closed || _window.AppWindow is not { IsVisible: true } appWindow)
        {
            return null;
        }

        var position = appWindow.Position;
        var size = appWindow.Size;
        return (position.X + (size.Width / 2), position.Y + (size.Height / 2));
    }

    /// <summary>Show a particular page, bringing the window forward. A step in the back history.</summary>
    public void Navigate(ConfigTarget target)
    {
        _window.Activate();
        Win32.SetForegroundWindow(_hwnd);
        Show(target, record: true);
    }

    /// <summary>
    /// [S-1745] Show <paramref name="target"/> without taking the foreground: a click on a widget in
    /// edit mode follows it here, and the desktop must keep the keyboard for the next shortcut. The
    /// page already shown is left as it is (its scroll position, a half-typed value).
    /// </summary>
    public void Follow(ConfigTarget target)
    {
        if (target != _target)
        {
            Show(target, record: true);
        }
    }

    /// <summary>Make a target the current page. Only the user's own steps are recorded for back.</summary>
    private FrameworkElement Show(ConfigTarget target, bool record)
    {
        _target = target;

        // [S-1831] A link to one display or view opens its group; the reader's other groups stay as they were.
        if (target is ConfigTarget.App { Item: { } item } app && ConfigTarget.ListOf(app.Page) is { } list)
        {
            _expansion.Set(list, item, true);
        }

        if (record)
        {
            _history.Push(target);
        }

        UpdateBackButton();
        SelectForTarget();
        return RenderBody();
    }

    // -----------------------------------------------------------------------------------------
    // Back and forward ([S-1547])
    // -----------------------------------------------------------------------------------------

    private void GoBack()
    {
        if (_history.Back() is { } target)
        {
            Show(target, record: false);
        }
    }

    private void GoForward()
    {
        if (_history.Forward() is { } target)
        {
            Show(target, record: false);
        }
    }

    private void UpdateBackButton() => _back.IsEnabled = _history.CanBack;

    /// <summary>Alt+Left / Alt+Right and the mouse's own back and forward buttons, as in Settings.</summary>
    private void AttachHistoryKeys(UIElement root)
    {
        var back = new KeyboardAccelerator { Key = Windows.System.VirtualKey.Left, Modifiers = Windows.System.VirtualKeyModifiers.Menu };
        back.Invoked += (_, args) =>
        {
            GoBack();
            args.Handled = true;
        };

        var forward = new KeyboardAccelerator { Key = Windows.System.VirtualKey.Right, Modifiers = Windows.System.VirtualKeyModifiers.Menu };
        forward.Invoked += (_, args) =>
        {
            GoForward();
            args.Handled = true;
        };

        root.KeyboardAccelerators.Add(back);
        root.KeyboardAccelerators.Add(forward);

        root.AddHandler(
            UIElement.PointerPressedEvent,
            new PointerEventHandler((sender, args) =>
            {
                var properties = args.GetCurrentPoint(sender as UIElement).Properties;
                if (properties.IsXButton1Pressed)
                {
                    GoBack();
                    args.Handled = true;
                }
                else if (properties.IsXButton2Pressed)
                {
                    GoForward();
                    args.Handled = true;
                }
            }),
            handledEventsToo: true);
    }
}
