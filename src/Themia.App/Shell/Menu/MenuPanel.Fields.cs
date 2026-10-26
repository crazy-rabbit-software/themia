using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell;

/// <summary>
/// [S-3736] A line of text is a level of its own: the field alone, filling its row, the reason it
/// cannot be given under it while it cannot, and the row that gives it where the question has one.
/// [S-3556] A password's field is Windows' own <see cref="PasswordBox"/>: a bullet for each letter, no
/// copy or cut (paste works), no reveal button, the IME off while it has the keyboard, and to UI
/// Automation an edit that IsPassword and exposes no value. What it holds is never in the menu's
/// nodes, a tooltip or the log, and is wiped as its form's level closes.
/// [S-3737] An "are you sure?" is a level of its own too: the question and what goes with it, then the
/// row that does it and Cancel.
/// </summary>
internal partial class MenuPanel
{
    /// <summary>[S-3736] A drawn field: its box (a text box or, masked, a password box), and the level it is typed for.</summary>
    private sealed class FieldView(FrameworkElement root, TextBox? text, PasswordBox? password)
    {
        public FrameworkElement Root { get; } = root;

        public TextBox? Text { get; } = text;

        public PasswordBox? Password { get; } = password;

        public Control Box => (Control?)Text ?? Password!;

        /// <summary>The box is being written by the menu: not the user's typing.</summary>
        public bool Writing { get; set; }

        /// <summary>The keyboard into the box, its words selected as a field's level opens (Karpo's), else the caret at their end.</summary>
        public void Take(bool selectAll)
        {
            Box.Focus(FocusState.Keyboard);
            if (Text is { } text)
            {
                if (selectAll)
                {
                    text.SelectAll();
                }
                else
                {
                    text.SelectionStart = text.Text.Length;
                }
            }
        }

#if DEBUG
        /// <summary>Debug renders only: type <paramref name="words"/> as the user would.</summary>
        public void Type(string words)
        {
            if (Text is { } text)
            {
                text.Text = words;
            }
            else
            {
                Password!.Password = words;
            }
        }
#endif
    }

    /// <summary>
    /// [S-3736] Ask for a line of text: the row whose click asks (or, asked with none in hand, the
    /// level shown now) opens a level holding the field, its name the row's.
    /// </summary>
    protected void AskText(MenuEdit edit)
    {
        ArgumentNullException.ThrowIfNull(edit);
        if (_root is null)
        {
            Log.Warn(Scope, "a line of text was asked with no menu shown; nothing was asked");
            return;
        }

        var anchor = _asking;
        _path.Add(new Step(anchor?.Key ?? "ask:" + edit.Title, anchor?.Text ?? edit.Title)
        {
            Edit = edit,
            Synthetic = anchor is null,
            Under = _scroll.VerticalOffset,
            Text = edit.Masked ? string.Empty : edit.Initial,

            // [S-3556] What Escape puts back in a password's form: what it held as the level opened, dropped as it closes.
            MaskedStart = edit.Masked ? edit.Fill?.Invoke() : null,
        });
        Draw(+1, null, 0, _askKeys ? FocusHow.Keys : FocusHow.Pointer);
        _askKeys = false;
    }

    /// <summary>
    /// [S-3737] Ask "are you sure?" on a level of the menu's own: the row whose click asks (or, asked
    /// with none in hand, an import's once Windows' dialog has gone, the level shown now) opens it.
    /// Only its action runs <see cref="MenuQuestion.Done"/>; Cancel, Escape or going back change nothing.
    /// </summary>
    protected void AskQuestion(MenuQuestion question)
    {
        ArgumentNullException.ThrowIfNull(question);
        if (_root is null)
        {
            Log.Warn(Scope, "a question was asked with no menu shown; nothing was done");
            return;
        }

        var anchor = _asking;
        _path.Add(new Step(anchor?.Key ?? "question:" + question.Accept, anchor?.Text ?? question.Accept)
        {
            Question = question,
            Synthetic = anchor is null,
            Under = _scroll.VerticalOffset,
        });

        // [S-3727] A dangerous question has the keyboard on Cancel, so Enter straight after keeps everything.
        Draw(+1, question.Danger ? "question/cancel" : "question/accept", 0, _askKeys ? FocusHow.Keys : FocusHow.Pointer);
        _askKeys = false;
    }

    /// <summary>[S-3736] A text level's rows: the field; the reason under it while what is typed cannot be given; the row that gives it.</summary>
    private List<MenuNode> EditNodes(Step step, MenuEdit edit)
    {
        var reason = edit.Reason(step.Text);
        var nodes = new List<MenuNode>
        {
            new("edit", MenuNodeKind.Field) { Text = step.Title, Edit = edit },
            new("edit/why", MenuNodeKind.Note) { Text = reason ?? string.Empty, Danger = true },
        };

        if (edit.Accept is { } accept)
        {
            nodes.Add(MenuNode.Separator("edit/-"));
            nodes.Add(new MenuNode("edit/accept") { Text = accept, Disabled = reason is not null, Icon = () => Glyph(Glyphs.Done), IconKey = "accept", Run = () => Give(step, edit) });
        }

        return nodes;
    }

