using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Themia.App.Services;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Settings;

namespace Themia.App.Shell.Config;

/// <summary>
/// The one settings window: app options, views, displays and every widget's own settings, reached
/// through a navigation pane — drawn as Windows 11's Settings app. [S-1540]
/// <para>
/// One window rather than a modal over the desktop surface. A widget's settings are not a transient
/// prompt — you change something, look at the result on the desktop behind, then change something
/// else — and a modal hosted by a full-screen click-through surface dims the very desktop the user
/// is trying to judge.
/// </para>
/// <para>
/// The shape is Settings': Mica behind everything, a tall title bar with the app's mark and name, a
/// back button and the "Find a setting" box in the middle; the navigation pane on the left with
/// its accent selection pill, folding away when the window is narrow; and each page under a large
/// breadcrumb title that stays put while the cards scroll. There is no status bar — Settings has
/// none, and nothing on it was a setting.
/// </para>
/// </summary>
internal sealed partial class ConfigWindow
{
    private const string Scope = "config";

    private readonly AppHost _host;
    private readonly Window _window;
    private readonly NavigationView _nav = new();
    private readonly ScrollViewer _bodyHost = new();
    private readonly Border _pageColumn = new();
    private readonly StackPanel _crumbs = new() { Orientation = Orientation.Horizontal };
    private readonly Grid _titleBar = new();
    private readonly NavHistory<ConfigTarget> _history = new();
    private readonly SettingsSearchIndex _index;

    /// <summary>[S-1833] The open groups of the Displays and Views pages, for as long as the window is open.</summary>
    private readonly SettingsExpansion _expansion = new();

    private nint _hwnd;
    private ConfigTarget _target = new ConfigTarget.App();
    private string _structure = string.Empty;
    private bool _closed;

    /// <summary>[S-1720] Whether this window has the focus: while it does, a state change is the user's own.</summary>
    private bool _active = true;

    /// <summary>[S-1720] What the page on screen shows (<see cref="SettingsPageSync.Signature"/>).</summary>
    private string? _shown;

    /// <summary>[S-1834] The groups the list page on screen was built with (<see cref="SettingsPageSync.Shape"/>).</summary>
    private string _shownShape = string.Empty;

    /// <summary>A catch-up is queued; the state changes before it runs fold into it.</summary>
    private bool _syncQueued;

    /// <summary>[S-6010] A widget page's rebuild for its new style is queued.</summary>
    private bool _restyleQueued;

    public ConfigWindow(AppHost host)
    {
        _host = host;
        _index = new SettingsSearchIndex(host);

        _window = new Window
        {
            Title = Str.Settings_Title.Text(),
            Content = BuildChrome(),
        };

        ConfigureWindow();
        UiLanguage.Apply(_window.Content as FrameworkElement);

        _structure = StructureSignature();
        RebuildNavigation();
        _history.Push(_target);
        RenderBody();

        _host.StateChanged += OnStateChanged;
        Themia.Core.Localization.Strings.Changed += OnLanguageChanged;
        _window.Activated += OnActivated;
    }

    /// <summary>Raised once the window has closed, so the host can drop its reference.</summary>
    public event Action? Closed;

    // -----------------------------------------------------------------------------------------
    // Reacting to the desktop
    // -----------------------------------------------------------------------------------------

    private void OnStateChanged()
    {
        if (_closed)
        {
            return;
        }

        ApplyTheme();

        // Only when a page came or went. Doing this on every state change meant that dragging a
        // slider re-indexed many times a second; the navigation itself lists pages, not views or
        // displays, so it never changes with the state ([S-1830]).
        var structure = StructureSignature();
        if (structure != _structure)
        {
            _structure = structure;

            // A page that is gone leaves the back history, and search forgets what it had of it.
            _history.RemoveWhere(t => !Exists(t));
            _index.Invalidate();
            UpdateBackButton();
        }

        // The body is not re-rendered for a change the user made with it: it is the thing being
        // edited. It is replaced when whatever it was editing no longer exists, and caught up when
        // what it shows was changed from somewhere else ([S-1720]).
        if (!Exists(_target))
        {
            _target = _history.Current ?? new ConfigTarget.App();
            SelectForTarget();
            RenderBody();
            return;
        }

        // [S-1834] A list page that gained, lost or reordered a group is built again at once, even
        // under the user's own hand: the change came from the page itself, and it has to show it.
        if (Shape(_target) != _shownShape)
        {
            if (_target is ConfigTarget.Widget)
            {
                QueueRestyle();
            }
            else
            {
                RenderBody(keepScroll: true);
            }

            return;
        }

        QueueSync();
    }

