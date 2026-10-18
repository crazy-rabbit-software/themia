using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Windows.System;

namespace Themia.Sdk.WinUI.Kit;

/// <summary>[S-7063] One account offering what a widget needs, as its settings row shows it.</summary>
/// <param name="Description">Its provider and address, or the problem it has.</param>
public sealed record KitAccountRow(string Id, string Label, string Description);

/// <summary>[S-7063] The sentences that name the account kind: whole sentences per widget, never a noun glued in.</summary>
/// <param name="None">The row when no signed-in account offers it.</param>
/// <param name="Every">"Every calendar account".</param>
/// <param name="AddHint">Where accounts are added, under <paramref name="None"/>.</param>
public sealed record KitAccountTexts(string None, string Every, string AddHint);

/// <summary>[S-7063] One optional field's switch: its glyph, label and description, and the setting it writes.</summary>
public sealed record KitShowRow(string Glyph, string Label, string? Description, Func<bool> Get, Action<bool> Set);

/// <summary>
/// [S-7063] The standard settings rows of a kit widget, built only from <see cref="FluentLayout"/>
/// (so a restyle reaches them, and each carries its search entry): the accounts it reads, its free
/// inputs, how many items, and its optional fields. No refresh row ([S-3780]: the cadence is the
/// app's) and nothing the properties panel already sets ([S-6536]).
/// </summary>
public static class KitRows
{
    /// <summary>
    /// [S-7063] "Every account", or a choice of them. Every is the default and means accounts added
    /// later join too; turning it off starts from the accounts there are now, so nothing disappears;
    /// the last chosen account cannot be turned off, because an empty list means "every". With no
    /// account at all, one row that says so and opens Settings › Accounts.
    /// </summary>
    /// <param name="perAccount">Rows to show under an account while it is chosen (a calendar picker).</param>
    public static void Accounts(
        List<FrameworkElement> rows,
        string glyph,
        IReadOnlyList<KitAccountRow> accounts,
        KitAccountTexts texts,
        Func<List<string>> getIds,
        Action<List<string>> setIds,
        Action save,
        Action showAccounts,
        Func<KitAccountRow, FrameworkElement?>? perAccount = null)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(texts);
        ArgumentNullException.ThrowIfNull(getIds);
        ArgumentNullException.ThrowIfNull(setIds);
        ArgumentNullException.ThrowIfNull(save);
        ArgumentNullException.ThrowIfNull(showAccounts);

        var manage = new Button { Content = (accounts.Count == 0 ? Str.WidgetsA_Office_AddAccount : Str.WidgetsA_Office_ManageAccounts).Text() };
        manage.Click += (_, _) => showAccounts();

        if (accounts.Count == 0)
        {
            rows.Add(FluentLayout.Row(glyph, texts.None, texts.AddHint, manage));
            return;
        }

        var toggles = new List<ToggleSwitch>();
        var chosenRows = new List<FrameworkElement>();
        var extras = new List<(KitAccountRow Account, FrameworkElement Element)>();
        var quiet = false;

        bool Chosen(KitAccountRow account) => getIds().Count == 0 || getIds().Contains(account.Id);

        void ShowChoice()
        {
            foreach (var row in chosenRows)
            {
                row.Visibility = getIds().Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            }

            foreach (var (account, element) in extras)
            {
                element.Visibility = Chosen(account) ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        rows.Add(FluentLayout.Row(glyph, texts.Every, Str.WidgetsA_Office_EveryDesc.Text(),
            FluentLayout.Toggle(getIds().Count == 0, on =>
            {
                setIds(on ? [] : [.. accounts.Select(a => a.Id)]);

                quiet = true;
                foreach (var toggle in toggles)
                {
                    toggle.IsOn = true;
                }

                quiet = false;
                save();
                ShowChoice();
            })));

        foreach (var account in accounts)
        {
            var index = toggles.Count;
            var toggle = FluentLayout.Toggle(Chosen(account), on =>
            {
                if (quiet)
                {
                    return;
                }

                var ids = (getIds().Count == 0 ? accounts.Select(a => a.Id) : getIds()).ToList();
                ids.Remove(account.Id);
                if (on)
                {
                    ids.Add(account.Id);
                }

                if (ids.Count == 0)
                {
                    quiet = true;
                    toggles[index].IsOn = true;
                    quiet = false;
                    return;
                }

                setIds([.. accounts.Select(a => a.Id).Where(ids.Contains)]);
                save();
                ShowChoice();
            });

            toggles.Add(toggle);

            var row = FluentLayout.Row(glyph, account.Label, account.Description, toggle);
            chosenRows.Add(row);
            rows.Add(row);

            if (perAccount?.Invoke(account) is { } extra)
            {
                extras.Add((account, extra));
                rows.Add(extra);
            }
        }

        rows.Add(FluentLayout.Row(Glyphs.Contact, Str.WidgetUi_AccountsLabel.Text(), Str.WidgetsA_Office_ManageDesc.Text(), manage));
        ShowChoice();
    }

    /// <summary>
    /// [S-7063] A free input (a feed address, a repository) for a list editor's entry or a row of its
    /// own: checked on commit (Enter, or leaving the box) by the widget's own validator, its verdict
    /// under the box. A refused value is not written and never repaired into something that works;
    /// an empty one stays as a blank to fill in.
    /// </summary>
    /// <param name="refusal">The validator: why a typed value is refused, or null to accept it.</param>
    public static FrameworkElement InputBox(string value, Func<string, string?> refusal, Action<string> commit, string placeholder, int maxLength)
    {
        ArgumentNullException.ThrowIfNull(refusal);
        ArgumentNullException.ThrowIfNull(commit);

        var box = new TextBox
        {
            Text = value,
            MinWidth = 260,
            MaxLength = maxLength,
            PlaceholderText = placeholder,
        };

        var verdict = FluentLayout.Verdict();
        var held = value;

        void Commit()
        {
            var typed = box.Text.Trim();

            if (typed == held)
            {
                return;
            }

            // Refused here, where the user can see why, rather than accepted and then refused again
            // silently on every tick. Never repaired.
            if (typed.Length > 0 && refusal(typed) is { } why)
            {
                FluentLayout.Refuse(verdict, why);
                return;
            }

            FluentLayout.Confirm(verdict, string.Empty);
            held = typed;
            commit(typed);
        }

        // On commit, not per keystroke: a value is only valid once it has been fully typed.
        box.KeyDown += (_, args) =>
        {
            if (args.Key == VirtualKey.Enter)
            {
                args.Handled = true;
                Commit();
            }
        };

        box.LostFocus += (_, _) => Commit();

        return new StackPanel { Spacing = Fluent.Space1, Children = { box, verdict } };
    }

    /// <summary>[S-7063] How many items: a slider within the widget's bounds.</summary>
    public static void Count(List<FrameworkElement> rows, string glyph, string label, string? description, int value, int min, int max, Action<int> set)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(set);

        rows.Add(FluentLayout.Row(glyph, label, description,
            FluentLayout.Slider(Math.Clamp(value, min, max), min, max, 1, string.Empty, v => set((int)Math.Round(v)))));
    }

    /// <summary>[S-7063] One switch per optional field the widget offers ([S-6070]: only fields the current style draws).</summary>
    public static void Shows(List<FrameworkElement> rows, IReadOnlyList<KitShowRow> fields, Action save)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(fields);
        ArgumentNullException.ThrowIfNull(save);

        foreach (var field in fields)
        {
            rows.Add(FluentLayout.Row(field.Glyph, field.Label, field.Description,
                FluentLayout.Toggle(field.Get(), value => { field.Set(value); save(); })));
        }
    }
}
