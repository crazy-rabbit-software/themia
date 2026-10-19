using CommunityToolkit.WinUI.Controls;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell.Config;

/// <summary>
/// What a settings page hands <see cref="PlaceSearch"/>: the place chosen now, and what to do when
/// the user picks another or clears it. The search itself owns nothing in the config.
/// </summary>
/// <param name="Chosen">The chosen place's label, or null when none is chosen yet.</param>
/// <param name="Picked">A place was picked: store its coordinates and label, and save.</param>
/// <param name="Cleared">[S-2848] The place was cleared: forget it (<c>IPlaced.ClearLocation</c>), and save.</param>
public sealed record PlaceSearchBinding(string? Chosen, Action<PlaceMatch> Picked, Action Cleared);

/// <summary>
/// [S-2840] The location row, one piece for both weather widgets and Sun &amp; Moon: the row says
/// which place is chosen (or that none is), and its one control is a search box with type-ahead
/// suggestions, the way Windows' own Weather and Clock pick a city.
/// <para>
/// The box never holds the choice: it is only ever a question. A pick replaces the choice at once
/// and empties the box; there is no "Change" mode to enter and no way to leave a widget with half a
/// choice. [S-2848] While a place is chosen, a clear button beside the box forgets it. Everything the box can be told is said inside the suggestion list, where the eye already
/// is: searching, the places ([S-2842]), no match, offline, failed ([S-2843]).
/// </para>
/// <para>
/// The place resolves to coordinates <b>once</b>, at the pick, never on a refresh: re-geocoding
/// would double the requests and let the widget move on its own the day the geocoder changes its
/// mind about which Springfield is the popular one.
/// </para>
/// </summary>
public static class PlaceSearch
{
    private const string Scope = "weather";

    /// <summary>As wide as the Clock's zone search and the verdict line under a field.</summary>
    private const double FieldWidth = 300;

    /// <summary>The host's geocoder ([S-2840]): the request is the host's, the row is the SDK's.</summary>
    private static Func<string, CancellationToken, Task<(PlaceOutcome Outcome, IReadOnlyList<PlaceMatch> Places, string? Detail)>>? _lookup;

    private static Func<string, CancellationToken, Task<(PlaceOutcome Outcome, IReadOnlyList<PlaceMatch> Places, string? Detail)>> Geocoder
        => Volatile.Read(ref _lookup) ?? throw new InvalidOperationException("no place lookup installed: the host installs it before any place row is shown");

    /// <summary>The host's geocoder. Once per process.</summary>
    internal static void InstallLookup(Func<string, CancellationToken, Task<(PlaceOutcome Outcome, IReadOnlyList<PlaceMatch> Places, string? Detail)>> lookup)
    {
        ArgumentNullException.ThrowIfNull(lookup);
        if (Interlocked.CompareExchange(ref _lookup, lookup, null) is not null)
        {
            throw new InvalidOperationException("the place lookup is installed once per process");
        }
    }

#if DEBUG
    /// <summary>
    /// Debug renders only: answers in place of the geocoder, so every state of the list can be drawn
    /// without a network. Null in every real run.
    /// </summary>
    internal static Func<string, CancellationToken, Task<(PlaceOutcome Outcome, IReadOnlyList<PlaceMatch> Places, string? Detail)>>? DebugLookup { get; set; }

    /// <summary>Debug renders only: type into the last search box built and search at once, as Enter does.</summary>
    internal static Action<string>? DebugType { get; private set; }
#endif

    /// <summary>
    /// [S-3722] The geocoder as a search on the properties menu: what the box says calls for nothing
    /// (empty, one letter), a word (no letter in it), or the lookup ([S-2841]); its places as lines,
    /// a click on one <paramref name="picked"/>; no match, offline and failed said in their place ([S-2843]).
    /// </summary>
    public static async Task<MenuSearchAnswer> MenuAnswerAsync(string typed, Action<PlaceMatch> picked, CancellationToken cancel)
    {
        ArgumentNullException.ThrowIfNull(picked);
        var query = PlaceQuery.Normalise(typed);
        switch (PlaceQuery.Classify(query, submitted: false))
        {
            case PlaceQueryStep.Idle:
            case PlaceQueryStep.TooShort:
                return new MenuSearchAnswer([]);
            case PlaceQueryStep.NotAName:
                return new MenuSearchAnswer([], Str.Shell_Place_TypeName.Text());
        }

#if DEBUG
        var find = DebugLookup ?? Geocoder;
#else
        var find = Geocoder;
#endif
        var (outcome, places, detail) = await find(query, cancel).ConfigureAwait(true);
        switch (outcome)
        {
            case PlaceOutcome.Matches:
                return new MenuSearchAnswer([.. places.Select(place => new ItemMenuEntry(Glyphs.Place,
                    place.Detail.Length > 0 ? place.Name + " · " + place.Detail : place.Name, () => picked(place)))]);
            case PlaceOutcome.NoMatch:
                return new MenuSearchAnswer([], Str.Shell_Place_NoMatch.Format(("query", query)));
            case PlaceOutcome.Offline:
                return new MenuSearchAnswer([], Str.Shell_Place_Offline.Text());
            case PlaceOutcome.NotAName:
                return new MenuSearchAnswer([], Str.Shell_Place_TypeName.Text());
            default:
                Log.Warn(Scope, $"place search failed: {detail}".TrimEnd());
                return new MenuSearchAnswer([], Str.Shell_Place_Failed.Text());
        }
    }

