using Themia.App.Shell;
using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;

namespace Themia.Sdk.WinUI.Kit;

/// <summary>[S-7063] One signed-in account offering what a widget needs, as its menu line shows it.</summary>
/// <param name="Detail">Its provider, or the problem it has: the words at the end of its line.</param>
public sealed record KitAccountLine(string Id, string Label, string? Detail);

/// <summary>[S-7063] The sentences that name the account kind: whole sentences per widget, never a noun glued in.</summary>
/// <param name="None">What the accounts submenu says while no signed-in account offers it ("No calendar account yet").</param>
/// <param name="Every">"Every calendar account".</param>
public sealed record KitAccountWords(string None, string Every);

/// <summary>[S-7063] One optional field's switch: its words and the setting it writes.</summary>
public sealed record KitShowLine(string Label, Func<bool> Get, Action<bool> Set);

/// <summary>
/// [S-7063] The standard lines of a kit widget on its properties menu ([S-3719]), built only from
/// the menu's vocabulary (<see cref="WidgetMenuLines"/>): the accounts it reads, how many items, and
/// its optional fields. No refresh line ([S-3780]: the cadence is the app's). A free input is the
/// menu's own ask ([S-3721]) or list ([S-3729]), checked by the widget's validator as it is typed.
/// </summary>
public static class KitMenuLines
{
    /// <summary>
    /// [S-7063] The Accounts submenu: "Every account" (on by default: accounts added later join too),
    /// then a switch per signed-in account; turning Every off starts from the accounts there are now,
    /// so nothing disappears, and the last chosen account cannot be turned off (it is greyed), because
    /// an empty list means "every". Then Manage accounts, which opens ⚙ › Accounts. With no
    /// account at all: the widget's sentence and Add an account.
    /// </summary>
    public static ItemMenuEntry Accounts(
        WidgetMenuLines menu,
        string? glyph,
        IReadOnlyList<KitAccountLine> accounts,
        KitAccountWords words,
        IReadOnlyList<string> chosenIds,
        Action<List<string>> setIds)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(words);
        ArgumentNullException.ThrowIfNull(chosenIds);
        ArgumentNullException.ThrowIfNull(setIds);

        var label = Str.WidgetUi_AccountsLabel.Text();
        if (accounts.Count == 0)
        {
            return WidgetMenuLines.Submenu(glyph, label,
            [
                WidgetMenuLines.Note(words.None),
                ItemMenuEntry.Separator,
                menu.AccountsLine(Glyphs.Add, Str.WidgetsA_Office_AddAccount.Text()),
            ]);
        }

        var every = chosenIds.Count == 0;
        var all = accounts.Select(a => a.Id).ToList();
        var chosen = every ? all : all.Where(chosenIds.Contains).ToList();

        var lines = new List<ItemMenuEntry>(accounts.Count + 4)
        {
            menu.Toggle(null, words.Every, every, on => setIds(on ? [] : [.. all])),
            ItemMenuEntry.Separator,
        };

        foreach (var account in accounts)
        {
            var on = chosen.Contains(account.Id);
            lines.Add(menu.Toggle(null, account.Label, on, _ => setIds(Toggled(all, chosen, account.Id))) with
            {
                Value = account.Detail,

                // The last one stays: an empty choice would mean every account.
                Disabled = on && chosen.Count == 1,
            });
        }

        lines.Add(ItemMenuEntry.Separator);
        lines.Add(menu.AccountsLine(Glyphs.Contact, Str.WidgetsA_Office_ManageAccounts.Text()));

        // The line says what holds: every account, or the one chosen, or how many.
        var value = every ? null : chosen.Count == 1 ? accounts.First(a => a.Id == chosen[0]).Label : LocaleFormat.Integer(chosen.Count);
        return WidgetMenuLines.Submenu(glyph, label, lines) with { Value = value };
    }

    /// <summary>
    /// [S-7063] The choice after one account's switch is clicked: <paramref name="chosen"/> without it,
    /// or with it, in the accounts' order. Never empty: the last one is never taken away.
    /// </summary>
    public static List<string> Toggled(IReadOnlyList<string> all, IReadOnlyList<string> chosen, string id)
    {
        ArgumentNullException.ThrowIfNull(all);
        ArgumentNullException.ThrowIfNull(chosen);

        var next = chosen.Contains(id) ? chosen.Where(c => c != id).ToList() : [.. chosen, id];
        return next.Count == 0 ? [.. chosen] : [.. all.Where(next.Contains)];
    }

    /// <summary>[S-7063] How many items: a number within the widget's bounds ([S-3726]).</summary>
    public static ItemMenuEntry Count(WidgetMenuLines menu, string? glyph, string label, int value, int min, int max, int step, Action<int> set)
    {
        ArgumentNullException.ThrowIfNull(menu);
        return menu.Number(glyph, label, value, min, max, step, v => LocaleFormat.Integer(v), set);
    }

    /// <summary>
    /// [S-2440] A list that states its rows ([S-2440]): how many rows it shows (1 to 20; dragging its
    /// bottom edge sets the same) and, where its rows can have a second line, how many lines each has.
    /// </summary>
    public static IEnumerable<ItemMenuEntry> Rows(WidgetMenuLines menu, IKitRows config, bool offersLines = true)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);

        yield return Count(menu, null, Str.Kit_Rows.Text(), KitSizing.ClampRows(config.Rows), KitSizing.MinRows, KitSizing.MaxRows, 1, v => config.Rows = v);
        if (offersLines)
        {
            yield return menu.Choice(
                null,
                Str.Kit_Lines.Text(),
                [(1, Str.Kit_LinesOne.Text()), (2, Str.Kit_LinesTwo.Text())],
                KitSizing.ClampLines(config.Lines),
                v => config.Lines = v);
        }
    }

    /// <summary>
    /// [S-2449] A one-readout widget's Size: Small, Medium or Large (the family's 40, 68, 96); its card
    /// follows the size, never the other way round.
    /// </summary>
    public static ItemMenuEntry ReadoutSize(WidgetMenuLines menu, IReadoutSized config)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(config);
        return menu.Choice(
            null,
            Str.Kit_Size.Text(),
            [
                (Themia.Core.Widgets.ReadoutSize.Small, Str.Kit_SizeSmall.Text()),
                (Themia.Core.Widgets.ReadoutSize.Medium, Str.Kit_SizeMedium.Text()),
                (Themia.Core.Widgets.ReadoutSize.Large, Str.Kit_SizeLarge.Text()),
            ],
            ReadoutSizes.Normalise(config.Size),
            v => config.Size = v);
    }

    /// <summary>[S-7063] One switch per optional field the widget offers ([S-6070]: only fields the current style draws).</summary>
    public static IEnumerable<ItemMenuEntry> Shows(WidgetMenuLines menu, IReadOnlyList<KitShowLine> fields)
    {
        ArgumentNullException.ThrowIfNull(menu);
        ArgumentNullException.ThrowIfNull(fields);
        return fields.Select(field => menu.Toggle(null, field.Label, field.Get(), field.Set));
    }
}
