using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Settings;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell;

/// <summary>[S-3722] [S-3725] A search's level: its box on top, its answers under it.</summary>
internal partial class MenuPanel
{
    /// <summary>
    /// The lines of the level a node opens: its submenu, or a search's answers and its fixed lines.
    /// [S-3725] A search shows its first five answers and Show all (N) under them until that is
    /// clicked, and the answers' room is always six lines (<see cref="SearchRoom"/>: five answers and
    /// Show all's slot, kept free under a word or fewer answers), so the submenu keeps its height
    /// while it is typed into.
    /// </summary>
    private List<MenuNode> LevelNodes(Level level, MenuNode owner)
    {
        if (owner.Search is not { } search)
        {
            return owner.Filter is null ? [.. owner.Children ?? []] : Filtered(level, owner);
        }

        var answers = level.Results;
        var fill = SearchRoom.Fill(answers.Count, level.ShowAll);
        var nodes = new List<MenuNode>(answers.Take(fill.Shown));
        if (fill.ShowAll)
        {
            nodes.Add(new MenuNode(owner.Key + "/all")
            {
                Text = Str.Menu_ShowAll.Format(("count", LocaleFormat.Integer(answers.Count))),
                Run = () => ShowAllAnswers(level),
            });
        }

        if (fill.Free > 0)
        {
            nodes.Add(new MenuNode(owner.Key + "/pad" + fill.Free, MenuNodeKind.Spacer) { Height = fill.Free * LineHeight });
        }

        if (search.Fixed.Count > 0)
        {
            nodes.Add(MenuNode.Separator(owner.Key + "/-fixed"));
            nodes.AddRange(FromEntries(search.Fixed, owner.Key + "/fixed"));
        }

        return nodes;
    }

    /// <summary>
    /// [S-3537] A filtered level's lines: those whose words or keywords hold what is typed in its box
    /// (every one while it is empty), or "No results" in their place.
    /// </summary>
    private static List<MenuNode> Filtered(Level level, MenuNode owner)
    {
        var typed = level.Box?.Text ?? string.Empty;
        var kept = (owner.Children ?? []).Where(n => n.Kind != MenuNodeKind.Line || SettingsMenu.Matches(n.Text, n.Keywords, typed)).ToList();
        return kept.Any(n => n.Kind == MenuNodeKind.Line)
            ? kept
            : [new MenuNode(owner.Key + "/none", MenuNodeKind.Note) { Text = Str.Common_NoResults.Text() }];
    }

    /// <summary>
    /// [S-3537] A filter's box, on top of a long choice's submenu: each letter typed keeps the options
    /// that match at once (nothing goes out to be asked); Enter picks the first one kept, Down moves to it.
    /// </summary>
    private TextBox FilterBox(Level level, string hint)
    {
        var box = new TextBox
        {
            PlaceholderText = hint,
            Height = LineHeight,
            MinHeight = LineHeight,
            Margin = new Thickness(Fluent.Space2, Fluent.Space2, Fluent.Space2, 0),
            IsSpellCheckEnabled = false,
            IsTextPredictionEnabled = false,
        };
        AutomationProperties.SetName(box, hint);

        box.TextChanged += (_, _) =>
        {
            if (level.Anchor is { } anchor && _levels.IndexOf(level) is var at and > 0)
            {
                FillLevel(level, LevelNodes(level, anchor.Node));
                Place(level, at);
            }
        };

        box.KeyDown += (_, e) =>
        {
            if (e.Key == Windows.System.VirtualKey.Enter && level.Views.FirstOrDefault(v => v is { Button.IsEnabled: true }) is { } first)
            {
                OnLine(level, first);
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Down && level.Views.FirstOrDefault(v => v is { Button.IsEnabled: true }) is { } next)
            {
                next.Button.Focus(FocusState.Keyboard);
                e.Handled = true;
            }
        };

        box.GotFocus += (_, _) => CloseFrom(_levels.IndexOf(level) + 1);
        return box;
    }

