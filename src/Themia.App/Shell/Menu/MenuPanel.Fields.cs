using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Themia.App.Widgets;
using Themia.Core.Widgets;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell;

/// <summary>
/// [S-3561] The lines a menu is typed into (a form's fields) and the notes written in place, so a
/// form keeps the keyboard while it is typed in. [S-3556] A password's field is Windows' own
/// <see cref="PasswordBox"/>: a bullet for each letter, no copy or cut (paste works), no reveal
/// button, the IME off while it has the keyboard, and to UI Automation an edit that IsPassword and
/// exposes no value. What it holds is never in the menu's nodes, a tooltip or the log, and is wiped
/// as its submenu closes.
/// </summary>
internal partial class MenuPanel
{
    /// <summary>[S-3556] The levels are being closed only to be opened again (a rebuild): their fields keep what they hold.</summary>
    private bool _reopening;

    /// <summary>[S-3561] A drawn note: the parts its node is written into.</summary>
    private sealed class NoteView(Border root, TextBlock words, ProgressRing ring, bool icons)
    {
        public Border Root { get; } = root;

        public TextBlock Words { get; } = words;

        public ProgressRing Ring { get; } = ring;

        /// <summary>Its level has an icon column: its words start where a line's do.</summary>
        public bool Icons { get; } = icons;
    }

    /// <summary>[S-3561] A drawn field: its node now, its words and its box (a text box or, masked, a password box).</summary>
    private sealed class FieldView(MenuNode node, FrameworkElement root, TextBlock label, TextBox? text, PasswordBox? password)
    {
        public MenuNode Node { get; set; } = node;

        public FrameworkElement Root { get; } = root;

        public TextBlock Label { get; } = label;

        public TextBox? Text { get; } = text;

        public PasswordBox? Password { get; } = password;

        public Control Box => (Control?)Text ?? Password!;

        /// <summary>The box is being written from the node: not the user's typing.</summary>
        public bool Writing { get; set; }
    }

    /// <summary>
    /// [S-3561] A field's line: its words in the caption step over the box, the box the menu's width
    /// less the lines' inset. Each letter typed is handed on at once; Enter moves on to the next line,
    /// as Tab does. [S-3556] A masked field starts with what its form still holds (a level drawn
    /// again keeps the password typed), never from its node.
    /// </summary>
    private FieldView MakeField(MenuNode node)
    {
        var rule = node.Field!;
        var label = Fluent.Text(FluentType.Caption, "TextFillColorSecondaryBrush");
        label.Text = node.Text;

        TextBox? text = null;
        PasswordBox? password = null;
        Control box;
        if (rule.Masked)
        {
            password = new PasswordBox
            {
                PlaceholderText = rule.Placeholder,
                PasswordRevealMode = PasswordRevealMode.Hidden,
                MinHeight = LineHeight,
            };
            box = password;
        }
        else
        {
            text = new TextBox
            {
                Text = node.Value ?? string.Empty,
                PlaceholderText = rule.Placeholder,
                IsSpellCheckEnabled = false,
                IsTextPredictionEnabled = false,
                MinHeight = LineHeight,
            };

            if (rule.Numeric)
            {
                text.InputScope = new InputScope { Names = { new InputScopeName(InputScopeNameValue.Number) } };
            }

            box = text;
        }

        AutomationProperties.SetName(box, node.Text);
        box.HorizontalAlignment = HorizontalAlignment.Stretch;
        box.IsEnabled = !node.Disabled;

        var root = new StackPanel
        {
            Spacing = Fluent.Space1,
            Margin = new Thickness(Fluent.Space1 + Fluent.Space3, Fluent.Space1, Fluent.Space1 + Fluent.Space3, Fluent.Space1),
            Children = { label, box },
        };

        var view = new FieldView(node, root, label, text, password);

        if (password is not null)
        {
            if (rule.HasValue && rule.Fill?.Invoke() is { Length: > 0 } held)
            {
                view.Writing = true;
                password.Password = held;
                view.Writing = false;
            }

            password.PasswordChanged += (_, _) =>
            {
                if (!view.Writing)
                {
                    Run(() => view.Node.Field!.Set(password.Password));
                }
            };
        }
        else
        {
            text!.TextChanged += (_, _) =>
            {
                if (!view.Writing)
                {
                    Run(() => view.Node.Field!.Set(text.Text));
                }
            };
        }

        box.KeyDown += (_, e) =>
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                e.Handled = FocusManager.TryMoveFocus(FocusNavigationDirection.Next, new FindNextElementOptions { SearchRoot = XamlRoot?.Content });
            }
        };

        return view;
    }

    /// <summary>
    /// [S-3561] Write a field's node in: its words, its hint, whether it is greyed, and what it holds
    /// where that changed from outside (a form emptied once its account was added), never under the
    /// caret of the box being typed in. [S-3556] A masked box is only ever emptied from here.
    /// </summary>
    private static void ApplyField(FieldView view, MenuNode node)
    {
        view.Node = node;
        var rule = node.Field!;
        view.Label.Text = node.Text;
        view.Box.IsEnabled = !node.Disabled;
        AutomationProperties.SetName(view.Box, node.Text);

        if (view.Text is { } text)
        {
            text.PlaceholderText = rule.Placeholder;
            var value = node.Value ?? string.Empty;
            if (text.FocusState == FocusState.Unfocused && !string.Equals(text.Text.Trim(), value, StringComparison.Ordinal))
            {
                view.Writing = true;
                text.Text = value;
                view.Writing = false;
            }
        }
        else if (view.Password is { } password)
        {
            password.PlaceholderText = rule.Placeholder;
            if (!rule.HasValue && password.Password.Length > 0)
            {
                view.Writing = true;
                password.Password = string.Empty;
                view.Writing = false;
            }
        }
    }

    /// <summary>The key of the field in <paramref name="level"/> that has the keyboard, or null.</summary>
    private static string? FocusedField(Level level)
    {
        foreach (var field in level.Fields.Values)
        {
            if (field.Box.FocusState != FocusState.Unfocused)
            {
                return field.Node.Key;
            }
        }

        return null;
    }

    /// <summary>
    /// [S-3561] The field of <paramref name="key"/>, drawn again, takes the keyboard back, the caret at
    /// its end: once it stands in the window (it is drawn before it is put there).
    /// </summary>
    private void Refocus(Level level, string? key)
    {
        if (key is null || level.Fields.Values.FirstOrDefault(f => f.Node.Key == key) is not { } field)
        {
            return;
        }

        void Take()
        {
            field.Box.Focus(FocusState.Keyboard);
            if (field.Text is { } text)
            {
                text.SelectionStart = text.Text.Length;
            }
        }

        if (DispatcherQueue is not { } queue || !queue.TryEnqueue(() => Run(Take)))
        {
            Themia.Core.Diagnostics.Log.Debug(Scope, "a field drawn again could not take the keyboard back");
        }
    }

    /// <summary>
    /// [S-3556] [S-3557] A level closes: what its masked fields hold is wiped, from the box and from
    /// the form behind it. Not when the level is only being opened again (a rebuild).
    /// </summary>
    private void CloseFields(Level level)
    {
        if (_reopening)
        {
            return;
        }

        foreach (var field in level.Fields.Values)
        {
            if (field.Password is { } password)
            {
                field.Writing = true;
                password.Password = string.Empty;
                field.Writing = false;
                Run(field.Node.Field!.Wipe);
            }
        }
    }
}
