using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Tasks;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;
using Themia.Widget.Tasks;

namespace Themia.App.Widgets;

/// <summary>
/// [S-616] The rows are the kit's check look ([S-7037], [S-7038]): this is what Tasks itself does with
/// them — a task as a kit item, its actions, and the writes the list hands back (a tick, a rename, a
/// new task, a delete, a new order).
/// </summary>
internal sealed partial class TasksView
{
    /// <summary>The rows as drawn, in order: what an order is measured against and what a list action names.</summary>
    private IReadOnlyList<TaskItem> _displayed = [];

    /// <summary>The plan the rows on screen were drawn for; a new size that changes it redraws them.</summary>
    private KitPlan? _drawnPlan;

    private KitList NewList() => new(_widgetHost, TaskSpec.Spec, ActionsOf, Run)
    {
        Renamed = OnRenamed,
        Added = text => _ = AddAsync(text),
        Reordered = CommitOrder,
        TextInput = _ => UpdateKeyboard(),
    };

    /// <summary>[S-616] The kit items for the rows, in order: the done ones under their heading, Today's with their lists.</summary>
    private List<KitItem> Items(IReadOnlyList<TaskItem> rows, DateOnly today)
    {
        var done = rows.Count(t => t.Completed);
        var listNames = _feed?.IsToday == true && !IsLocal;
        return [.. rows.Select(t => TaskSpec.Item(t, today, _config.ShowDueDate, listNames && _listNames.TryGetValue(t.ListId, out var name) ? name : null, done))];
    }

    private TaskItem? TaskOf(KitItem item) => _displayed.FirstOrDefault(t => t.Id == item.Id);

    /// <summary>[S-616] A row's actions, read fresh for every menu and key.</summary>
    private IReadOnlyList<KitAction> ActionsOf(KitItem item)
        => TaskOf(item) is { } task ? TaskSpec.Actions(task, _feed?.Edits.IsUnconfirmedCreate(task.Id) != true) : [];

    /// <summary>Carry out a task's action: tick, rename in place, delete.</summary>
    private void Run(KitItem item, KitAction action)
    {
        if (!action.Runnable || TaskOf(item) is not { } task)
        {
            return;
        }

        switch (action.Id)
        {
            case TaskSpec.ToggleId:
                SetCompleted(task, !task.Completed);
                break;

            case TaskSpec.RenameId:
                _list.BeginRename(item);
                break;

            case TaskSpec.DeleteId:
                Delete(task);
                break;
        }
    }

    private void OnRenamed(KitItem item, string title)
    {
        if (TaskOf(item) is { } task)
        {
            Rename(task, title);
        }
    }

    /// <summary>
    /// [S-808] Whether an editor is being typed into: a row's or the add row's box, or the list
    /// picker's. This is only the text box's share of the shell's keyboard lease.
    /// </summary>
    private void UpdateKeyboard() => _surface?.RequestTextInput(!_detached && (_list.Editing || _renamingList is not null));

    /// <summary>
    /// [S-787] [S-808] A key from the shell's keyboard lease: the kit's list's one key map ([S-616]).
    /// An open editor owns its own keys.
    /// </summary>
    public bool HandleKey(Themia.Core.Widgets.WidgetKey key) => !_detached && _list.HandleKey(key);

    // ---- changes ---------------------------------------------------------------------------------------

    private void SetCompleted(TaskItem task, bool completed)
    {
        if (_service is null)
        {
            return;
        }

        if (IsLocal)
        {
            if (_localListId is { } listId)
            {
                _service.Local.Update(listId, tasks => LocalTaskLists.SetDone(tasks, task.Id, completed));
            }

            return;
        }

        if (_feed is { } feed)
        {
            _ = _service.SetCompletedAsync(feed, task, completed);
        }
    }

    private void Rename(TaskItem task, string title)
    {
        if (_service is null)
        {
            return;
        }

        if (IsLocal)
        {
            if (_localListId is { } listId)
            {
                _service.Local.Update(listId, tasks => LocalTaskLists.RenameTask(tasks, task.Id, title));
            }

            return;
        }

        if (_feed is { } feed)
        {
            _ = _service.RenameAsync(feed, task, title);
        }
    }

    private void Delete(TaskItem task)
    {
        if (_service is null)
        {
            return;
        }

        if (IsLocal)
        {
            if (_localListId is { } listId)
            {
                _service.Local.Update(listId, tasks => LocalTaskLists.RemoveTask(tasks, task.Id));
            }

            return;
        }

        if (_feed is { } feed)
        {
            _ = _service.DeleteAsync(feed, task);
        }
    }

    /// <summary>
    /// [S-598]/[S-786] Keep a new arrangement of the rows shown, from the menu or a drag. A list on
    /// this PC stores its order in the list; an account list's order lives in this widget's config,
    /// because no provider keeps one. An unchanged order writes nothing.
    /// </summary>
    private void CommitOrder(IReadOnlyList<string> ids)
    {
        var shown = _displayed.Select(t => t.Id).ToList();

        if (TaskRules.SameOrder(ids, shown) || _service is null)
        {
            return;
        }

        if (IsLocal)
        {
            if (_localListId is { } listId)
            {
                _service.Local.Update(listId, tasks => LocalTaskLists.Reorder(tasks, ids));
            }

            return;
        }

        // [S-611] Written over the whole list, so rows a filter hides keep their places.
        if (_feed is not { } feed)
        {
            return;
        }

        var all = Arranged(feed, DateOnly.FromDateTime(DateTime.Now)).Select(t => t.Id).ToList();
        _config.TaskOrder = [.. TaskRules.MergeOrder(all, ids)];
        _surface?.RequestSave();
        Render();
    }

    private async Task AddAsync(string title)
    {
        if (_service is null || title.Trim().Length == 0)
        {
            return;
        }

        if (IsLocal)
        {
            if (_localListId is { } listId)
            {
                _service.Local.Update(listId, tasks => LocalTaskLists.AddTask(tasks, title, $"local-{Guid.NewGuid():n}", _config.NewOnTop));
            }

            return;
        }

        if (_feed is not { } feed)
        {
            return;
        }

        var created = await _service.CreateAsync(feed, title).ConfigureAwait(true);

        if (created is null || _detached || !ReferenceEquals(feed, _feed))
        {
            return;
        }

        // [S-599] [S-610] Pin the arrangement with the new task last (or first). Without this the next
        // refresh re-imposes the provider's own sort — Graph lists most recently changed first —
        // which is how a task just added kept jumping to the top. Rebuilt from the whole list held,
        // so ids of deleted tasks prune themselves rather than accumulate, and rows a filter hides
        // keep their places.
        var all = Arranged(feed, DateOnly.FromDateTime(DateTime.Now)).Select(t => t.Id).ToList();
        _config.TaskOrder = [.. TaskRules.PlaceAdded(all, created.Id, _config.NewOnTop)];
        _surface?.RequestSave();
        Render();
    }

    private bool Writable => IsLocal ? _local?.Writable == true && _localListId is not null : _feed is { Loaded: true };

    /// <summary>
    /// [S-611] Whether the add row may show: a list to add to, and no filter narrowing it — a task
    /// typed into a filtered list would vanish the moment it was added.
    /// </summary>
    private bool CanAdd => Writable && !_config.Filter(today: false, Importance).Narrows;
}
