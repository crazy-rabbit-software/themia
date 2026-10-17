using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Services;
using Themia.App.Services.Tasks;
using Themia.App.Widgets;
using Themia.Core.Accounts;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Tasks;
using Themia.Core.Widgets;

namespace Themia.App.Shell.Config;

/// <summary>
/// [S-592]/[S-604] The Tasks widget's settings: where the list comes from — an account's list, its
/// Today view, or a list kept on this PC — and how rows are drawn.
/// <para>
/// Accounts are signed in on the Accounts page, not here: this page lists the accounts that can hold
/// tasks and links there. Lists on this PC, by contrast, are made, renamed and deleted right here,
/// because this page is the only place they exist.
/// </para>
/// </summary>
internal static partial class WidgetSettings
{
    private const string TasksScope = "tasks-settings";

    public static void Tasks(List<FrameworkElement> rows, TasksConfig config, Action save, Action showAccounts)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(save);
        ArgumentNullException.ThrowIfNull(showAccounts);

        var service = AppServices.Tasks;

        rows.Add(FluentLayout.Header(Str.WidgetsC_Tasks_Settings_Source.Text()));

        // The rows under the source change with it, so they live in one host rebuilt in place.
        var sourceRows = new StackPanel { Spacing = Fluent.Space1 };
        rows.Add(sourceRows);

        var filterRows = new StackPanel { Spacing = Fluent.Space1 };

        void Rebuild()
        {
            sourceRows.Children.Clear();

            foreach (var row in TaskSourceRows(service, config, save, showAccounts, Rebuild))
            {
                sourceRows.Children.Add(row);
            }

            filterRows.Children.Clear();

            foreach (var row in TaskAccountFilterRows(config, save))
            {
                filterRows.Children.Add(row);
            }
        }

        rows.Add(FluentLayout.Header(Str.ConfigTabs_Display.Text()));

        rows.Add(FluentLayout.Row(Glyphs.Done, Str.Todo_ShowCompleted_Label.Text(), Str.WidgetsC_Tasks_Settings_ShowCompleted_Desc.Text(),
            FluentLayout.Toggle(config.ShowCompleted, value => { config.ShowCompleted = value; save(); })));

        // [S-6074] Due dates exist only on an account's tasks.
        if (config.Offers(TasksSetting.DueDate))
        {
            rows.Add(FluentLayout.Row(Glyphs.Calendar, Str.WidgetsC_Tasks_Settings_ShowDue_Label.Text(), Str.WidgetsC_Tasks_Settings_ShowDue_Desc.Text(),
                FluentLayout.Toggle(config.ShowDueDate, value => { config.ShowDueDate = value; save(); })));
        }

        // [S-6400] List or Compact is the widget's style, picked on the properties panel ([S-6536]).

        rows.Add(FluentLayout.Row(Glyphs.Wrap, Str.Todo_TitleLines_Label.Text(), Str.WidgetsC_Tasks_Settings_TitleLines_Desc.Text(),
            FluentLayout.Slider(config.EffectiveTitleLines, TaskRules.TitleLinesMin, TaskRules.TitleLinesMax, 1, string.Empty,
                value => { config.TitleLines = (int)Math.Round(value); save(); })));

        // [S-610] Where a new task lands; the stored order ([S-598]) is kept either way.
        rows.Add(FluentLayout.Row(Glyphs.Sort, Str.WidgetsC_Tasks_Settings_NewOnTop_Label.Text(), Str.WidgetsC_Tasks_Settings_NewOnTop_Desc.Text(),
            FluentLayout.Toggle(config.NewOnTop, value => { config.NewOnTop = value; save(); })));

        // [S-611] Filters. Due dates and importance exist only on an account's tasks, so the rows that
        // filter by them follow the source (rebuilt with it).
        rows.Add(FluentLayout.Header(Str.WidgetsC_Tasks_Settings_Filter.Text()));
        rows.Add(filterRows);
        rows.Add(TaskTextFilterRow(config, save));

