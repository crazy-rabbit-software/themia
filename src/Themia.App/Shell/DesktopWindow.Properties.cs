using Microsoft.UI.Xaml;
using Themia.Core.Diagnostics;
using Themia.Core.Model;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell;

/// <summary>
/// [S-2340]..[S-2343] The selection's entry on this display's edit bar and its page: selecting a widget
/// adds its entry and opens its flyout, once per selection (closed by the user it stays closed; a click
/// on the widget again opens it again).
/// </summary>
internal sealed partial class DesktopWindow
{
    /// <summary>[S-3701] What the selection on this display offers, as last read.</summary>
    private QuickSetOffer _selectionOffer = QuickSetOffer.None;

    /// <summary>[S-2343] The selection whose flyout has opened on its own once: the same selection does not open it again.</summary>
    private string? _selectionShown;

    /// <summary>[S-3719] The one selected widget's own lines as last built, and the selection they were built for.</summary>
    private WidgetMenuLines? _widgetLines;

    /// <summary>What the selection's page does: everything goes through the host, as one undo step each.</summary>
    private SelectionCommands SelectionCommands() => new(
        SetHeader: _host.SetSelectedHeader,
        SetBackground: _host.SetSelectedBackground,
        SetTitle: _host.SetTitle,
        SetStyle: _host.SetSelectedStyle,
        SetItemBackground: _host.SetSelectedItemBackground,
        SetItemTextColor: _host.SetSelectedItemTextColor,
        SetScale: _host.SetSelectedScale,
        SetAlign: _host.SetSelectedAlign,
        Cut: _host.CutSelected,
        Copy: _host.CopySelected,
        Delete: _host.DeleteSelected,
        WidgetLines: (id, ask, confirm) => _host.WidgetMenuLines(id, ask, confirm, link => RequestSetting(link)),
        Preview: PreviewSelected,
        SetBackgroundImage: _host.SetSelectedBackgroundImage);

    /// <summary>
    /// [S-2340] The selection's page as the selection is now: its widget's own lines built again on every
    /// change (their closures hold the config as it is now; an undo replaces it).
    /// </summary>
    private PageModel SelectionModel()
    {
        var commands = SelectionCommands();
        _widgetLines = null;
        if (_selectionOffer.TitleWidgetId is { } id && commands.WidgetLines is { } lines)
        {
            try
            {
                var built = lines(
                    id,
                    ask => AskOnFlyout(new PageEdit(ask.Placeholder, ask.Initial ?? string.Empty, ask.Done)
                    {
                        Refuse = ask.Refuse,
                        AllowEmpty = ask.AllowEmpty,
                        Accept = ask.Accept,
                        Title = ask.Title,
                    }),
                    confirm => _flyout?.Page.Confirm(new PageQuestion(confirm.Question, confirm.Accept, confirm.Done, confirm.Danger, confirm.Detail)));

                // [S-3746] A line still reading (an account's calendars) asks to be built again once its answer has come.
                if (built is not null)
                {
                    built.Outdated += () => _root.DispatcherQueue?.TryEnqueue(() =>
                    {
                        if (ReferenceEquals(_widgetLines, built))
                        {
                            SyncBarMenu();
                        }
                    });
                }

                _widgetLines = built;
            }
            catch (Exception ex)
            {
                Log.Error(Scope, "a widget's lines could not be built", ex);
            }
        }

        return SelectionRows.Of(_selectionOffer, commands, _widgetLines);
    }

    /// <summary>[S-3731] The selected cards here show a value not yet kept (a fill's opacity); null shows what they hold again.</summary>
    private void PreviewSelected(CardPreview? preview)
    {
        var selection = _host.Selection;
        foreach (var card in _cards.Where(c => selection.Contains(c.Model.Id)))
        {
            card.Preview(preview);
            card.ApplyContentScale(ContentScale(card.Model, preview?.Scale));
        }
    }

    /// <summary>
    /// [S-2343] The selection on this display as it is now: its entry on the bar; a new selection opens its
    /// flyout (once: closed, it stays closed for that selection); none closes it.
    /// </summary>
    private void SyncProperties()
    {
        _selectionOffer = EditMode ? _host.QuickSetFor(CurrentViewId) : QuickSetOffer.None;
        if (!_selectionOffer.Shows)
        {
            _selectionShown = null;
            if (_selectionOpen)
            {
                CloseFlyout(focusEntry: false);
            }

            ShowToolbar();
            return;
        }

        var key = string.Join(",", _host.Selection.Ids.Order(StringComparer.Ordinal));
        if (_selectionShown != key)
        {
            _selectionShown = key;
            if (_host.Placing is null && !_propertiesHeld)
            {
                OpenSelectionPage(focus: false);
            }
        }

        ShowToolbar();
    }

    /// <summary>[S-2343] A click on a widget already selected: its flyout opened again.</summary>
    private void ReopenSelectionPage()
    {
        if (_selectionOffer.Shows && !_selectionOpen)
        {
            OpenSelectionPage(focus: false);
        }
    }

    /// <summary>[S-2343] The selection's entry: its flyout opened, or closed again.</summary>
    private void ToggleSelectionPage()
    {
        if (_selectionOpen)
        {
            CloseFlyout(focusEntry: false);
        }
        else
        {
            OpenSelectionPage(focus: true);
        }
    }

    /// <summary>[S-3710] A drag or a resize is under way: nothing opens until it ends.</summary>
    private bool _propertiesHeld;

    private void HoldProperties(bool held) => _propertiesHeld = held;

    /// <summary>[S-2325] The flyout stays hung from its entry as the surface lays out again.</summary>
    private void PlaceProperties() => PlaceFlyout();
}
