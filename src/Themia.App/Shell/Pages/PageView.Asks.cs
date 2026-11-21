using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Settings;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Windows.System;
using I = Themia.Sdk.Kit.ShellMetrics.Inline;
using P = Themia.Sdk.Kit.ShellMetrics.Page;

namespace Themia.App.Shell;

/// <summary>
/// [S-2351] What a page asks, asked in place (owner, 2026-11-20: "fence name setting opens a new subpage where i can
/// enter the name, this should be a section not an own page. this type of settings component should not exist anywhere
/// in themia settings"): a line of text (Rename…, Add…, a path typed, a fence's name) or a question (Delete…, Reset all,
/// Sign out) opens as the one inner level of the card of what asks it, under a hairline on the secondary card fill as an
/// inline picker does: the field holding what it holds (selected), why it is refused under it, the accept in its own
/// words (greyed while refused); or the question, what goes with it, its action and Cancel. Enter gives it; Escape, a
/// press anywhere else on the page or the head again closes it, keeping nothing. One at a time per page
/// (<see cref="AskInPlace"/>, Core's rules). Never a page of its own.
/// </summary>
internal sealed partial class PageView
{
    /// <summary>[S-2351] A question asked after a step of its own (<see cref="ConfirmAt"/>) at the open section's row; null for none.</summary>
    private PageQuestion? _question;

    /// <summary>[S-2351] A press away from the open section closed it at this drawing: it is drawn closed on the release, unless something drew since.</summary>
    private int? _closedAt;

    /// <summary>[S-2351] What a row's card asks now: the section's key, the row holding it, its field or its question.</summary>
    private readonly record struct Asked(string Key, string Holder, PageEdit? Edit, PageQuestion? Question);

    /// <summary>
    /// [S-2351] [S-3727] A question asked after a step of its own (an import's, once its file is read; a preset over a
    /// look of one's own), in place in the card of the row keyed <paramref name="at"/>: the cards along the way open, the
    /// section scrolled into sight; only its action does it.
    /// </summary>
    public void ConfirmAt(string at, PageQuestion question)
    {
        ArgumentNullException.ThrowIfNull(at);
        ArgumentNullException.ThrowIfNull(question);
        _model ??= _build();
        var keys = FromKeys();
        _question = question;
        _typed.Remove(at + "#ask");

        // The cards it stands in open (a picker inside one unfolded); on the page itself it is the one card open.
        _nav.AskAt(Nav(_model), at);
        Opened(at, keys);
    }

    /// <summary>
    /// [S-2351] A click on what asks (an asking row's head, a tool that asks first): its section opens in its card (the
    /// one open on the page where the card stands on the page; any other section closes), or, open, closes keeping nothing.
    /// </summary>
    private void ToggleAsk(string key, string holder, bool inside)
    {
        var keys = FromKeys();
        _question = null;
        _typed.Remove(key + "#ask");
        if (!_nav.ToggleAsk(key, holder, inside))
        {
            // [S-2361] Only the card that held it is drawn again, after the click.
            Redraw(() => Refocus(new FocusTag(holder + "#head", 0, keys)));
            return;
        }

        Opened(holder, keys);
    }

    /// <summary>
    /// [S-2351] A section just opened: [S-2361] its card drawn again (alone, after the event that asked), then scrolled into
    /// sight as little as it takes ([S-2347]), the keyboard in it, with its ring where the keys opened it (<paramref name="keys"/>).
    /// </summary>
    private void Opened(string holder, bool keys) => Redraw(() => Focused(holder, keys));

    /// <summary>
    /// [S-2351] The opened section's card, drawn: revealed, the keyboard in its field or on its answer. Opened from the keys,
    /// the answer takes the keyboard's focus and its ring: the posted drawing made a new control, which a programmatic focus
    /// would have drawn as the pointer left it (no ring), the head the keys pressed being gone with the card it stood in.
    /// </summary>
    private void Focused(string holder, bool keys)
    {
        Reveal(holder);
        if (AskedNow() is not { } asked)
        {
            return;
        }

        // [S-3727] A danger's keyboard on Cancel, so Enter straight after it keeps everything; any other question's on its action.
        var tag = asked.Key + (asked.Question is null ? "#field" : asked.Question.Danger ? "#cancel" : "#accept");
        var how = keys ? FocusState.Keyboard : FocusState.Programmatic;
        switch (Tagged(this, tag))
        {
            case TextBox box:
                box.Focus(how);
                box.SelectAll();
                break;
            case Control control:
                control.Focus(how);
                break;
        }
    }

    /// <summary>[S-2351] Escape, Cancel: the open section closes, nothing kept, the keyboard back on its head. False where none was open.</summary>
    private bool CloseAsk()
    {
        var holder = AskedNow()?.Holder;
        var keys = FromKeys();
        if (!_nav.Asks.Cancel())
        {
            return false;
        }

        _question = null;
        Redraw(() =>
        {
            if (holder is not null)
            {
                Refocus(new FocusTag(holder + "#head", 0, keys));
            }
        });

        return true;
    }

