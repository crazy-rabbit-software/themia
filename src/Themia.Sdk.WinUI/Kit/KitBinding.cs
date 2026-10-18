using Microsoft.UI.Xaml;
using Themia.App.Services;
using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Sdk.WinUI.Kit;

/// <summary>
/// [S-7061] The glue of a kit view: a source on one side, the list and its state panel on the
/// other. It reads the source on attach and whenever the source says it changed (never on a timer
/// of its own), decides the screen (<see cref="KitScreens.Decide"/>), plans the rows
/// (<see cref="KitLadder.Plan"/>) and draws them; redraws on a language change and on its tick (the
/// ages); publishes the header title the data suggests ([S-403]: the user's own still wins); and
/// carries out the actions the user picks: an open through the host's one launcher, a copy through
/// the host's clipboard, Refresh through the source, the rest through the view's handler.
/// Composition, not inheritance: the view owns one and forwards to it; Detach-safe twice.
/// </summary>
public sealed class KitBinding : IDisposable
{
    private const string Scope = "kit";

    private readonly IWidgetHost _host;
    private readonly KitSpec _spec;
    private readonly Action<KitItem, KitAction>? _handle;
    private readonly Action<string> _open;

    private Ticker? _ticker;
    private IDisposable? _tick;
    private TimeSpan _tickInterval;
    private IKitSource? _source;
    private IKitConfig? _config;
    private IWidgetSurface? _surface;
    private WidgetArea _area;
    private bool _attached;

    /// <param name="glyph">The widget's glyph, over its state sentences.</param>
    /// <param name="actions">An item's actions, as the widget's spec gives them.</param>
    /// <param name="handle">Carries out the actions only the widget knows (a check, a rename, a delete), by <see cref="KitAction.Id"/>.</param>
    /// <param name="open">How an item's address is opened (the widget's own until it opens through the host's launcher).</param>
    public KitBinding(IWidgetHost host, KitSpec spec, string glyph, Func<KitItem, IReadOnlyList<KitAction>> actions, Action<string> open, Action<KitItem, KitAction>? handle = null)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(glyph);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(open);

