using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Shell;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;
using Themia.Widget.Notifications;

namespace Themia.App.Widgets;

/// <summary>
/// [S-4400] Windows' notification list, as Windows 11's notification center draws it (Karpo §19, §100):
/// grouped by app under a heading with the app's icon, its name and a × that clears it; each app's newest
/// three and "See more (n)"; a notification's title, its body and when it came, the × in the time's place
/// under the pointer. A click opens the app; Clear all in the header clears Windows' own list.
/// <para>
/// [S-4404] The ladder: the bell and how many on the smallest tile; then the newest flat, each with its
/// app's icon; grouped from an expanded height; the body one, two, three lines as the tile grows. The list
/// is the host's one feed's (<see cref="IWidgetHost.Notifications"/>), which follows Windows' changes; access Windows has not given is
/// a sentence and the one button that fixes it ([S-4403]).
/// </para>
/// </summary>
internal sealed partial class NotificationsView : Grid, IWidgetView, IWidgetHeaderContent, IWidgetSizeAware, IWidgetSurfaceAware, IWidgetKeys
{
    private const string Scope = "notifications";

    /// <summary>The app icon's size in a heading and a flat row, in DIPs; asked at twice for 200 %.</summary>
    private const double AppIconSize = 16;

    /// <summary>Where Windows keeps notification access: Settings › Privacy › Notifications.</summary>
    private static readonly Uri AccessPage = new("ms-settings:privacy-notifications");

    private readonly Button _clearAll;
    private readonly TextBlock _clearAllText = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
    private readonly StackPanel _list = new() { Spacing = 0 };
    private readonly ScrollViewer _scroll;
    private ScrollFade? _fade;
    private readonly StackPanel _empty;
    private readonly TextBlock _emptyText = new();