    /// <returns>
    /// Says a place chosen some other way (the Sun and Moon's exact fields): the description and the
    /// clear button follow it.
    /// </returns>
    public static Action<string?> Add(List<FrameworkElement> rows, PlaceSearchBinding binding)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(binding);

        var box = new AutoSuggestBox
        {
            PlaceholderText = Str.Shell_Place_SearchPlaceholder.Text(),
            QueryIcon = new SymbolIcon(Symbol.Find),
            Width = FieldWidth,

            // Arrowing through the list previews nothing and commits nothing; Enter or a click does.
            UpdateTextOnSelect = false,
        };

        AutomationProperties.SetName(box, Str.Shell_Place_SearchPlaceholder.Text());

        // [S-2848] Clear: only while there is a place to clear, so it never offers to do nothing.
        var clear = FluentLayout.IconCommand(Glyphs.Dismiss, Str.Shell_Place_Clear.Text());
        clear.Visibility = Chosen(binding.Chosen) ? Visibility.Visible : Visibility.Collapsed;

        var control = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Fluent.Space2,
            VerticalAlignment = VerticalAlignment.Center,
            Children = { clear, box },
        };

        var row = FluentLayout.Row(Glyphs.Place, Str.Shell_Place_Location.Text(), Describe(binding.Chosen), control);

        // One search at a time. A new one cancels the last, so a slow answer for "par" cannot land
        // on top of the results for "paris".
        CancellationTokenSource? pending = null;

        // The places in the list, and the query they answer: Enter picks the first of them only
        // while the box still says what they were found for.
        IReadOnlyList<PlaceMatch> shown = [];
        var shownFor = string.Empty;

        void Cancel()
        {
            pending?.Cancel();
            pending = null;
        }

        void Show(List<object> items)
        {
            box.ItemsSource = items;
            box.IsSuggestionListOpen = items.Count > 0;
        }

        void Close()
        {
            shown = [];
            shownFor = string.Empty;
            box.ItemsSource = null;
            box.IsSuggestionListOpen = false;
        }

        void Say(string glyph, string text, bool critical = false, bool busy = false)
        {
            shown = [];
            shownFor = string.Empty;
            Show([StatusItem(glyph, text, critical, busy)]);
        }

        void Pick(PlaceMatch place)
        {
            Cancel();
            binding.Picked(place);

            if (row.Child is SettingsCard card)
            {
                card.Description = Describe(place.Label);
            }

            clear.Visibility = Visibility.Visible;
            box.Text = string.Empty;
            Close();
        }

        clear.Click += (_, _) =>
        {
            Cancel();
            binding.Cleared();

            if (row.Child is SettingsCard card)
            {
                card.Description = Describe(null);
            }

            // The button goes with the place; focus moves to the search, the next thing to do.
            clear.Visibility = Visibility.Collapsed;
            box.Focus(FocusState.Programmatic);
        };

        async Task RunAsync(string query)
        {
            Cancel();
            var cts = new CancellationTokenSource();
            pending = cts;

            Say(Glyphs.Search, Str.Shell_Place_Searching.Text(), busy: true);

            try
            {
#if DEBUG
                var find = DebugLookup ?? Geocoder;
#else
                var find = Geocoder;
#endif
                var (outcome, places, detail) = await find(query, cts.Token).ConfigureAwait(true);

                if (!ReferenceEquals(pending, cts))
                {
                    return;
                }

                switch (outcome)
                {
                    case PlaceOutcome.Matches:
                        shown = places;
                        shownFor = query;
                        Show([.. places.Select(PlaceItem)]);
                        break;

                    case PlaceOutcome.NoMatch:
                        Say(Glyphs.Search, Str.Shell_Place_NoMatch.Format(("query", query)));
                        break;

                    case PlaceOutcome.Offline:
                        Say(Glyphs.Network, Str.Shell_Place_Offline.Text());
                        break;

                    case PlaceOutcome.NotAName:
                        Say(Glyphs.Info, Str.Shell_Place_TypeName.Text());
                        break;

                    default:
                        Log.Warn(Scope, $"place search failed: {detail}".TrimEnd());
                        Say(Glyphs.Warning, Str.Shell_Place_Failed.Text(), critical: true);
                        break;
                }
            }
            catch (OperationCanceledException)
            {
                // ignore: superseded by a newer search, or the page closed; the newer one reports.
            }
            finally
            {
                if (ReferenceEquals(pending, cts))
                {
                    pending = null;
                }

                cts.Dispose();
            }
        }

        // [S-2841] What the box says now calls for nothing, a word, or a search.
        void Start(bool submitted)
        {
            var query = PlaceQuery.Normalise(box.Text);

            switch (PlaceQuery.Classify(query, submitted))
            {
                case PlaceQueryStep.Idle:
                case PlaceQueryStep.TooShort:
                    Cancel();
                    Close();
                    return;

                case PlaceQueryStep.NotAName:
                    Cancel();
                    Say(Glyphs.Info, Str.Shell_Place_TypeName.Text());
                    return;
            }

            // [S-2844] The list already answers this query: Enter takes its best match, a pause
            // just shows it again.
            if (shown.Count > 0 && query == shownFor)
            {
                if (submitted)
                {
                    Pick(shown[0]);
                }
                else
                {
                    box.IsSuggestionListOpen = true;
                }

                return;
            }

            _ = RunAsync(query);
        }

        // The timer belongs to this page's thread and dies with the page: stopped on unload, so a
        // keystroke made just before the settings window closed does not search into nowhere.
        var timer = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()?.CreateTimer();

        if (timer is not null)
        {
            timer.Interval = PlaceQuery.Debounce;
            timer.IsRepeating = false;
            timer.Tick += (_, _) => Start(submitted: false);
        }

        box.TextChanged += (_, args) =>
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
            {
                return;
            }

            timer?.Stop();

            // Emptied: close at once rather than after the pause.
            if (box.Text.Length == 0)
            {
                Start(submitted: false);
                return;
            }

            timer?.Start();
        };

        // [S-2844] Enter, a click on a suggestion or on the search glyph. A highlighted place is
        // picked; otherwise the query is asked now, or its best match taken if the list answers it.
        box.QuerySubmitted += (_, args) =>
        {
            timer?.Stop();

            if (args.ChosenSuggestion is ListViewItem { Tag: PlaceMatch place })
            {
                Pick(place);
                return;
            }

            Start(submitted: true);
        };

        box.Unloaded += (_, _) =>
        {
            timer?.Stop();
            Cancel();
        };

