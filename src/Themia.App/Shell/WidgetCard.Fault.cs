using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Themia.App.Services;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;

namespace Themia.App.Shell;

/// <summary>
/// [S-891]/[S-892] The card's error boundary. A view that throws while it is built, attached,
/// handed new settings or ticked is taken out and an error card put in its place — the widget's
/// header stays, the body says it stopped, and Retry builds the view again. One broken widget can
/// no longer leave a hole in the surface, a half-drawn card, or a silent failure every second.
/// <para>
/// [S-890] The same card, worded as "paused", is what safe mode shows instead of starting a view at
/// all: after starts that kept dying, nothing a widget does runs until the user asks.
/// </para>
/// </summary>
internal sealed partial class WidgetCard
{
    /// <summary>[S-890] Safe mode: new cards show the paused card instead of starting their view.</summary>
    internal static bool StartPaused { get; set; }

    /// <summary>Why the view is not running: the failure, or null while it runs (or is only paused).</summary>
    private Exception? _fault;

    /// <summary>The view is not running and its card is showing (failed or paused).</summary>
    public bool IsFaulted => _view is FaultCard;

    /// <summary>Paused by safe mode rather than failed.</summary>
    public bool IsPaused => _view is FaultCard { Paused: true };

    /// <summary>
    /// [S-1881] The view is attached (subscribed to the ticker, its services and its events) and not
    /// yet detached. Detach runs only while this is set, so it runs once however many paths ask.
    /// </summary>
    private bool _running;

    /// <summary>Set while the view attaches, so a failure on its very first tick counts as an attach failure.</summary>
    private Exception? _attachFault;
    private bool _attaching;

    private FrameworkElement CreateView(WidgetKind kind)
    {
        if (StartPaused)
        {
            return new FaultCard(this, paused: true);
        }

        try
        {
            return WidgetCatalog.CreateView(kind);
        }
        catch (Exception ex)
        {
            return Failed(ex, "being created");
        }
    }

    /// <summary>
    /// Bind the view and attach it to the ticker and its config, catching what either throws.
    /// [S-1881] A widget that needs setting up is not attached at all: its view is parked under the
    /// Set up button until the config or an account makes it useful.
    /// </summary>
    private void AttachView()
    {
        if (_view is FaultCard)
        {
            return;
        }

        if (!WidgetLiveness.Runs(Model.Config, SignedIn()))
        {
            _parked = true;
            return;
        }

        _parked = false;

        _attaching = true;
        _attachFault = null;

        try
        {
            using var faults = _host.Ticker.CatchFaults(OnTickFault);

            if (_view is IWidgetSurfaceAware aware)
            {
                aware.Bind(this);
            }

            if (_view is IWidgetView widgetView)
            {
                // Running before Attach: a view that throws half-way through it has subscribed to
                // something already, and the failure path's detach must still reach it.
                _running = true;
#if DEBUG
                Gallery.LiveCensus.ViewAttached(_view);
#endif
                widgetView.Attach(_host.Ticker, Model.Config);
            }
        }
        catch (Exception ex)
        {
            _attachFault ??= ex;
        }
        finally
        {
            _attaching = false;
        }

        if (_attachFault is { } fault)
        {
            DetachQuietly();
            _view = Failed(fault, "starting");
        }
    }

    private void ApplyViewConfig()
    {
        // [S-1881] A parked view hears nothing; it is built afresh with the config once it runs.
        if (!_running || _view is not IWidgetView widgetView)
        {
            return;
        }

        try
        {
            using var faults = _host.Ticker.CatchFaults(OnTickFault);
            widgetView.ApplyConfig(Model.Config);
        }
        catch (Exception ex)
        {
            Replace(Failed(ex, "applying its settings"));
        }
    }

    /// <summary>
    /// A tick of this card's view threw. During Attach it is an attach failure; afterwards the card
    /// is swapped on the next pass of the UI queue — not inside the ticker's loop, whose
    /// subscription list the view's Detach is about to change.
    /// </summary>
    private void OnTickFault(Exception error)
    {
        if (_attaching)
        {
            _attachFault ??= error;
            return;
        }

        // A card already taken off the surface: a shared service that subscribed during this
        // card's Attach outlives it, and its failures are not this card's any more.
        if (Parent is null)
        {
            return;
        }

        if (_view is FaultCard || !DispatcherQueue.TryEnqueue(() =>
        {
            if (_view is not FaultCard)
            {
                Replace(Failed(error, "updating"));
            }
        }))
        {
            Log.Warn(Scope, "could not schedule the error card for a failing widget");
        }
    }

