using Themia.Core.Mail;
using Themia.Core.Widgets;

namespace Themia.App.Widgets;

/// <summary>
/// [S-1288] Keyboard navigation: a click focuses a row; Up/Down, Page Up/Down, Home/End move; Enter
/// opens; Delete deletes where the account allows; the Menu key opens the message's menu; Escape
/// lets go. [S-1560] The keys come through the shell's keyboard lease (<see cref="IWidgetKeys"/>). The focus follows its message
/// across redraws, and moves to the row that took its place when the message left the list.
/// </summary>
internal sealed partial class EmailView
{
    /// <summary>The focused message, by key, so a redraw (new mail, a resize) keeps it.</summary>
    private string? _focusKey;

    /// <summary>Where the focused row was, for when its message has left the list.</summary>
    private int _focusIndex = -1;

    /// <summary>
    /// [S-1560] A key from the shell's keyboard lease: what <see cref="ListKeys"/> decides, carried
    /// out here. Escape — also sent whenever the lease ends — drops the row focus.
    /// </summary>
    public bool HandleKey(WidgetKey key)
    {
        if (_detached)
        {
            return false;
        }

        var decision = ListKeys.Decide(key, _focusKey is null ? -1 : _focusIndex, _shown.Count);

        switch (decision.Command)
        {
            case ListCommand.Focus:
                FocusRow(decision.Index);
                return true;

            case ListCommand.Open:
                OpenMessage(_shown[decision.Index]);
                return true;

            case ListCommand.Delete when CanChange(_shown[decision.Index]):
                Change(_shown[decision.Index], MailChange.Delete);
                return true;

            case ListCommand.Menu when decision.Index < _rows.Count:
                ShowMessageMenu(_rows[decision.Index], _shown[decision.Index], null);
                return true;

            case ListCommand.Release:
                var had = _focusKey is not null;
                ReleaseFocus();
                return had;

            default:
                return false;
        }
    }

    private void FocusRow(int index)
    {
        if (index < 0 || index >= _rows.Count || index >= _shown.Count)
        {
            return;
        }

        if (_focusKey is not null && _focusIndex >= 0 && _focusIndex < _rows.Count)
        {
            MarkRow(_rows[_focusIndex], false);
        }

        _focusIndex = index;
        _focusKey = _shown[index].Key;
        MarkRow(_rows[index], true);
    }

    private void ReleaseFocus()
    {
        if (_focusKey is not null && _focusIndex >= 0 && _focusIndex < _rows.Count)
        {
            MarkRow(_rows[_focusIndex], false);
        }

        _focusKey = null;
        _focusIndex = -1;
    }

    /// <summary>After a redraw: the focus back on its message, or on the row now where it was.</summary>
    private void RestoreFocus()
    {
        if (_focusKey is null)
        {
            return;
        }

        var index = -1;
        for (var i = 0; i < _shown.Count; i++)
        {
            if (_shown[i].Key == _focusKey)
            {
                index = i;
                break;
            }
        }

        index = index >= 0 ? index : ListKeys.AfterRemoval(_focusIndex, _shown.Count);
        if (index < 0)
        {
            _focusKey = null;
            _focusIndex = -1;
            return;
        }

        FocusRow(index);
    }
}
