using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Tasks;
using Themia.Core.Widgets;
using Windows.System;

namespace Themia.App.Widgets;

/// <summary>
/// [S-614] The list picker in the header, as Notes has one ([S-1364]): the lists to switch to, and
/// Rename list… for the one shown. An account list is renamed at the provider through the account's
/// queue ([S-2610]); a list on this PC in the pool every widget shares ([S-603]).
/// </summary>
internal sealed partial class TasksView
{
    private Button _picker = null!;

    /// <summary>The open list-rename box (the header's flyout), or null.</summary>
    private TextBox? _renamingList;

    private void BuildPicker()
    {
        _picker = new Button
        {
            Content = Fluent.Icon(Glyphs.ChevronDown, "TextFillColorSecondaryBrush", 12),
            Padding = new Thickness(Fluent.Space1),
            MinWidth = 0,
            MinHeight = 0,
            BorderThickness = new Thickness(0),
            Background = FluentLayout.Themed("SubtleFillColorTransparentBrush", Colors.Transparent),
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = Visibility.Collapsed,
        };

        ApplyPickerTexts();
        _picker.Click += OnPickerClick;
    }

    private void ApplyPickerTexts()
    {
        ToolTipService.SetToolTip(_picker, Str.WidgetsC_Tasks_ChooseList.Text());
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(_picker, Str.WidgetsC_Tasks_ChooseList.Text());
    }

    /// <summary>
    /// The picker shows wherever there are lists to pick from — never on the narrowest tile, where it
    /// would push the list's name out of the header.
    /// </summary>
    private void UpdatePicker()
    {
        var lists = IsLocal ? _local?.Lists.Count > 0 : _feed?.Lists is { Count: > 0 };
        _picker.Visibility = lists && _area.WidthClass > SizeClass.Compact ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>The account list this widget shows (the default list for an unchosen one), or null for Today and a list on this PC.</summary>
    private TaskListInfo? ShownList()
        => _feed is { IsToday: false } feed ? feed.Lists?.FirstOrDefault(l => l.Id == feed.ListId) : null;

    private void OnPickerClick(object sender, RoutedEventArgs args)
    {
        if (_detached || _service is null)
        {
            return;
        }

        var entries = new List<ItemMenuEntry>();

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
                entries.Add(new ItemMenuEntry(Glyphs.Rename, Str.WidgetsC_Tasks_RenameListEllipsis.Text(), () => RenameInFlyout(shown.Name)));
            }
        }
        else if (_feed is { Lists: { } lists } feed)
        {
            entries.Add(new ItemMenuEntry(null, Str.Todo_TodayList.Text(), () => PickAccountList(TaskRules.TodayListId, string.Empty), Checked: feed.IsToday));

            foreach (var list in lists)
            {
                var (id, name) = (list.Id, list.Name);
                entries.Add(new ItemMenuEntry(null, Display(name), () => PickAccountList(id, name), Checked: !feed.IsToday && id == feed.ListId));
            }

            if (TaskRules.CanRename(ShownList()))
            {
                entries.Add(ItemMenuEntry.Separator);
                entries.Add(new ItemMenuEntry(Glyphs.Rename, Str.WidgetsC_Tasks_RenameListEllipsis.Text(), () => RenameInFlyout(ShownList()?.Name ?? string.Empty)));
            }
        }

        ItemMenu.Open(_picker, entries);
    }

    private static string Display(string name) => string.IsNullOrWhiteSpace(name) ? Str.WidgetsC_Tasks_UntitledList.Text() : name;

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

    /// <summary>Rename the shown list in a small box under the picker: Enter or leaving it commits, Escape does not.</summary>
    private void RenameInFlyout(string current)
    {
        var box = new TextBox { Text = current, MinWidth = 200, PlaceholderText = Str.Todo_LocalLists_NamePlaceholder.Text() };
        var flyout = new Flyout { Content = box, Placement = FlyoutPlacementMode.Bottom };
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

        flyout.ShowAt(_picker);
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

    private void DetachPicker() => _picker.Click -= OnPickerClick;
}
