using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Tasks;
using Themia.Core.Widgets;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI;
using Themia.Sdk.WinUI.Kit;
using Windows.System;
using Themia.Widget.Tasks;

namespace Themia.App.Widgets;

/// <summary>
/// [S-614] The list picker in the header, as Notes has one ([S-1364]): the lists to switch to, and
/// Rename list… for the one shown. An account list is renamed at the provider through the account's
/// queue ([S-2610]); a list on this PC in the pool every widget shares ([S-603]).
/// </summary>
internal sealed partial class TasksView
{
    private TextBox? _renamingList;

    /// <summary>
    /// [S-612] The list picker: one of the kit header's buttons (the 16 chevron), shown while there are
    /// lists to pick from; the header's fold gives it way on a narrow tile, the menu still has the lists.
    /// </summary>
    private IReadOnlyList<KitHeaderButton> PickerButtons()
    {
        var lists = IsLocal ? _local?.Lists.Count > 0 : _feed?.Lists is { Count: > 0 };
        return lists
            ? [new KitHeaderButton(Glyphs.ChevronDown, TasksStr.WidgetsC_Tasks_ChooseList.Text()) { Menu = PickerEntries }]
            : [];
    }

    private TaskListInfo? ShownList()
        => _feed is { IsToday: false } feed ? feed.Lists?.FirstOrDefault(l => l.Id == feed.ListId) : null;

    private IEnumerable<ItemMenuEntry> PickerEntries()
    {
        var entries = new List<ItemMenuEntry>();
        if (_detached || _service is null)
        {
            return entries;
        }


        if (IsLocal)
        {
            foreach (var list in _local?.Lists ?? [])
            {
                var id = list.Id;
                entries.Add(new ItemMenuEntry(null, Display(list.Name), () => PickLocal(id), Checked: id == _localListId));
            }

            if (_local is { Writable: true } && _local.Find(_localListId) is { } shown)
            {
                entries.Add(ItemMenuEntry.Separator);
                entries.Add(new ItemMenuEntry(Glyphs.Rename, TasksStr.WidgetsC_Tasks_RenameListEllipsis.Text(), () => RenameInFlyout(shown.Name)));
            }
        }
        else if (_feed is { Lists: { } lists } feed)
        {
            entries.Add(new ItemMenuEntry(null, TasksStr.Todo_TodayList.Text(), () => PickAccountList(TaskRules.TodayListId, string.Empty), Checked: feed.IsToday));

            foreach (var list in lists)
            {
                var (id, name) = (list.Id, list.Name);
                entries.Add(new ItemMenuEntry(null, Display(name), () => PickAccountList(id, name), Checked: !feed.IsToday && id == feed.ListId));
            }

            if (!feed.ReadOnly && TaskRules.CanRename(ShownList()))
            {
                entries.Add(ItemMenuEntry.Separator);
                entries.Add(new ItemMenuEntry(Glyphs.Rename, TasksStr.WidgetsC_Tasks_RenameListEllipsis.Text(), () => RenameInFlyout(ShownList()?.Name ?? string.Empty)));
            }
        }

        return entries;
    }

    private static string Display(string name) => string.IsNullOrWhiteSpace(name) ? TasksStr.WidgetsC_Tasks_UntitledList.Text() : name;

    /// <summary>Show another account list here, as the settings' list box does: its own order starts empty.</summary>
    private void PickAccountList(string id, string name)
    {
        if (_detached || (id == _config.ListId && id.Length > 0))
        {
            return;
        }

        _config.ListId = id;
        _config.ListName = name;
        _config.TaskOrder = [];
        _surface?.RequestSave();
        Rebind();
    }

    private void PickLocal(string id)
    {
        if (_detached || id == _config.LocalListId)
        {
            return;
        }

        _config.LocalListId = id;
        _surface?.RequestSave();
        Rebind();
    }

    /// <summary>The rename box: room for a list's name of a few words.</summary>
    private const double RenameWidth = 320;

    /// <summary>Rename the shown list in a small box under the picker: Enter or leaving it commits, Escape does not.</summary>
    private void RenameInFlyout(string current)
    {
        var box = new TextBox { Text = current, MinWidth = RenameWidth, PlaceholderText = TasksStr.Todo_LocalLists_NamePlaceholder.Text() };
        // [S-1500] Its own popup: inside the surface's window the region clips it to the card.
        var flyout = new Flyout { Content = box, Placement = FlyoutPlacementMode.Bottom, ShouldConstrainToRootBounds = false };
        var cancelled = false;

        box.KeyDown += (_, e) =>
        {
            if (e.Key is VirtualKey.Enter or VirtualKey.Escape)
            {
                e.Handled = true;
                cancelled = e.Key == VirtualKey.Escape;
                flyout.Hide();
            }
        };

        flyout.Opened += (_, _) =>
        {
            _renamingList = box;
            UpdateKeyboard();
            box.Focus(FocusState.Programmatic);
            box.SelectAll();
        };

        flyout.Closed += (_, _) =>
        {
            _renamingList = null;
            UpdateKeyboard();

            if (!cancelled && !_detached)
            {
                RenameShownList(box.Text);
            }
        };

        flyout.ShowAt(_header.ButtonAt(0) ?? (FrameworkElement)this);
    }

    /// <summary>[S-614] A blank or unchanged name writes nothing; To Do's own lists and Today are never offered.</summary>
    private void RenameShownList(string typed)
    {
        if (_service is null)
        {
            return;
        }

        if (IsLocal)
        {
            if (_local?.Find(_localListId) is { } local && TaskRules.RenameTo(local.Name, typed) is { } name)
            {
                _service.Local.Rename(local.Id, name);
            }

            return;
        }

        if (_feed is { } feed && ShownList() is { } list && TaskRules.RenameTo(list.Name, typed) is { } clean)
        {
            if (list.Id == _config.ListId)
            {
                // The name the header shows while the lists load next time.
                _config.ListName = clean;
                _surface?.RequestSave();
            }

            _ = _service.RenameListAsync(feed.AccountId, list, clean);
        }
    }
}