    /// <summary>[S-663] The view hears its slot; the error card has none to hear.</summary>
    private void HookViewSize()
    {
        if (_view is IWidgetSizeAware sizeAware)
        {
            _view.SizeChanged += (_, e) =>
            {
                // [S-1881] Only a running view: a parked one was never attached and has nothing to fit.
                if (_running && ReferenceEquals(_view, sizeAware) && e.NewSize.Width > 0 && e.NewSize.Height > 0)
                {
                    sizeAware.ApplySize(new WidgetArea(e.NewSize.Width, e.NewSize.Height));
                }
            };
        }
    }

    /// <summary>[S-891] Retry: build the view again, as if the card were new. Also how safe mode's paused cards start.</summary>
    public void Retry()
    {
        if (_view is not FaultCard)
        {
            return;
        }

        _fault = null;
        var fresh = CreateViewNow(Model.Kind);
        var old = _view;
        _view = fresh;
        AttachView();
        Swap(old, _view);

        if (_view is not FaultCard)
        {
            Log.Info(Scope, $"widget '{Model.Id}' ({Model.Kind}) started again");
        }
    }

    /// <summary>Retry ignores the safe-mode switch: it is the user asking.</summary>
    private FrameworkElement CreateViewNow(WidgetKind kind)
    {
        try
        {
            return WidgetCatalog.CreateView(kind);
        }
        catch (Exception ex)
        {
            return Failed(ex, "being created");
        }
    }

    private FaultCard Failed(Exception error, string phase)
    {
        _fault = error;
        Log.Error(Scope, $"widget '{Model.Id}' ({Model.Kind}) failed while {phase}; showing its error card", error);
        return new FaultCard(this, paused: false);
    }

    /// <summary>Take the running view out (detaching it) and put <paramref name="next"/> in its place.</summary>
    private void Replace(FrameworkElement next)
    {
        var old = _view;
        DetachQuietly();
        _view = next;
        Swap(old, next);
    }

    /// <summary>Put <paramref name="next"/> where <paramref name="old"/> sat, with the header's trailing part to match.</summary>
    private void Swap(FrameworkElement old, FrameworkElement next)
    {
        var at = _content.Children.IndexOf(old);
        if (at >= 0)
        {
            _content.Children.RemoveAt(at);
        }

        Grid.SetRow(next, Grid.GetRow(old));
        Grid.SetColumn(next, Grid.GetColumn(old));
        _content.Children.Insert(at >= 0 ? at : _content.Children.Count, next);

        // The trailing slot of the header belongs to the view: a failed view's control goes with it.
        foreach (var trailing in _header.Children.OfType<FrameworkElement>().Where(c => Grid.GetColumn(c) == 2).ToList())
        {
            _header.Children.Remove(trailing);
        }

        if ((next as IWidgetHeaderContent)?.HeaderTrailing is { } added)
        {
            added.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(added, 2);
            _header.Children.Add(added);
        }

        HookViewSize();
        ApplySetup();
        ApplyHeader();
        ApplyDesign();
        ApplyEditMode(_host.EditMode);
        _host.OnCardFrameChanged(this);
    }

    /// <summary>Detach the running view, if it runs, catching what it throws.</summary>
    private void DetachQuietly()
    {
        try
        {
            StopView();
        }
        catch (Exception ex)
        {
            // It already failed once; its card is what matters now, and the failure is on record.
            Log.Warn(Scope, $"widget '{Model.Id}' threw while being taken out after a failure", ex);
        }
    }

    /// <summary>
    /// [S-1881] Detach the view if it is attached, exactly once: a stopped view holds no ticker
    /// subscription, no service and no event, whoever asks to stop it next.
    /// </summary>
    private void StopView()
    {
        if (!_running)
        {
            return;
        }

        _running = false;
#if DEBUG
        Gallery.LiveCensus.ViewDetached();
#endif
        ((IWidgetView)_view).Detach();
    }

#if DEBUG
    /// <summary>Debug only: the notice render shows a failed card without a widget that really fails.</summary>
    internal void FailForPreview(Exception error)
    {
        if (_view is not FaultCard)
        {
            Replace(Failed(error, "drawing a preview"));
        }
    }
#endif

    /// <summary>A short, user-facing line for the details flyout: what was thrown and its message.</summary>
    private static string Describe(Exception error)
        => $"{error.GetType().Name}: {error.Message}";

    /// <summary>
    /// [S-891] What Copy puts on the clipboard, for a bug report: the widget's kind and the whole
    /// exception with its stack, through the log's redactor ([S-983]) so no secret leaves in it.
    /// </summary>
    private static string Report(WidgetKind kind, Exception error)
        => LogRedactor.Redact($"Themia {kind} widget stopped working.\r\n{error}");