    /// <summary>[S-2351] Nothing asked any more (another card opened, a tab, the page going): closed without a drawing of its own.</summary>
    private void DropAsk()
    {
        _nav.Asks.Cancel();
        _question = null;
    }

    /// <summary>[S-2351] [S-3549] A tool used (its click, its key on its row): one that asks first opens its question in its card; any other does its thing.</summary>
    private void UseTool(PageRow row, PageTool tool, bool inside)
    {
        if (tool.Question is not null)
        {
            ToggleAsk(tool.Key, row.Key, inside);
        }
        else
        {
            Do(tool.Run);
        }
    }

    /// <summary>[S-2351] What <paramref name="row"/>'s card asks now: its own line or question, a question asked at it, or one of its tools'; null for nothing.</summary>
    private Asked? AskOf(PageRow row)
    {
        if (_nav.Asks.Open is not { } open)
        {
            return null;
        }

        if (open == row.Key)
        {
            if (row.Kind == SettingsRowKind.Ask)
            {
                return new Asked(open, row.Key, row.Edit, row.Question);
            }

            return _question is { } asked ? new Asked(open, row.Key, null, asked) : null;
        }

        return row.Tools?.FirstOrDefault(t => t.Key == open && t.Question is not null) is { Question: { } question }
            ? new Asked(open, row.Key, null, question)
            : null;
    }

    /// <summary>[S-2351] What is asked now, found on the page as built; null for nothing.</summary>
    private Asked? AskedNow() => _nav.Asks.Open is { } open && _model is not null && HolderOf(_model, open) is { } row ? AskOf(row) : null;

    /// <summary>[S-2351] The row whose card holds the section keyed <paramref name="key"/> (its own, or the one whose tool it is), at any depth.</summary>
    private static PageRow? HolderOf(PageModel model, string key)
    {
        static PageRow? In(IReadOnlyList<PageRow> rows, string key)
        {
            foreach (var row in rows)
            {
                if (row.Key == key || (row.Tools?.Any(t => t.Key == key) ?? false))
                {
                    return row;
                }

                if (row.Rows is { } inner && In(inner, key) is { } found)
                {
                    return found;
                }
            }

            return null;
        }

        return model.Sections.Select(s => In(s.Rows, key)).FirstOrDefault(r => r is not null);
    }

    /// <summary>
    /// [S-2351] A press on the page while a section is open: anywhere but in the card holding it, the section closes,
    /// nothing kept. Only the state changes here; whatever the press does draws the page (a head, a box), and a press that
    /// draws nothing has the page drawn on its release ([S-2346]: never inside the event that asked).
    /// </summary>
    private void PressedOnPage(object sender, PointerRoutedEventArgs e)
    {
        if (_nav.Asks.Open is not { } open || _model is null)
        {
            return;
        }

        var holder = HolderOf(_model, open) is { } row ? Tagged(this, row.Key + "#row") : null;
        var inside = holder is not null && e.OriginalSource is DependencyObject source && Under(source, holder);
        if (_nav.Asks.PressedOutside(inside))
        {
            _question = null;
            _closedAt = Drawings;
        }
    }

    /// <summary>[S-2351] The press that closed a section let go: the page drawn once, after the event, where nothing drew it since.</summary>
    private void ReleasedOnPage(object sender, PointerRoutedEventArgs e)
    {
        if (_closedAt is not { } at)
        {
            return;
        }

        _closedAt = null;
        if (Drawings == at)
        {
            // [S-2361] Its card alone, drawn after this event.
            Redraw();
        }
    }

