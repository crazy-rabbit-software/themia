using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Settings;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell;

/// <summary>[S-3722] [S-3742] A search's level and a long level's filter: the field pinned under the head, the rows under it.</summary>
internal partial class MenuPanel
{
    /// <summary>
    /// [S-3722] A search's rows: every answer (one row each, the chosen one ticked; a word in their
    /// place while it is looking, finds nothing or is offline), then the search's fixed rows behind a rule.
    /// </summary>
    private List<MenuNode> SearchNodes(Step step)
    {
        var nodes = new List<MenuNode>(step.Results);
        if (step.Search is { Fixed.Count: > 0 } search)
        {
            nodes.Add(MenuNode.Separator(step.Key + "/-fixed"));
            nodes.AddRange(FromEntries(search.Fixed, step.Key + "/fixed"));
        }

        return nodes;
    }

    /// <summary>
    /// [S-3742] A long level's rows: those whose words or keywords hold what is typed in its filter
    /// (every one while it is empty), the level's notes and rules with them, or "No results" in their place.
    /// </summary>
    private static List<MenuNode> Filtered(Step step, IReadOnlyList<MenuNode> rows)
    {
        var typed = step.Text;
        if (typed.Trim().Length == 0)
        {
            return [.. rows];
        }

        var kept = rows.Where(n => Listed(n) && SettingsMenu.Matches(n.Text, n.Keywords, typed)).ToList();
        return kept.Count > 0 ? kept : [new MenuNode(step.Key + "/none", MenuNodeKind.Note) { Text = Str.Common_NoResults.Text() }];
    }

    /// <summary>
    /// [S-3742] The filter of a level with more than ten rows to pick (Karpo's): it has the keyboard as
    /// the level opens, and each letter typed keeps the rows that match at once; Enter picks the first
    /// one kept, Down moves to it, Escape empties it first and then goes back.
    /// </summary>
    private TextBox FilterBox(Step step)
    {
        var hint = step.Holder?.Filter ?? Str.Menu_Filter.Text();
        var box = new TextBox
        {
            Text = step.Text,
            PlaceholderText = hint,
            IsSpellCheckEnabled = false,
            IsTextPredictionEnabled = false,
        };
        AutomationProperties.SetName(box, hint);

        box.TextChanged += (_, _) =>
        {
            if (_path.Count == 0 || !ReferenceEquals(_path[^1], step))
            {
                return;
            }

            step.Text = box.Text;
            FillLevel(_shown, Current());
        };

        box.KeyDown += (_, e) =>
        {
            if (e.Key == Windows.System.VirtualKey.Enter && _shown.Views.FirstOrDefault(v => v is { Button.IsEnabled: true }) is { } first)
            {
                Enter(first.Node, keys: true);
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Down && _shown.Views.FirstOrDefault(v => v is { Button.IsEnabled: true }) is { } next)
            {
                next.Button.Focus(FocusState.Keyboard);
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Escape && box.Text.Length > 0)
            {
                box.Text = string.Empty;
                e.Handled = true;
            }
        };

        return box;
    }

    /// <summary>
    /// [S-3722] A search's field, pinned under its head: what is typed is asked once typing settles (at
    /// once for an answer in hand), each answer replacing the one before; Enter picks the first answer,
    /// Down moves to it.
    /// </summary>
    private TextBox SearchBox(Step step, MenuSearch search)
    {
        var box = new TextBox
        {
            Text = step.Text,
            PlaceholderText = search.Placeholder,
            IsSpellCheckEnabled = false,
            IsTextPredictionEnabled = false,
        };
        AutomationProperties.SetName(box, search.Placeholder);

        void OnSettled(Microsoft.UI.Dispatching.DispatcherQueueTimer timer, object _) => Find(step, box.Text);

        box.TextChanged += (_, _) =>
        {
            step.Text = box.Text;
            var settle = step.Search?.Settle ?? TimeSpan.Zero;
            if (settle <= TimeSpan.Zero)
            {
                Find(step, box.Text);
                return;
            }

            // A pause in typing, not a guess at readiness: the geocoder is asked for what was meant, once.
            step.Settle ??= DispatcherQueue.CreateTimer();
            step.Settle.Stop();
            step.Settle.Interval = settle;
            step.Settle.IsRepeating = false;
            step.Settle.Tick -= OnSettled;
            step.Settle.Tick += OnSettled;
            step.Settle.Start();
        };

        box.KeyDown += (_, e) =>
        {
            if (e.Key == Windows.System.VirtualKey.Enter
                && _shown.Views.FirstOrDefault(v => v is { Button.IsEnabled: true } && v.Node.Key.Contains("/q/", StringComparison.Ordinal)) is { } first)
            {
                Enter(first.Node, keys: true);
                e.Handled = true;
            }
            else if (e.Key == Windows.System.VirtualKey.Down && _shown.Views.FirstOrDefault(v => v is { Button.IsEnabled: true }) is { } next)
            {
                next.Button.Focus(FocusState.Keyboard);
                e.Handled = true;
            }
        };

        return box;
    }

    /// <summary>[S-3722] Ask the search for <paramref name="typed"/>; a newer question drops an older one's answer.</summary>
    private async void Find(Step step, string typed)
    {
        try
        {
            if (step.Search is not { } search)
            {
                return;
            }

            step.Pending?.Cancel();
            using var cts = new CancellationTokenSource();
            step.Pending = cts;

            // A question that goes out to the network says so while it is out.
            if (search.Settle > TimeSpan.Zero && typed.Trim().Length > 0)
            {
                step.Results = [new MenuNode(step.Key + "/q/note", MenuNodeKind.Note) { Text = Str.Shell_Place_Searching.Text() }];
                Shown(step);
            }

            var answer = await search.Find(typed.Trim(), cts.Token).ConfigureAwait(true);
            if (!ReferenceEquals(step.Pending, cts) || !_path.Contains(step))
            {
                return;
            }

            step.Pending = null;
            step.Results = answer.Note is { } note
                ? [new MenuNode(step.Key + "/q/note", MenuNodeKind.Note) { Text = note }]
                : FromEntries(answer.Lines, step.Key + "/q");
            Shown(step);
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

    /// <summary>[S-3722] A search's answers came: drawn where its level is the one shown, its field keeping the keyboard.</summary>
    private void Shown(Step step)
    {
        if (_path.Count > 0 && ReferenceEquals(_path[^1], step))
        {
            FillLevel(_shown, Current());
        }
    }
}