    /// <summary>
    /// [S-891] The body of a card whose view is not running: a mark, one line, Retry, and Details
    /// (the error itself) when it failed. Sheds like an empty state on a small tile — the glyph
    /// first, then the details link — so the Retry button is still there at XS.
    /// </summary>
    private sealed partial class FaultCard : Grid
    {
        public FaultCard(WidgetCard owner, bool paused)
        {
            Paused = paused;

            var full = paused ? Str.Shell_Card_Paused.Text() : Str.Shell_Card_Failed.Text();
            var brief = paused ? Str.Shell_Card_PausedShort.Text() : Str.Shell_Card_FailedShort.Text();
            var message = new TextBlock { Text = full };
            var state = Fluent.EmptyState(paused ? Glyphs.Pause : Glyphs.Broken, message, out var icon);

            var retry = new Button
            {
                Content = paused ? Str.Shell_Card_Start.Text() : Str.Common_Retry.Text(),
                HorizontalAlignment = HorizontalAlignment.Center,
                AllowFocusOnInteraction = false,
            };
            retry.Click += (_, _) => owner.Retry();

            // The word is long in some languages ("Erneut versuchen"); on a tile too narrow for it the
            // button keeps its job as the refresh mark, with the word as its name and tooltip.
            var retryText = paused ? Str.Shell_Card_Start.Text() : Str.Common_Retry.Text();
            ToolTipService.SetToolTip(retry, retryText);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(retry, retryText);

            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Spacing = Fluent.Space2,
                Margin = new Thickness(0, Fluent.Space2, 0, 0),
                Children = { retry },
            };

            if (!paused && owner._fault is { } error)
            {
                var details = new HyperlinkButton
                {
                    Content = Str.CtxMenu_Details.Text(),
                    AllowFocusOnInteraction = false,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                var kind = owner.Model.Kind;
                details.Click += (_, _) => ShowDetails(details, Describe(error), Report(kind, error));
                actions.Children.Add(details);
            }

            var column = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Children = { state, actions },
            };

            Children.Add(column);

            // The ladder down: the details link, then the glyph, then the long sentence go; the
            // short sentence and the button stay at XS. Decided here because the column measures its
            // children with unlimited height, so the empty state's own shedding never triggers.
            SizeChanged += (_, e) =>
            {
                var narrow = e.NewSize.Width < 150 || e.NewSize.Height < 110;
                var low = e.NewSize.Height < 160;
                icon.Visibility = low || narrow ? Visibility.Collapsed : Visibility.Visible;
                message.Text = narrow ? brief : full;

                // The buttons shed by what fits, not by a width guessed for English: the details
                // link goes first, then the button's word gives way to its mark.
                var room = e.NewSize.Width - (2 * Fluent.Space3);
                retry.Content = retryText;
                if (actions.Children.Count > 1)
                {
                    actions.Children[1].Visibility = narrow ? Visibility.Collapsed : Visibility.Visible;
                }

                actions.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
                if (actions.DesiredSize.Width > room && actions.Children.Count > 1)
                {
                    actions.Children[1].Visibility = Visibility.Collapsed;
                    actions.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
                }

                if (!paused && (narrow || actions.DesiredSize.Width > room))
                {
                    retry.Content = new FontIcon { Glyph = Glyphs.Refresh, FontSize = FluentType.Body.FontSize };
                }
            };
        }

        public bool Paused { get; }

        /// <summary>
        /// [S-891] The error and a Copy button. A windowed popup, not one inside the surface: outside
        /// edit mode the surface's region is the cards' own rects ([S-851]), and a flyout inside it
        /// was cut off at the card's edge.
        /// </summary>
        private static void ShowDetails(FrameworkElement anchor, string text, string report)
        {
            var body = Fluent.Text(FluentType.Body, "TextFillColorPrimaryBrush");
            body.Text = text;
            body.TextWrapping = TextWrapping.Wrap;
            body.IsTextSelectionEnabled = true;

            var copy = new Button
            {
                Content = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = Fluent.Space2,
                    Children = { Fluent.Icon(Glyphs.Copy, "TextFillColorPrimaryBrush", FluentType.Body.FontSize), new TextBlock { Text = Str.Common_Copy.Text() } },
                },
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(copy, Str.Common_Copy.Text());
            copy.Click += (_, _) =>
            {
                // ClipboardWatcher logs the Win32 reason when another app holds the clipboard.
                if (!ClipboardWatcher.SetText(report))
                {
                    Log.Warn(Scope, "the error details could not be copied");
                }
            };

            var content = new StackPanel
            {
                Spacing = Fluent.Space3,
                MaxWidth = 320,
                Children =
                {
                    new ScrollViewer { Content = body, MaxHeight = 240, VerticalScrollBarVisibility = ScrollBarVisibility.Auto },
                    copy,
                },
            };

            new Flyout { Content = content, Placement = FlyoutPlacementMode.Bottom, ShouldConstrainToRootBounds = false }.ShowAt(anchor);
        }
    }
}