    /// <summary>
    /// [S-3722] A search's box, on top of its submenu: what is typed is asked once typing settles (at
    /// once for an answer in hand), each answer replacing the one before; Enter picks the first line,
    /// Down moves to it.
    /// </summary>
    private TextBox SearchBox(Level level, MenuSearch search)
    {
        var box = new TextBox
        {
            PlaceholderText = search.Placeholder,
            Height = LineHeight,
            MinHeight = LineHeight,
            Margin = new Thickness(Fluent.Space2, Fluent.Space2, Fluent.Space2, 0),
            IsSpellCheckEnabled = false,
            IsTextPredictionEnabled = false,
        };
        AutomationProperties.SetName(box, search.Placeholder);

        box.TextChanged += (_, _) =>
        {
            var settle = level.Search?.Settle ?? TimeSpan.Zero;
            if (settle <= TimeSpan.Zero)
            {
                Ask(level, box.Text);
                return;
            }

            // A pause in typing, not a guess at readiness: the geocoder is asked for what was meant, once.
            level.Settle ??= DispatcherQueue.CreateTimer();
            level.Settle.Stop();
            level.Settle.Interval = settle;
            level.Settle.IsRepeating = false;
            level.Settle.Tick -= OnSettled;
            level.Settle.Tick += OnSettled;
            level.Settle.Start();
        };

        void OnSettled(Microsoft.UI.Dispatching.DispatcherQueueTimer timer, object _) => Ask(level, box.Text);

        box.KeyDown += (_, e) =>
        {
            if (e.Key == Windows.System.VirtualKey.Enter
                && level.Views.FirstOrDefault(v => v is { Button.IsEnabled: true } && v.Node.Key.Contains("/q/", StringComparison.Ordinal)) is { } first)
            {
                OnLine(level, first);
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Down && level.Views.FirstOrDefault(v => v is { Button.IsEnabled: true }) is { } next)
            {
                next.Button.Focus(FocusState.Keyboard);
                e.Handled = true;
            }
        };

        box.GotFocus += (_, _) => CloseFrom(_levels.IndexOf(level) + 1);
        return box;
    }

    /// <summary>[S-3722] Ask the search for <paramref name="typed"/>; a newer question drops an older one's answer.</summary>
    private async void Ask(Level level, string typed)
    {
        try
        {
            if (level.Search is not { } search || level.Anchor is not { } anchor)
            {
                return;
            }

            level.Pending?.Cancel();
            using var cts = new CancellationTokenSource();
            level.Pending = cts;

            // A question that goes out to the network says so while it is out.
            level.ShowAll = false;
            if (search.Settle > TimeSpan.Zero && typed.Trim().Length > 0)
            {
                level.Results = [new MenuNode(anchor.Node.Key + "/q/note") { Text = Str.Shell_Place_Searching.Text(), Disabled = true }];
                FillLevel(level, LevelNodes(level, anchor.Node));
            }

            var answer = await search.Find(typed.Trim(), cts.Token).ConfigureAwait(true);
            if (!ReferenceEquals(level.Pending, cts) || !_levels.Contains(level))
            {
                return;
            }

            level.Pending = null;
            level.Results = answer.Note is { } note
                ? [new MenuNode(anchor.Node.Key + "/q/note") { Text = note, Disabled = true }]
                : FromEntries(answer.Lines, anchor.Node.Key + "/q");
            FillLevel(level, LevelNodes(level, anchor.Node));
            Place(level, _levels.IndexOf(level));
        }
        catch (OperationCanceledException)
        {
            // ignore: a newer question is out; its answer is the one shown.
        }
        catch (Exception ex)
        {
            // An async void from a XAML event: nothing may escape.
            Log.Error(Scope, "a menu search failed", ex);
        }
    }

    /// <summary>[S-3725] Show all: every answer of the search, until the next question; the keyboard goes on to the sixth.</summary>
    private void ShowAllAnswers(Level level)
    {
        var at = _levels.IndexOf(level);
        if (level.Anchor is not { } anchor || at < 0)
        {
            return;
        }

        var focused = level.Views.Any(v => v is not null && v.Node.Key.EndsWith("/all", StringComparison.Ordinal) && v.Button.FocusState == FocusState.Keyboard);
        level.ShowAll = true;
        FillLevel(level, LevelNodes(level, anchor.Node));
        Place(level, at);

        if (focused && level.Views.Where(v => v is { Button.IsEnabled: true }).Skip(SearchRoom.Answers).FirstOrDefault() is { } next)
        {
            next.Button.Focus(FocusState.Keyboard);
        }
    }
}