    /// <summary>[S-3737] A question's rows: its words and what goes with it, then its action and Cancel.</summary>
    private List<MenuNode> QuestionNodes(MenuQuestion question) =>
    [
        new("question/text", MenuNodeKind.Note) { Text = question.Question, Strong = true },
        new("question/detail", MenuNodeKind.Note) { Text = question.Detail ?? string.Empty },
        MenuNode.Separator("question/-"),
        new("question/accept")
        {
            Text = question.Accept,
            Danger = question.Danger,
            IconKey = question.Danger ? "danger" : "accept",
            Icon = () => Glyph(question.Danger ? Glyphs.Delete : Glyphs.Done),
            Run = () =>
            {
                // The level goes first: what the answer changes is drawn over the level under it.
                Back(keys: false);
                Run(question.Done);
            },
        },
        new("question/cancel") { Text = Str.Common_Cancel.Text(), IconKey = "cancel", Icon = () => Glyph(Glyphs.Dismiss), Run = () => Back(keys: false) },
    ];

    /// <summary>
    /// [S-3736] The field, filling its row's 28-DIP fill (Windows 11's text box: the faint fill, the
    /// accent line while it has the keyboard, the clear button), its hint saying what to type. Each
    /// letter is kept on the level (a redraw keeps it) and handed to a form's field as it is typed;
    /// Enter gives it and goes back; Escape puts back what it held and goes back.
    /// </summary>
    private FieldView MakeField(MenuNode node)
    {
        var edit = node.Edit!;
        var step = _path[^1];
        TextBox? text = null;
        PasswordBox? password = null;
        Control box;
        if (edit.Masked)
        {
            password = new PasswordBox
            {
                PlaceholderText = edit.Placeholder,
                PasswordRevealMode = PasswordRevealMode.Hidden,
            };
            box = password;
        }
        else
        {
            text = new TextBox
            {
                Text = step.Text,
                PlaceholderText = edit.Placeholder,
                IsSpellCheckEnabled = false,
                IsTextPredictionEnabled = false,
            };

            if (edit.Numeric)
            {
                text.InputScope = new InputScope { Names = { new InputScopeName(InputScopeNameValue.Number) } };
            }

            box = text;
        }

        AutomationProperties.SetName(box, node.Text);
        box.HorizontalAlignment = HorizontalAlignment.Stretch;
        var view = new FieldView(BoxRow(box), text, password);

        if (password is not null)
        {
            // [S-3556] A password's box starts with what its form holds, never from a node.
            if (edit.Fill?.Invoke() is { Length: > 0 } held)
            {
                view.Writing = true;
                password.Password = held;
                view.Writing = false;
            }

            password.PasswordChanged += (_, _) =>
            {
                if (!view.Writing)
                {
                    Typed(step, edit, password.Password);
                }
            };
        }
        else
        {
            text!.TextChanged += (_, _) =>
            {
                if (!view.Writing)
                {
                    Typed(step, edit, text.Text);
                }
            };
        }

        box.KeyDown += (_, e) =>
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                e.Handled = true;
                Give(step, edit);
            }
            else if (e.Key == Windows.System.VirtualKey.Escape)
            {
                // Karpo's: the words go back to what they were, and so does the level.
                e.Handled = true;
                var back = edit.Masked ? step.MaskedStart ?? string.Empty : edit.Initial;
                if (!edit.Masked)
                {
                    step.Text = edit.Initial;
                }

                if (edit.Live is { } live)
                {
                    Run(() => live(back));
                }

                Back(keys: true);
            }
        };

        return view;
    }

    /// <summary>[S-3736] A letter typed: kept on the level, handed to a form's field, and the reason and the giving row written in place.</summary>
    private void Typed(Step step, MenuEdit edit, string typed)
    {
        if (!edit.Masked)
        {
            step.Text = typed;
        }

        if (edit.Live is { } live)
        {
            Run(() => live(typed));
        }

        var reason = edit.Reason(typed);
        foreach (var (at, note) in _shown.Notes)
        {
            if (_shown.Nodes[at].Key == "edit/why")
            {
                ApplyNote(note, _shown.Nodes[at] with { Text = reason ?? string.Empty });
            }
        }

        if (_shown.Views.FirstOrDefault(v => v?.Node.Key == "edit/accept") is { } accept)
        {
            accept.Button.IsEnabled = reason is null;
        }
    }

    /// <summary>[S-3736] Enter, or the giving row: what is typed is given where it can be, and the level goes back.</summary>
    private void Give(Step step, MenuEdit edit)
    {
        if (edit.Masked || edit.Live is not null)
        {
            // A form's field holds every letter already: Enter only goes back.
            Back(keys: true);
            return;
        }

        if (edit.Reason(step.Text) is not null)
        {
            return;
        }

        var text = step.Text.Trim();
        var changed = !string.Equals(text, edit.Initial.Trim(), StringComparison.Ordinal);

        // Given once: going back must not give it a second time.
        step.Text = edit.Initial;
        Back(keys: true);
        if (changed || edit.Accept is not null)
        {
            Run(() => edit.Commit(text));
        }
    }
}