    /// <summary>
    /// [S-1720] Losing the focus closes the run of the user's own edits: whatever the page's controls
    /// wrote is what they show, so that becomes the page's signature. A change after this came from
    /// somewhere else and is caught up.
    /// </summary>
    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        _active = args.WindowActivationState != WindowActivationState.Deactivated;
        if (!_active && !_closed)
        {
            _shown = Signature(_target);
        }
    }

    /// <summary>
    /// [S-1720] Build the page again if the state it shows was changed from outside this window.
    /// Queued, so a burst of changes is one rebuild and the sender's own handler has finished first.
    /// </summary>
    private void QueueSync()
    {
        if (_active || _syncQueued)
        {
            return;
        }

        _syncQueued = _window.DispatcherQueue.TryEnqueue(() =>
        {
            _syncQueued = false;
            if (_closed)
            {
                return;
            }

            var current = Signature(_target);
            if (SettingsPageSync.NeedsRebuild(_active, _shown, current))
            {
                RenderBody(keepScroll: true);
            }
        });

        if (!_syncQueued)
        {
            Log.Warn(Scope, "could not queue the settings page catch-up");
        }
    }

    /// <summary>
    /// [S-6010] [S-6071] A widget page whose style was picked, or whose switch other rows depend on
    /// was turned, shows the rows that now apply: built again once the pick has finished, since
    /// rebuilding inside it would tear down the control raising it. The groups that were open stay
    /// open and the focus goes back to the row that had it, so a keyboard user stays on the switch
    /// they just turned.
    /// </summary>
    private void QueueRestyle()
    {
        if (_restyleQueued)
        {
            return;
        }

        _restyleQueued = _window.DispatcherQueue.TryEnqueue(() =>
        {
            _restyleQueued = false;
            if (!_closed && Shape(_target) != _shownShape)
            {
                var focus = FocusedRow();
                var open = OpenGroups(_pageColumn.Child as FrameworkElement);
                var page = RenderBody(keepScroll: true);
                Reopen(page, open);
                if (focus is { } had)
                {
                    Refocus(page, had.Title, had.State);
                }
            }
        });

        if (!_restyleQueued)
        {
            Log.Warn(Scope, "could not queue the widget page for its new style; reopen it to see the style's rows");
        }
    }

    /// <summary>The title of the page row holding the focus, and how it got it; null when no row has it.</summary>
    private (string Title, FocusState State)? FocusedRow()
    {
        if (_pageColumn.XamlRoot is not { } root || _pageColumn.Child is not FrameworkElement page
            || FocusManager.GetFocusedElement(root) is not DependencyObject focused)
        {
            return null;
        }

        var state = focused is Control control ? control.FocusState : FocusState.Programmatic;
        foreach (var (row, _) in SettingsIndex.Collect(page))
        {
            for (var at = focused; at is not null; at = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetParent(at))
            {
                if (ReferenceEquals(at, row.Control))
                {
                    return (row.Title, state);
                }
            }
        }

        return null;
    }

    /// <summary>The titles of the page's expanders that are open.</summary>
    private static HashSet<string> OpenGroups(FrameworkElement? page)
    {
        var open = new HashSet<string>(StringComparer.Ordinal);
        if (page is null)
        {
            return open;
        }

        foreach (var (row, _) in SettingsIndex.Collect(page))
        {
            if (row.Parent is { IsExpanded: true, Header: string title })
            {
                open.Add(title);
            }
        }

        return open;
    }

    /// <summary>Open again the rebuilt page's expanders that were open before.</summary>
    private static void Reopen(FrameworkElement page, HashSet<string> open)
    {
        if (open.Count == 0)
        {
            return;
        }

        foreach (var (row, _) in SettingsIndex.Collect(page))
        {
            if (row.Parent is { Header: string title } expander && open.Contains(title))
            {
                expander.IsExpanded = true;
            }
        }
    }

    /// <summary>Put the focus back on the row titled <paramref name="title"/> once the rebuilt page is laid out.</summary>
    private static void Refocus(FrameworkElement page, string title, FocusState state)
    {
        void Focus()
        {
            foreach (var (row, _) in SettingsIndex.Collect(page))
            {
                if (row.Title == title && row.Control is { } control)
                {
                    var target = control as Control ?? FocusManager.FindFirstFocusableElement(control) as Control;
                    target?.Focus(state == FocusState.Unfocused ? FocusState.Programmatic : state);
                    return;
                }
            }
        }

        if (page.IsLoaded)
        {
            Focus();
            return;
        }

        void OnLoaded(object sender, RoutedEventArgs args)
        {
            page.Loaded -= OnLoaded;
            Focus();
        }

        page.Loaded += OnLoaded;
    }

    /// <summary>[S-1721] The signature of what a target's page shows.</summary>
    private string Signature(ConfigTarget target)
        => SettingsPageSync.Signature(_host.State, ScopeOf(target), (target as ConfigTarget.Widget)?.WidgetId, DisplayIds());

    /// <summary>[S-1834] The groups of a target's page; empty for a page that is not a list.</summary>
    private string Shape(ConfigTarget target) => SettingsPageSync.Shape(_host.State, ScopeOf(target), DisplayIds(), (target as ConfigTarget.Widget)?.WidgetId);

    private static SettingsScope ScopeOf(ConfigTarget target) => target switch
    {
        ConfigTarget.App { Page: AppPage.Views } => SettingsScope.Views,
        ConfigTarget.App { Page: AppPage.Displays } => SettingsScope.Displays,
        ConfigTarget.Widget => SettingsScope.Widget,
        _ => SettingsScope.App,
    };

    private string[] DisplayIds() => [.. _host.Displays.Select(d => d.Id)];

    /// <summary>
    /// The pages that can come and go and what search shows of them (views, widgets, displays by
    /// name): a change to any of it drops pages from the history and the search index.
    /// </summary>
    private string StructureSignature()
    {
        var views = string.Join(
            "|",
            _host.State.Views.Select(v =>
                $"{v.Id}~{v.Name}~{v.Icon}~{string.Join(",", v.Widgets.Select(w => $"{w.Id}={w.Kind}={w.Label}"))}"));

        var displays = string.Join(",", _host.Displays.Select(d => $"{d.Describe()}~{_host.State.DisabledMonitors.Contains(d.Id)}"));
        return $"{views}||{displays}";
    }

    private bool Exists(ConfigTarget target) => target switch
    {
        ConfigTarget.Widget widget => _host.Find(widget.WidgetId) is not null,
        ConfigTarget.App app => ConfigPages.AppPages.Any(p => p.Page == app.Page),
        _ => true,
    };

    // -----------------------------------------------------------------------------------------
    // Body
    // -----------------------------------------------------------------------------------------

    /// <summary>
    /// Build the page for the current target, put its title in the breadcrumb, and hand its rows to
    /// search — the page on screen is always what search knows of it.
    /// </summary>
    /// <param name="keepScroll">
    /// [S-1720] Stay where the reader was: the same page built again to catch up with a change, not
    /// a new page, which starts at the top.
    /// </param>
    private FrameworkElement RenderBody(bool keepScroll = false)
    {
        var offset = _bodyHost.VerticalOffset;
        FrameworkElement page;

        try
        {
            page = ConfigPages.Build(new PageContext(_host, Navigate, _expansion), _target);
        }
        catch (Exception ex)
        {
            // A page that cannot be built must not take the settings window down with it.
            Log.Error(Scope, $"could not build the page for {_target}", ex);
            page = new SettingsPage(NavTitle(_target)).With([FluentLayout.Note(Str.Shell_Settings_PageFailed.Text())]);
        }

        _pageColumn.Child = page;
        _shown = Signature(_target);
        _shownShape = Shape(_target);
        RenderCrumbs(page is SettingsPage settings ? settings.Title : NavTitle(_target));

        if (keepScroll)
        {
            // Measured first, or the offset is clamped against a page that has no height yet.
            _bodyHost.UpdateLayout();
        }

        _bodyHost.ChangeView(null, keepScroll ? offset : 0, null, disableAnimation: true);

        // [S-1831] A link to one display or view lands on its group, opened. Not when the page is
        // only being caught up: the reader stays where they were.
        if (!keepScroll && _target is ConfigTarget.App { Item: { } item })
        {
            Reveal(page, item);
        }

        _index.Update(_target, page);
        return page;
    }

    /// <summary>Bring the group of <paramref name="item"/> to the top of the page once it is laid out.</summary>
    private static void Reveal(FrameworkElement page, string item)
    {
        Control? group = null;
        foreach (var (row, _) in SettingsIndex.Collect(page))
        {
            if (row.Item == item && row.Parent is null && row.Card is { } card)
            {
                group = card;
                break;
            }
        }

        if (group is null)
        {
            return; // ignore: the display or view is gone; the page opens at the top
        }

        void Bring() => group.StartBringIntoView(new BringIntoViewOptions { VerticalAlignmentRatio = 0, AnimationDesired = false });

        if (page.IsLoaded)
        {
            Bring();
            return;
        }

        void OnLoaded(object sender, RoutedEventArgs args)
        {
            page.Loaded -= OnLoaded;
            Bring();
        }

        page.Loaded += OnLoaded;
    }
}