    /// <summary>Whether <paramref name="node"/> is <paramref name="root"/> or stands inside it.</summary>
    private static bool Under(DependencyObject node, DependencyObject root)
    {
        for (var at = node; at is not null; at = VisualTreeHelper.GetParent(at))
        {
            if (ReferenceEquals(at, root))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// [S-2351] The section as its card's inner level: under the card's hairline on the secondary card fill, 12 in, its
    /// parts from where the rows' names start to 16 from the card's end; square at its foot <paramref name="inside"/> a card.
    /// </summary>
    private Border AskSection(Asked asked, string said, bool inside)
    {
        FrameworkElement part = asked.Question is { } question ? QuestionPart(asked.Key, question) : FieldPart(asked.Key, asked.Edit!, said);
        part.Margin = new Thickness(P.NameAt - I.Pad, 0, P.EndInset - I.Pad, 0);
        return new Border
        {
            Background = ShellBrush.Of(ShellRole.CardSecondary),
            Padding = new Thickness(I.Pad),
            CornerRadius = inside ? new CornerRadius(0) : new CornerRadius(0, 0, P.CardRadius, P.CardRadius),
            Child = part,
            Tag = asked.Key + "#ask",
        };
    }

    /// <summary>
    /// [S-2351] [S-3721] A line of text asked: the field (what it holds, selected as it opens; its hint while empty) with
    /// the accept at its end in its own words, greyed while what is typed is refused, and why under the field (nothing
    /// said while it is only empty). Enter gives it as the accept does; Escape closes it. What is typed outlives a drawing.
    /// </summary>
    private StackPanel FieldPart(string key, PageEdit edit, string said)
    {
        var box = new TextBox
        {
            Text = _typed.GetValueOrDefault(key + "#ask") ?? edit.Initial,
            PlaceholderText = edit.Placeholder,
            MinHeight = P.Control,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center,
            Tag = key + "#field",
        };
        AutomationProperties.SetName(box, said);

        var words = edit.Accept ?? Str.Common_Done.Text();
        var accept = new Button
        {
            Content = words,
            MinWidth = P.ActionLeast,
            MaxWidth = P.ButtonMost,
            Height = P.Control,
            Padding = new Thickness(P.Label, 0, P.Label, 0),
            Margin = new Thickness(Fluent.Space2, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Top,
            Tag = key + "#accept",
        };
        FluentLayout.ApplyStyle(accept, "AccentButtonStyle");
        AutomationProperties.SetName(accept, words);
        Grid.SetColumn(accept, 1);

        var why = CaptionText(string.Empty, critical: true);
        void Check()
        {
            var reason = edit.Reason(box.Text);
            accept.IsEnabled = reason is null;
            why.Text = reason ?? string.Empty;
            why.Visibility = reason is { Length: > 0 } ? Visibility.Visible : Visibility.Collapsed;
        }

        void Give()
        {
            var answer = _nav.Asks.Accept(key, box.Text, edit.AllowEmpty, edit.Refuse);
            if (!answer.Kept)
            {
                Check();
                return;
            }

            _typed.Remove(key + "#ask");
            var holder = _model is { } model ? HolderOf(model, key)?.Key : null;
            var keys = FromKeys();
            Do(() => edit.Commit(answer.Text));
            Show();
            if (holder is not null)
            {
                Refocus(new FocusTag(holder + "#head", 0, keys));
            }
        }

        box.TextChanged += (_, _) =>
        {
            _typed[key + "#ask"] = box.Text;
            Check();
        };
        box.KeyDown += (_, e) =>
        {
            switch (e.Key)
            {
                case VirtualKey.Enter:
                    Give();
                    e.Handled = true;
                    break;
                case VirtualKey.Escape:
                    CloseAsk();
                    e.Handled = true;
                    break;
            }
        };
        accept.Click += (_, _) => Give();
        Check();

        var line = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                new ColumnDefinition { Width = GridLength.Auto },
            },
            Children = { box, accept },
        };
        return new StackPanel { Spacing = Fluent.Space1, Children = { line, why } };
    }

    /// <summary>
    /// [S-2351] [S-3727] An "are you sure?": the question, what goes with it under it in Caption, then its action (the
    /// accent's, the critical colour's for a danger) and Cancel. Only the action does it; Cancel or Escape closes it.
    /// </summary>
    private StackPanel QuestionPart(string key, PageQuestion question)
    {
        var asked = Fluent.Text(FamilyType.Body, "TextFillColorPrimaryBrush");
        asked.Text = question.Question;
        asked.TextWrapping = TextWrapping.Wrap;
        var stack = new StackPanel { Spacing = Fluent.Space1, Children = { asked } };
        if (question.Detail is { Length: > 0 } detail)
        {
            stack.Children.Add(CaptionText(detail));
        }

        var accept = new Button
        {
            Content = question.Accept,
            MinWidth = P.ActionLeast,
            MaxWidth = P.ButtonMost,
            Height = P.Control,
            Padding = new Thickness(P.Label, 0, P.Label, 0),
            Tag = key + "#accept",
        };
        FluentLayout.ApplyStyle(accept, "AccentButtonStyle");
        if (question.Danger)
        {
            var critical = ThemeBrush.Of("SystemFillColorCriticalBrush", Colors.IndianRed);
            accept.Resources["AccentButtonBackground"] = critical;
            accept.Resources["AccentButtonBackgroundPointerOver"] = critical;
            accept.Resources["AccentButtonBackgroundPressed"] = critical;
        }

        var cancel = new Button
        {
            Content = Str.Common_Cancel.Text(),
            MinWidth = P.ActionLeast,
            Height = P.Control,
            Padding = new Thickness(P.Label, 0, P.Label, 0),
            Tag = key + "#cancel",
        };
        accept.Click += (_, _) =>
        {
            if (_nav.Asks.Answer(key))
            {
                _question = null;
                Do(question.Done);
                Show();
            }
        };
        cancel.Click += (_, _) => CloseAsk();

        stack.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = Fluent.Space2,
            Margin = new Thickness(0, Fluent.Space2, 0, 0),
            Children = { accept, cancel },
        });
        return stack;
    }
}