        _host = host;
        _spec = spec;
        _handle = handle;
        _open = open;
        List = new KitList(host, spec, actions, Run);
        State = new KitStatePanel(glyph, host) { Visibility = Visibility.Collapsed };
    }

    /// <summary>The list part: the view puts it in its slot.</summary>
    public KitList List { get; }

    /// <summary>The empty and problem state: the view puts it over the list.</summary>
    public KitStatePanel State { get; }

    /// <summary>What the source held at the last draw.</summary>
    public KitRead Last { get; private set; } = new([], []);

    /// <summary>The screen of the last draw.</summary>
    public KitScreen Screen { get; private set; } = new(KitScreenKind.Loading, string.Empty, false, []);

    /// <summary>The area of the last draw.</summary>
    public WidgetArea Area => _area;

    /// <summary>Raised after every draw, for the view's header.</summary>
    public event Action? Rendered;

    /// <summary>[S-1303] The card menu's Refresh, while the widget has something to read.</summary>
    public IReadOnlyList<WidgetAction> CardActions
        => _config is { Configured: true } && _source is not null
            ? [new WidgetAction(Glyphs.Refresh, Str.Common_Refresh.Text(), () => _source?.Show(refresh: true))]
            : [];

    /// <summary>Start: listen to the source and the language, draw what it holds, and tell it the widget is on screen.</summary>
    /// <param name="tick">How often the list is redrawn for its ages.</param>
    public void Attach(Ticker ticker, IKitSource source, IKitConfig config, TimeSpan tick)
    {
        ArgumentNullException.ThrowIfNull(ticker);

        _ticker = ticker;
        _attached = true;
        List.Attach();
        Strings.Changed -= OnLanguage;
        Strings.Changed += OnLanguage;
        Apply(source, config, tick, show: true);
    }

    /// <summary>
    /// A changed config: the source it reads now, its fields and its tick. <paramref name="show"/> asks
    /// the source (a changed address, a new cap); a change of fields only redraws.
    /// </summary>
    public void Apply(IKitSource source, IKitConfig config, TimeSpan tick, bool show)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(config);

        if (!_attached)
        {
            return;
        }

        if (!ReferenceEquals(_source, source))
        {
            if (_source is not null)
            {
                _source.Changed -= OnChanged;
            }

            _source = source;
            _source.Changed += OnChanged;
        }

        _config = config;

        if (_tick is null || tick != _tickInterval)
        {
            _tick?.Dispose();
            _tickInterval = tick;

            // The ticker draws once as it subscribes.
            _tick = _ticker?.Subscribe(tick, Render);
        }

        if (show)
        {
            source.Show(refresh: false);
        }

        Render();
    }

    /// <summary>[S-660] The content area changed.</summary>
    public void Size(WidgetArea area)
    {
        _area = area;
        Render();
    }

    /// <summary>The card's surface: where the header title goes.</summary>
    public void Surface(IWidgetSurface surface)
    {
        _surface = surface;
        PublishTitle();
    }

    /// <summary>Drop the keyboard cursor (the widget now reads something else).</summary>
    public void ReleaseCursor() => List.Release();

    /// <summary>[S-7020] A key from the lease.</summary>
    public bool HandleKey(WidgetKey key) => List.HandleKey(key);

    private void OnLanguage(Locale locale) => Render();

    private void OnChanged() => Render();

    /// <summary>Read the source and draw it.</summary>
    public void Render()
    {
        if (!_attached || _source is null || _config is null)
        {
            return;
        }

        var read = _source.Read();
        Last = read;

        if (read.Refused is { } refused)
        {
            Screen = KitScreen.Refused(refused);
        }
        else
        {
            var items = KitItems.Clean(read.Items, read.WebHosts);
            Screen = KitScreens.Decide(Strings.Current, read.Accounts, read.Chosen ?? [], read.Outcomes, items.Count, _spec.Empty());

            if (Screen.Kind == KitScreenKind.Items)
            {
                State.Hide();
                List.Visibility = Visibility.Visible;
                List.Show(items, KitLadder.Plan(_area, _spec, _config.Shows), _area, DateTimeOffset.Now);
                PublishTitle();
                Rendered?.Invoke();
                return;
            }
        }

        List.Clear();
        List.Visibility = Visibility.Collapsed;
        State.Show(Screen);
        PublishTitle();
        Rendered?.Invoke();
    }

    private void PublishTitle()
    {
        var title = Last.Title;
        _surface?.SetHeaderTitle(string.IsNullOrWhiteSpace(title) ? null : title);
    }

    /// <summary>Carry out an action the user picked (a menu line, a double-click, a key).</summary>
    private void Run(KitItem item, KitAction action)
    {
        switch (action.Kind)
        {
            case KitActionKind.Open or KitActionKind.OpenTo when action.Target is { } target:
                _open(target.OriginalString);
                break;

            case KitActionKind.CopyLink or KitActionKind.CopyText when action.Text is { Length: > 0 } text:
                if (!_host.CopyText(text))
                {
                    Log.Warn(Scope, "could not copy to the clipboard");
                }

                break;

            case KitActionKind.Refresh:
                _source?.Show(refresh: true);
                break;

            default:
                if (_handle is null)
                {
                    Log.Warn(Scope, $"no handler for a {action.Kind} action");
                    return;
                }

                _handle(item, action);
                break;
        }
    }

    public void Dispose()
    {
        if (!_attached)
        {
            return;
        }

        _attached = false;
        Strings.Changed -= OnLanguage;

        if (_source is not null)
        {
            _source.Changed -= OnChanged;
            _source = null;
        }

        _tick?.Dispose();
        _tick = null;
        _ticker = null;
        List.Detach();
        State.Detach();
    }
}