    /// <summary>The sentence over the one button that fixes it; the empty-state panel itself holds an icon and a line only.</summary>
    private readonly StackPanel _emptyHost = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _action = new() { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, Fluent.Space3, 0, 0) };
    private readonly StackPanel _face = new() { Orientation = Orientation.Horizontal, Spacing = Fluent.Space2, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly FontIcon _faceBell;
    private readonly TextBlock _faceCount = Fluent.Text(FluentType.Subtitle);

    private readonly List<IDisposable> _rowHandles = [];

    /// <summary>The rows a key can land on, in order, with the notification each stands for.</summary>
    private readonly List<(Border Row, NotificationItem Item)> _rows = [];

    /// <summary>The apps opened past "See more" in this showing; forgotten when the widget goes, as Windows forgets it.</summary>
    private readonly HashSet<string> _open = new(StringComparer.OrdinalIgnoreCase);

    private IWidgetSurface? _surface;
    private IDisposable? _lease;
    private Ticker? _ticker;
    private IDisposable? _minutes;
    private NotificationPlan _plan;
    private WidgetArea _area;
    private NotificationSnapshot _preview = NotificationSnapshot.Pending;
    private int? _cursor;
    private bool _detached = true;

    /// <summary>[S-6913] What the view asks of the host: the launcher, ⚙ › Accounts.</summary>
    private readonly IWidgetHost _widgetHost;

    public NotificationsView(IWidgetHost host)
    {
        _widgetHost = host;
        _clearAll = new Button
        {
            // Windows' own words, not a ×: the × is a heading's and a row's.
            Content = _clearAllText,
            Padding = new Thickness(Fluent.Space2, Fluent.Space1, Fluent.Space2, Fluent.Space1),
            MinWidth = 0,
            Background = ThemeBrush.Of("SubtleFillColorTransparentBrush", Colors.Transparent),
            BorderThickness = new Thickness(0),
        };

        _scroll = new ScrollViewer
        {
            Content = _list,
            Padding = WidgetLayout.ListInset,
            HorizontalScrollMode = ScrollMode.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
        };
        _empty = Fluent.EmptyState(NotificationsModule.Glyph, _emptyText, out _);
        _emptyHost.Children.Add(_empty);
        _emptyHost.Children.Add(_action);

        _faceBell = Fluent.Icon(NotificationsModule.Glyph, "TextFillColorPrimaryBrush", 24);
        Microsoft.UI.Xaml.Documents.Typography.SetNumeralAlignment(_faceCount, FontNumeralAlignment.Tabular);
        _face.Children.Add(_faceBell);
        _face.Children.Add(_faceCount);

        Children.Add(_scroll);
        Children.Add(_emptyHost);
        Children.Add(_face);
    }

    /// <summary>[S-4402] Clear all, in the card's header, as on Windows' own list.</summary>
    public FrameworkElement? HeaderTrailing => _clearAll;

    public void Bind(IWidgetSurface surface) => _surface = surface;

    public void Attach(Ticker ticker, WidgetConfig config)
    {
        _detached = false;
        _ticker = ticker;

        // [S-1251] A debug render never reads the person's notifications.
        if (_surface is not { IsPreview: true })
        {
            _lease = _widgetHost.Notifications.Acquire(Render);
        }
#if DEBUG
        else if (_preview.Access == NotificationAccess.Pending)
        {
            _preview = new NotificationSnapshot(NotificationAccess.Allowed, Sample(5));
        }
#endif

        _fade ??= new ScrollFade(_scroll, _list);
        _clearAll.Click += OnClearAll;
        _action.Click += OnAction;
        Tapped += OnFaceTapped;
        PointerEntered += OnPointerEntered;
        Strings.Changed += OnLanguage;

        // "4m" goes stale on its own: once a minute is the slowest cadence that keeps it true.
        _minutes = ticker.Subscribe(TimeSpan.FromMinutes(1), Render);
        Render();
    }

    public void ApplyConfig(WidgetConfig config) => Render();

    public void ApplySize(WidgetArea area)
    {
        var sameHeight = area.HeightClass == _area.HeightClass;
        _area = area;
        var plan = NotificationsLayout.Plan(area);
        if (plan == _plan && sameHeight)
        {
            return;
        }

        _plan = plan;
        Render();
    }

    public void Detach()
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        _clearAll.Click -= OnClearAll;
        _action.Click -= OnAction;
        Tapped -= OnFaceTapped;
        PointerEntered -= OnPointerEntered;
        Strings.Changed -= OnLanguage;
        _minutes?.Dispose();
        _minutes = null;
        _lease?.Dispose();
        _lease = null;
        _fade?.Dispose();
        _fade = null;
        ClearRows();
        _open.Clear();
        _ticker = null;
    }

    private NotificationSnapshot Snapshot => _lease is null ? _preview : _widgetHost.Notifications.Snapshot;

    // ---- drawing -----------------------------------------------------------------------------

    private void Render()
    {
        if (_detached)
        {
            return;
        }

        ClearRows();
        var snapshot = Snapshot;
        var items = snapshot.Items;
        _clearAll.IsEnabled = items.Count > 0 && !Inert;
        _clearAllText.Text = NotificationsStr.Panels_Notifications_ClearAll.Text();
        _clearAll.Visibility = _plan.Face || snapshot.Access != NotificationAccess.Allowed ? Visibility.Collapsed : Visibility.Visible;
        AutomationProperties.SetName(_clearAll, NotificationsStr.Panels_Notifications_ClearAll.Text());

        _scroll.Visibility = Visibility.Collapsed;
        _emptyHost.Visibility = Visibility.Collapsed;
        _face.Visibility = Visibility.Collapsed;

        if (snapshot.Access == NotificationAccess.Pending)
        {
            // Not answered yet: the frame, never a wrong "nothing" (§11).
            return;
        }

        if (snapshot.Access != NotificationAccess.Allowed)
        {
            ShowAccess(snapshot.Access);
            if (_plan.Face)
            {
                // Too small for the sentence: the dimmed bell, the sentence its tooltip, a click the fix.
                _emptyHost.Visibility = Visibility.Collapsed;
                ShowFace(0);
                ToolTipService.SetToolTip(_face, _emptyText.Text);
                AutomationProperties.SetName(this, _emptyText.Text);
            }

            return;
        }

        if (_plan.Face)
        {
            ShowFace(items.Count);
            return;
        }

        if (items.Count == 0)
        {
            _emptyText.Text = NotificationsStr.Panels_Notifications_None.Text();
            _action.Visibility = Visibility.Collapsed;
            _emptyHost.Visibility = Visibility.Visible;
            _cursor = null;
            return;
        }

        _scroll.Visibility = Visibility.Visible;
        var now = DateTimeOffset.Now;

        if (_plan.Grouped)
        {
            foreach (var group in NotificationRules.Groups(items))
            {
                _list.Children.Add(Heading(group));
                var (rows, more) = NotificationRules.Fold(group, _open.Contains(group.AppId), _plan.Folded);
                foreach (var item in rows)
                {
                    AddRow(item, now, withIcon: false);
                }

                if (more > 0 || (_open.Contains(group.AppId) && group.Items.Count > _plan.Folded))
                {
                    _list.Children.Add(Fold(group, more));
                }
            }
        }
        else
        {
            foreach (var item in NotificationRules.Newest(items))
            {
                AddRow(item, now, withIcon: true);
            }
        }

        _cursor = _cursor is { } cursor && cursor < _rows.Count ? cursor : null;
        ShowCursor();
        _fade?.Refresh();
    }

    /// <summary>[S-4404] The smallest tile: the bell and how many, or the bell alone with none.</summary>
    private void ShowFace(int count)
    {
        _face.Visibility = Visibility.Visible;
        _faceCount.Text = count > 0 ? LocaleFormat.Integer(count) : string.Empty;
        _faceCount.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
        _faceBell.Foreground = ThemeBrush.Of(count > 0 ? "TextFillColorPrimaryBrush" : "TextFillColorTertiaryBrush", Colors.Gray);
        var words = count > 0 ? $"{NotificationsStr.Panels_Notifications_Label.Text()}: {LocaleFormat.Integer(count)}" : NotificationsStr.Panels_Notifications_None.Text();
        ToolTipService.SetToolTip(_face, words);
        AutomationProperties.SetName(this, words);
    }

    /// <summary>[S-4403] Access Windows has not given: what is wrong, and the one button that fixes it.</summary>
    private void ShowAccess(NotificationAccess access)
    {
        _emptyHost.Visibility = Visibility.Visible;
        (_emptyText.Text, var button) = access switch
        {
            NotificationAccess.Denied => (NotificationsStr.Panels_Notifications_Denied.Text(), NotificationsStr.Panels_Notifications_OpenSettings.Text()),
            NotificationAccess.Unspecified => (NotificationsStr.Panels_Notifications_Unspecified.Text(), NotificationsStr.Panels_Notifications_Allow.Text()),
            _ => (NotificationsStr.Panels_Notifications_Unsupported.Text(), (string?)null),
        };

        // The tile is too small for a sentence and a button: the sentence, and a click on it does the same.
        _action.Content = button;
        _action.Tag = access;
        _action.Visibility = button is not null && _area.HeightClass >= SizeClass.Regular ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>[S-4402] An app's heading: its icon, its name, and the × that clears it.</summary>
    private Grid Heading(NotificationGroup group)
    {
        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
            },
            Padding = new Thickness(Fluent.Space4, Fluent.Space2, Fluent.Space2, 0),
            ColumnSpacing = Fluent.Space2,
        };

        var icon = AppIcon(group.AppId);
        var name = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
        name.Text = group.AppName;
        name.TextTrimming = TextTrimming.CharacterEllipsis;
        name.MaxLines = 1;
        name.VerticalAlignment = VerticalAlignment.Center;

        var clear = FluentLayout.IconCommand(Glyphs.Dismiss, NotificationsStr.Panels_Notifications_ClearApp.Format(("app", group.AppName)));
        clear.Width = 24;
        clear.Height = 24;
        clear.Background = ThemeBrush.Of("SubtleFillColorTransparentBrush", Colors.Transparent);
        clear.BorderThickness = new Thickness(0);
        clear.IsEnabled = !Inert;
        var appId = group.AppId;
        RoutedEventHandler onClear = (_, _) => ClearApp(appId);
        clear.Click += onClear;
        _rowHandles.Add(new Unhook(() => clear.Click -= onClear));

        SetColumn(icon, 0);
        SetColumn(name, 1);
        SetColumn(clear, 2);
        grid.Children.Add(icon);
        grid.Children.Add(name);
        grid.Children.Add(clear);
        return grid;
    }

    /// <summary>[S-4402] "See more (n)" under a folded group, "See fewer" under an opened one.</summary>
    private HyperlinkButton Fold(NotificationGroup group, int more)
    {
        var link = new HyperlinkButton
        {
            Content = more > 0 ? NotificationsStr.Panels_Notifications_SeeMore.Format(("count", LocaleFormat.Integer(more))) : NotificationsStr.Panels_Notifications_SeeFewer.Text(),
            Margin = new Thickness(Fluent.Space2, 0, 0, Fluent.Space1),
            Padding = new Thickness(Fluent.Space2, Fluent.Space1, Fluent.Space2, Fluent.Space1),
        };

        var appId = group.AppId;
        RoutedEventHandler onClick = (_, _) =>
        {
            if (!_open.Remove(appId))
            {
                _open.Add(appId);
            }

            Render();
        };
        link.Click += onClick;
        _rowHandles.Add(new Unhook(() => link.Click -= onClick));
        return link;
    }

    /// <summary>One notification: (icon) title · time, then its body over the plan's lines.</summary>
    private void AddRow(NotificationItem item, DateTimeOffset now, bool withIcon)
    {
        var title = Fluent.Text(FluentType.BodyStrong);
        title.Text = item.Title.Length > 0 ? item.Title : NotificationRules.FallbackName(item.AppId);
        title.TextTrimming = TextTrimming.CharacterEllipsis;
        title.MaxLines = 1;

        var when = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
        when.Text = Formatting.Ago(item.Arrived, now);
        when.VerticalAlignment = VerticalAlignment.Center;
        Microsoft.UI.Xaml.Documents.Typography.SetNumeralAlignment(when, FontNumeralAlignment.Tabular);

        var dismiss = FluentLayout.IconCommand(Glyphs.Dismiss, NotificationsStr.Panels_Notifications_Dismiss.Text());
        dismiss.Width = 24;
        dismiss.Height = 24;
        dismiss.Background = ThemeBrush.Of("SubtleFillColorTransparentBrush", Colors.Transparent);
        dismiss.BorderThickness = new Thickness(0);
        dismiss.Visibility = Visibility.Collapsed;

        var top = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
            },
            ColumnSpacing = Fluent.Space2,
        };
        SetColumn(when, 1);
        SetColumn(dismiss, 1);
        top.Children.Add(title);
        top.Children.Add(when);
        top.Children.Add(dismiss);

        var words = new StackPanel { Spacing = 0 };
        words.Children.Add(top);
        if (_plan.BodyLines > 0 && item.Body.Length > 0)
        {
            var body = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
            body.Text = item.Body;
            body.TextWrapping = TextWrapping.Wrap;
            body.TextTrimming = TextTrimming.CharacterEllipsis;
            body.MaxLines = _plan.BodyLines;
            words.Children.Add(body);
        }

        FrameworkElement content = words;
        if (withIcon)
        {
            var line = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                },
                ColumnSpacing = Fluent.Space3,
            };
            var icon = AppIcon(item.AppId);
            icon.VerticalAlignment = VerticalAlignment.Top;
            icon.Margin = new Thickness(0, 2, 0, 0);
            SetColumn(words, 1);
            line.Children.Add(icon);
            line.Children.Add(words);
            content = line;
        }

        var row = Fluent.Row(content, compact: _plan.BodyLines == 0);
        row.Padding = new Thickness(Fluent.Space4, Fluent.Space1, Fluent.Space2, Fluent.Space1);
        RowFocus.Prepare(row);
        _rowHandles.Add(Fluent.Hover(row));
        _rowHandles.Add(ItemMenu.Attach(row, () => RowMenu(item)));

        // [S-4407] The × in the time's place under the pointer, as Windows' own list.
        PointerEventHandler entered = (_, _) =>
        {
            if (!Inert)
            {
                when.Visibility = Visibility.Collapsed;
                dismiss.Visibility = Visibility.Visible;
            }
        };
        PointerEventHandler left = (_, _) =>
        {
            when.Visibility = Visibility.Visible;
            dismiss.Visibility = Visibility.Collapsed;
        };
        RoutedEventHandler onDismiss = (_, _) => Dismiss(item);
        // [S-7065] A click focuses the row; a double-click opens the sender, as Enter does.
        TappedEventHandler onTap = (_, args) =>
        {
            args.Handled = true;
            Focus(row);
        };
        DoubleTappedEventHandler onDoubleTap = (_, args) =>
        {
            args.Handled = true;
            Open(item);
        };

        row.PointerEntered += entered;
        row.PointerExited += left;
        row.PointerCanceled += left;
        dismiss.Click += onDismiss;
        row.Tapped += onTap;
        row.DoubleTapped += onDoubleTap;
        _rowHandles.Add(new Unhook(() =>
        {
            row.PointerEntered -= entered;
            row.PointerExited -= left;
            row.PointerCanceled -= left;
            dismiss.Click -= onDismiss;
            row.Tapped -= onTap;
            row.DoubleTapped -= onDoubleTap;
        }));

        var app = item.AppName.Length > 0 ? item.AppName : NotificationRules.FallbackName(item.AppId);
        AutomationProperties.SetName(row, $"{app}: {title.Text}");
        ToolTipService.SetToolTip(row, NotificationsStr.Panels_Notifications_Open.Format(("app", app)));

        _rows.Add((row, item));
        _list.Children.Add(row);
    }

    /// <summary>[S-4405] The sender's icon through the shared app-icon service (packaged apps included); the bell until it comes.</summary>
    private FrameworkElement AppIcon(string appId)
    {
        var host = new Grid { Width = AppIconSize, Height = AppIconSize, VerticalAlignment = VerticalAlignment.Center };
        var fallback = Fluent.Icon(NotificationsModule.Glyph, "TextFillColorSecondaryBrush", 14);
        fallback.HorizontalAlignment = HorizontalAlignment.Center;
        host.Children.Add(fallback);

        if (_lease is not null && NotificationRules.AppTarget(appId) is { } target)
        {
            _ = LoadIconAsync(host, fallback, target);
        }

        return host;
    }

    private async Task LoadIconAsync(Grid host, FontIcon fallback, string target)
    {
        try
        {
            var image = await AppIconSource.GetAsync(target, (int)Math.Ceiling(AppIconSize * 2), AppIconSource.ThemeOf(this)).ConfigureAwait(true);
            if (image is null || _detached)
            {
                return;
            }

            host.Children.Remove(fallback);
            host.Children.Add(new Image { Source = image, Width = AppIconSize, Height = AppIconSize, Stretch = Stretch.Uniform });
        }
        catch (Exception ex)
        {
            // The bell stays: an icon is a nicety, never a reason for the row to go.
            Log.Debug(Scope, $"no icon for a sender ({ex.GetType().Name})");
        }
    }

    private void ClearRows()
    {
        foreach (var handle in _rowHandles)
        {
            handle.Dispose();
        }

        _rowHandles.Clear();
        _rows.Clear();
        _list.Children.Clear();
    }

    // ---- acting ------------------------------------------------------------------------------

    /// <summary>In edit mode the card owns the press: nothing is opened or cleared.</summary>
    private bool Inert => _detached || _surface?.EditMode == true;

    private IEnumerable<ItemMenuEntry> RowMenu(NotificationItem item)
    {
        if (Inert)
        {
            return [];
        }

        var app = item.AppName.Length > 0 ? item.AppName : NotificationRules.FallbackName(item.AppId);
        return
        [
            new(Glyphs.OpenWith, NotificationsStr.Panels_Notifications_Open.Format(("app", app)), () => Open(item)),
            new(Glyphs.Dismiss, NotificationsStr.Panels_Notifications_Dismiss.Text(), () => Dismiss(item), Quick: QuickCommand.Delete),
            ItemMenuEntry.Separator,
            new(Glyphs.Clear, NotificationsStr.Panels_Notifications_ClearApp.Format(("app", app)), () => ClearApp(item.AppId), Danger: true),
        ];
    }

    /// <summary>[S-4406] [S-7065] A double-click (or Enter) opens the sender, as Windows' list brings its app forward.</summary>
    private void Open(NotificationItem item)
    {
        if (Inert || _lease is null)
        {
            return;
        }

        if (_widgetHost.Launcher.OpenApp(item.AppId) != LaunchOutcome.Opened)
        {
            Log.Info(Scope, "the sender of a notification could not be opened");
        }
    }

    private void Dismiss(NotificationItem item)
    {
        if (Inert)
        {
            return;
        }

        if (_lease is null)
        {
            _preview = _preview with { Items = [.. _preview.Items.Where(each => each.Id != item.Id)] };
            Render();
            return;
        }

        _widgetHost.Notifications.Dismiss(item.Id);
    }

    private void ClearApp(string appId)
    {
        if (Inert)
        {
            return;
        }

        if (_lease is null)
        {
            _preview = _preview with { Items = [.. _preview.Items.Where(each => !string.Equals(each.AppId, appId, StringComparison.OrdinalIgnoreCase))] };
            Render();
            return;
        }

        _widgetHost.Notifications.DismissApp(appId);
    }

    private void OnClearAll(object sender, RoutedEventArgs args)
    {
        if (Inert)
        {
            return;
        }

        if (_lease is null)
        {
            _preview = _preview with { Items = [] };
            Render();
            return;
        }

        _widgetHost.Notifications.ClearAll();
    }

    /// <summary>[S-4403] The access state's button: Windows' prompt, or its privacy page.</summary>
    private void OnAction(object sender, RoutedEventArgs args)
    {
        if (Inert || _lease is null || _action.Tag is not NotificationAccess access)
        {
            return;
        }

        if (access == NotificationAccess.Unspecified)
        {
            _ = AskAsync();
            return;
        }

        _ = _widgetHost.Launcher.OpenWindowsSettings(AccessPage);
    }

    private async Task AskAsync()
    {
        try
        {
            var answer = await _widgetHost.Notifications.RequestAccessAsync().ConfigureAwait(true);
            if (answer == NotificationAccess.Denied && !_detached)
            {
                // Said no (or could not ask): the page where it can be turned on.
                _ = _widgetHost.Launcher.OpenWindowsSettings(AccessPage);
            }
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "notification access could not be asked for", ex);
        }
    }

    /// <summary>Where the tile has no room for the access button, a click on it does what the button would.</summary>
    private void OnFaceTapped(object sender, TappedRoutedEventArgs args)
    {
        var access = Snapshot.Access;
        if (_action.Visibility == Visibility.Visible || Inert || _lease is null
            || access is NotificationAccess.Allowed or NotificationAccess.Pending or NotificationAccess.Unsupported)
        {
            return;
        }

        args.Handled = true;
        if (access == NotificationAccess.Unspecified)
        {
            _ = AskAsync();
            return;
        }

        _ = _widgetHost.Launcher.OpenWindowsSettings(AccessPage);
    }

    /// <summary>Access, which Windows announces nowhere, is looked at again when the pointer comes by.</summary>
    private void OnPointerEntered(object sender, PointerRoutedEventArgs args)
    {
        if (_lease is not null)
        {
            _widgetHost.Notifications.Refresh();
        }
    }

    // ---- keys ([S-4409]) ---------------------------------------------------------------------

    /// <summary>
    /// [S-4409] Up/Down, Page Up/Down, Home/End move through the notifications (<see cref="ListKeys"/>);
    /// Enter opens the app, Delete dismisses, the Menu key opens its menu, Escape lets go.
    /// </summary>
    public bool HandleKey(WidgetKey key)
    {
        if (_detached)
        {
            return false;
        }

        var decision = ListKeys.Decide(key, _cursor ?? -1, _rows.Count);
        switch (decision.Command)
        {
            case ListCommand.Focus:
                _cursor = decision.Index;
                ShowCursor();
                return true;

            case ListCommand.Open:
                Open(_rows[decision.Index].Item);
                return true;

            case ListCommand.Delete:
                _cursor = ListKeys.AfterRemoval(decision.Index, _rows.Count - 1) is var next and >= 0 ? next : null;
                Dismiss(_rows[decision.Index].Item);
                return true;

            case ListCommand.Menu when decision.Index < _rows.Count:
                ItemMenu.Open(_rows[decision.Index].Row, RowMenu(_rows[decision.Index].Item));
                return true;

            case ListCommand.Release:
                var had = _cursor is not null;
                _cursor = null;
                ShowCursor();
                return had;

            default:
                return false;
        }
    }

    /// <summary>Only a list with rows takes the keyboard.</summary>
    public bool WantsKeys => !Inert && _rows.Count > 0;

    /// <summary>[S-7065] Put the cursor on the row a click landed on.</summary>
    private void Focus(Border row)
    {
        var index = _rows.FindIndex(r => ReferenceEquals(r.Row, row));
        if (index < 0 || Inert)
        {
            return;
        }

        _cursor = index;
        ShowCursor();
    }

    private void ShowCursor()
    {
        for (var i = 0; i < _rows.Count; i++)
        {
            RowFocus.Mark(_rows[i].Row, _cursor == i);
            if (_cursor == i)
            {
                _rows[i].Row.StartBringIntoView();
            }
        }
    }

    private void OnLanguage(Locale locale) => Render();

    /// <summary>One handler's removal, kept with the row it serves.</summary>
    private sealed class Unhook(Action undo) : IDisposable
    {
        private Action? _undo = undo;

        public void Dispose()
        {
            _undo?.Invoke();
            _undo = null;
        }
    }
}