        Rebuild();
    }

    /// <summary>[S-611] The due and importance filters: an account's tasks only.</summary>
    private static IEnumerable<FrameworkElement> TaskAccountFilterRows(TasksConfig config, Action save)
    {
        if (config.Offers(TasksSetting.DueFilter))
        {
            yield return DueFilterRow(config, save);
        }

        if (config.Offers(TasksSetting.ImportantOnly))
        {
            yield return FluentLayout.Row(Glyphs.Important, Str.WidgetsC_Tasks_Settings_ImportantOnly_Label.Text(), Str.WidgetsC_Tasks_Settings_ImportantOnly_Desc.Text(),
                FluentLayout.Toggle(config.ImportantOnly, value => { config.ImportantOnly = value; save(); }));
        }
    }

    private static Border DueFilterRow(TasksConfig config, Action save)
        => FluentLayout.Row(Glyphs.Calendar, Str.WidgetsC_Tasks_Settings_DueFilter_Label.Text(), Str.WidgetsC_Tasks_Settings_DueFilter_Desc.Text(),
            FluentLayout.Choice<TaskDueFilter>(
                [
                    (TaskDueFilter.Any, Str.WidgetsC_Tasks_Settings_DueFilter_Any.Text()),
                    (TaskDueFilter.Overdue, Str.WidgetsC_Tasks_Overdue.Text()),
                    (TaskDueFilter.Today, Str.Common_Today.Text()),
                    (TaskDueFilter.ThisWeek, Str.WidgetsC_Tasks_Settings_DueFilter_ThisWeek.Text()),
                    (TaskDueFilter.NoDate, Str.WidgetsC_Tasks_Settings_DueFilter_NoDate.Text()),
                ],
                config.DueFilter,
                value => { config.DueFilter = value; save(); }));

    /// <summary>[S-611] Words the titles must hold. Written on Enter and on leaving the box, never per keystroke.</summary>
    private static Border TaskTextFilterRow(TasksConfig config, Action save)
    {
        var box = new TextBox
        {
            Text = config.TextFilter,
            PlaceholderText = Str.WidgetsC_Tasks_Settings_TextFilter_Placeholder.Text(),
            MaxLength = TasksConfig.TextFilterMax,
            MinWidth = 200,
        };

        void Commit()
        {
            var text = box.Text.Trim();
            if (text != config.TextFilter)
            {
                config.TextFilter = text;
                save();
            }
        }

        box.LostFocus += (_, _) => Commit();
        box.KeyDown += (_, e) =>
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                e.Handled = true;
                Commit();
            }
        };

        return FluentLayout.Row(Glyphs.Search, Str.WidgetsC_Tasks_Settings_TextFilter_Label.Text(), Str.WidgetsC_Tasks_Settings_TextFilter_Desc.Text(), box);
    }

    /// <summary>The source picker, then what that source offers: an account's lists, or this PC's.</summary>
    private static IEnumerable<FrameworkElement> TaskSourceRows(
        TaskService? service,
        TasksConfig config,
        Action save,
        Action showAccounts,
        Action rebuild)
    {
        if (service is null)
        {
            yield return FluentLayout.Note(Str.WidgetsC_Tasks_Settings_NotStarted.Text());
            yield break;
        }

        var accounts = service.Accounts;

        // "Automatic" first: the first account with tasks, now or later. Then each account, then this PC.
        var combo = new ComboBox { MinWidth = 260 };
        combo.Items.Add(new ComboBoxItem { Content = accounts.Count > 0 ? Str.WidgetsC_Tasks_Settings_FirstAccountNamed.Format(("account", accounts[0].Label)) : Str.WidgetsC_Tasks_Settings_FirstAccount.Text(), Tag = string.Empty });

        foreach (var account in accounts)
        {
            combo.Items.Add(new ComboBoxItem
            {
                Content = account.Problem is null ? $"{account.Label} — {account.Provider.Label()}" : Str.WidgetsC_OneDrive_Settings_AccountSignInAgain.Format(("account", account.Label)),
                Tag = account.Id,
            });
        }

        combo.Items.Add(new ComboBoxItem { Content = Str.WidgetsC_Tasks_Settings_ThisPc.Text(), Tag = LocalTag });

        combo.SelectedIndex = config.Source == TaskSource.Local
            ? combo.Items.Count - 1
            : Math.Max(0, IndexOfTag(combo, config.AccountId));

        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedItem is not ComboBoxItem { Tag: string tag })
            {
                return;
            }

            if (tag == LocalTag)
            {
                if (config.Source == TaskSource.Local)
                {
                    return;
                }

                // [S-604] The list it had if that still exists, else the first, else a new one.
                config.Source = TaskSource.Local;
                config.LocalListId = service.Local.Ensure(config.LocalListId, Str.Todo_LocalLists_DefaultName.Text());
            }
            else
            {
                if (config.Source == TaskSource.Account && config.AccountId == tag)
                {
                    return;
                }

                // A different account's lists, and a different list's order.
                config.Source = TaskSource.Account;
                config.AccountId = tag;
                config.ListId = string.Empty;
                config.ListName = string.Empty;
                config.TaskOrder = [];
            }

            save();
            rebuild();
        };

        yield return FluentLayout.Row(Glyphs.Contact, Str.WidgetsC_Tasks_Settings_ShowFrom_Label.Text(),
            (accounts.Count == 0 ? Str.WidgetsC_Tasks_Settings_NoAccount : Str.WidgetsC_Tasks_Settings_ShowFrom_Desc).Text(),
            combo);

        if (accounts.Count == 0 || accounts.Any(a => a.Problem is not null))
        {
            yield return FluentLayout.NavRow(Glyphs.Contact, Str.WidgetUi_AccountsLabel.Text(),
                (accounts.Count == 0 ? Str.WidgetsC_Tasks_Settings_SignIn : Str.WidgetsC_OneDrive_Settings_AccountNeedsSignIn).Text(),
                showAccounts);
        }

        if (config.Source == TaskSource.Local)
        {
            foreach (var row in LocalListRows(service.Local, config, save, rebuild))
            {
                yield return row;
            }

            yield break;
        }

        var resolved = string.IsNullOrEmpty(config.AccountId) ? accounts.FirstOrDefault() : service.Find(config.AccountId);
        if (resolved is not null)
        {
            yield return AccountListRow(service, resolved, config, save);
        }
    }

    private const string LocalTag = "\u0001local";

    /// <summary>
    /// [S-592]/[S-596] The account's lists: its default first (what an unchosen widget shows), then
    /// Today — every list's tasks due today or overdue — then the lists themselves. Read once and
    /// shared with the widget through the service's cache.
    /// </summary>
    private static Border AccountListRow(TaskService service, TaskAccount account, TasksConfig config, Action save)
    {
        var combo = new ComboBox { MinWidth = 260, IsEnabled = false, PlaceholderText = Str.Todo_LoadingLists.Text() };
        var row = FluentLayout.Row(Glyphs.Task, Str.WidgetsC_Tasks_Settings_List_Label.Text(), Str.WidgetsC_Tasks_Settings_List_From.Format(("account", account.Label)), combo);

        _ = FillAsync();
        return row;

        async Task FillAsync()
        {
            IReadOnlyList<TaskListInfo> lists;
            try
            {
                lists = await service.ListsAsync(account.Id).ConfigureAwait(true);
            }
            catch (AccountException ex)
            {
                Log.Warn(TasksScope, $"could not read the lists of {account.Id}", ex);
                combo.PlaceholderText = (ex.Error == AccountError.SignInRequired ? Str.WidgetsC_Tasks_Settings_List_SignInAgain : Str.WidgetsC_Tasks_Settings_List_Failed).Text();
                return;
            }

            var fallback = TaskRules.DefaultList(lists);
            combo.Items.Add(new ComboBoxItem { Content = fallback is null ? Str.WidgetsC_Tasks_Settings_List_Default.Text() : Str.WidgetsC_Tasks_Settings_List_DefaultNamed.Format(("name", fallback.Name)), Tag = string.Empty });
            combo.Items.Add(new ComboBoxItem { Content = Str.WidgetsC_Tasks_Settings_List_Today.Text(), Tag = TaskRules.TodayListId });

            foreach (var list in lists)
            {
                combo.Items.Add(new ComboBoxItem { Content = string.IsNullOrWhiteSpace(list.Name) ? Str.WidgetsC_Tasks_Settings_List_Unnamed.Text() : list.Name, Tag = list.Id });
            }

            // A list that has gone since it was picked is not in the box; it falls back to the
            // default entry rather than sitting blank on a setting that means something.
            combo.SelectedIndex = Math.Max(0, IndexOfTag(combo, config.ListId));
            combo.IsEnabled = true;

            combo.SelectionChanged += (_, _) =>
            {
                if (combo.SelectedItem is not ComboBoxItem { Tag: string id } || id == config.ListId)
                {
                    return;
                }

                config.ListId = id;
                config.ListName = lists.FirstOrDefault(l => l.Id == id)?.Name ?? string.Empty;
                config.TaskOrder = [];
                save();
            };
        }
    }

    /// <summary>
    /// [S-604] The lists kept on this PC: pick one, rename it as you type, add one, delete one after
    /// asking. A pool every Tasks widget shares, so renaming here retitles each widget showing it.
    /// </summary>
    private static IEnumerable<FrameworkElement> LocalListRows(LocalTaskPool pool, TasksConfig config, Action save, Action rebuild)
    {
        if (pool.Problem is { } problem)
        {
            yield return FluentLayout.Note(problem);
        }

        foreach (var list in pool.Lists)
        {
            var pick = new RadioButton
            {
                GroupName = "tasks-local-lists",
                IsChecked = list.Id == config.LocalListId,
                MinWidth = 0,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var listId = list.Id;
            pick.Checked += (_, _) =>
            {
                if (config.LocalListId != listId)
                {
                    config.LocalListId = listId;
                    save();
                }
            };

            var name = new TextBox
            {
                Text = list.Name,
                PlaceholderText = Str.Todo_LocalLists_NamePlaceholder.Text(),
                MinWidth = 160,
                VerticalAlignment = VerticalAlignment.Center,
                IsEnabled = pool.Writable,
            };

            // Written through as it is typed; a blank name is kept so the box can be cleared.
            name.TextChanged += (_, _) => pool.Rename(listId, name.Text);

            var count = Fluent.Text(Themia.Core.Widgets.FluentType.Caption, "TextFillColorTertiaryBrush");
            count.Text = Str.WidgetsC_Tasks_Settings_TaskCount.Plural(list.Tasks.Count);
            count.VerticalAlignment = VerticalAlignment.Center;

            var remove = FluentLayout.IconCommand(Glyphs.Delete, Str.WidgetsC_Tasks_Settings_DeleteList.Text());
            remove.IsEnabled = pool.Writable;
            remove.Click += async (_, _) =>
            {
                try
                {
                    // [S-604] Always asked, empty or not: it takes every task in it, and the button
                    // sits one row from the name being typed in.
                    var detail = list.Tasks.Count == 0
                        ? Str.WidgetsC_Tasks_Settings_DeleteListEmpty.Format(("name", Display(list.Name)))
                        : Str.WidgetsC_Tasks_Settings_DeleteListWithTasks.Plural(list.Tasks.Count, ("name", Display(list.Name)));

                    if (!await FluentLayout.ConfirmAsync(remove, Str.WidgetsC_Tasks_Settings_DeleteListConfirm.Text(), detail, Str.Common_Delete.Text()).ConfigureAwait(true))
                    {
                        return;
                    }

                    pool.Delete(listId);

                    // The widget showing it says "pick or start a list" rather than being handed
                    // another list's tasks.
                    if (config.LocalListId == listId)
                    {
                        config.LocalListId = string.Empty;
                        save();
                    }

                    rebuild();
                }
                catch (Exception ex)
                {
                    Log.Error(TasksScope, "could not delete the list", ex);
                }
            };

            var line = new Grid
            {
                ColumnSpacing = Fluent.Space2,
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Auto },
                },
            };

            Grid.SetColumn(name, 1);
            Grid.SetColumn(count, 2);
            Grid.SetColumn(remove, 3);
            line.Children.Add(pick);
            line.Children.Add(name);
            line.Children.Add(count);
            line.Children.Add(remove);

            yield return FluentLayout.Card(line);
        }

        if (pool.Lists.Count == 0)
        {
            yield return FluentLayout.Note(Str.WidgetsC_Tasks_Settings_NoLocalLists.Text());
        }

        var add = new Button { Content = Str.WidgetsC_Tasks_Settings_AddList.Text(), IsEnabled = pool.Writable };
        add.Click += (_, _) =>
        {
            config.LocalListId = pool.Create(LocalTaskLists.NewListName);
            save();
            rebuild();
        };

        yield return add;
    }

    private static string Display(string name) => string.IsNullOrWhiteSpace(name) ? Str.WidgetsC_Tasks_UntitledList.Text() : name;

    private static int IndexOfTag(ComboBox combo, string? tag)
    {
        for (var i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is ComboBoxItem { Tag: string value } && value == (tag ?? string.Empty))
            {
                return i;
            }
        }

        return -1;
    }
}