#if DEBUG
        DebugType = text =>
        {
            box.Text = text;
            Start(submitted: true);
        };
#endif

        rows.Add(row);

        return chosen =>
        {
            if (row.Child is SettingsCard card)
            {
                card.Description = Describe(chosen);
            }

            clear.Visibility = Chosen(chosen) ? Visibility.Visible : Visibility.Collapsed;
        };
    }

    private static bool Chosen(string? chosen) => !string.IsNullOrWhiteSpace(chosen);

    /// <summary>The row's description: the chosen place in full, or that there is none yet.</summary>
    private static string Describe(string? chosen)
        => Chosen(chosen) ? chosen! : Str.Shell_Place_NotChosen.Text();

    /// <summary>[S-2842] A suggestion: the city on the first line, what sets it apart under it.</summary>
    private static ListViewItem PlaceItem(PlaceMatch place)
    {
        var name = Fluent.Text(FluentType.Body);
        name.Text = place.Name;

        var lines = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Children = { name } };

        if (place.Detail.Length > 0)
        {
            var detail = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
            detail.Text = place.Detail;
            lines.Children.Add(detail);
        }

        var item = new ListViewItem
        {
            Content = lines,
            Tag = place,
            Padding = new Thickness(Fluent.Space3, Fluent.Space1, Fluent.Space3, Fluent.Space1),
        };

        AutomationProperties.SetName(item, place.Label);
        return item;
    }

    /// <summary>
    /// [S-2843] A line in the list that is a state, not a choice: disabled, so neither the arrows nor
    /// a click can pick it, with a ring while searching and the critical colour only for a failure.
    /// </summary>
    private static ListViewItem StatusItem(string glyph, string text, bool critical, bool busy)
    {
        FrameworkElement mark = busy
            ? new ProgressRing { IsActive = true, Width = Fluent.IconSize, Height = Fluent.IconSize }
            : new FontIcon
            {
                Glyph = glyph,
                FontSize = Fluent.IconSize,
                Foreground = critical
                    ? ThemeBrush.Of("SystemFillColorCriticalBrush", Colors.Red)
                    : ThemeBrush.Of("TextFillColorSecondaryBrush", Colors.Gray),
            };

        mark.VerticalAlignment = VerticalAlignment.Top;
        mark.Margin = new Thickness(0, Fluent.Space1 / 2, 0, 0);

        var message = Fluent.Text(FluentType.Body, "TextFillColorSecondaryBrush");
        message.Text = text;
        message.TextTrimming = TextTrimming.None;
        message.TextWrapping = TextWrapping.Wrap;
        Grid.SetColumn(message, 1);

        var line = new Grid
        {
            ColumnSpacing = Fluent.Space3,
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
            },
            Children = { mark, message },
        };

        var item = new ListViewItem
        {
            Content = line,
            IsEnabled = false,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(Fluent.Space3, Fluent.Space2, Fluent.Space3, Fluent.Space2),
        };

        AutomationProperties.SetName(item, text);
        AutomationProperties.SetLiveSetting(item, AutomationLiveSetting.Polite);
        return item;
    }
}
