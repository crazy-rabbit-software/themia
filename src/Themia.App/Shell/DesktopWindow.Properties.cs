using Microsoft.UI.Xaml;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Model;
using Themia.Core.Settings;
using Themia.Core.Shell;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell;

/// <summary>
/// [S-2340]..[S-2343] [S-11754] [S-11755] The selection's entry on this display's edit bar and its page:
/// selecting widgets adds their entry and opens its flyout, once per selection (closed by the
/// user it stays closed; a click on the widget again opens it again). While the pointer holds a widget (a
/// press, a drag, a resize) nothing opens; the page opens when the pointer lets go.
/// </summary>
internal sealed partial class DesktopWindow
{
    /// <summary>[S-3701] [S-11754] What the selection on this display is and offers, as last read; null for nothing.</summary>
    private SelectionModel? _selectionModel;

    /// <summary>[S-3701] What the selection on this display offers, as last read.</summary>
    private QuickSetOffer _selectionOffer = QuickSetOffer.None;

    /// <summary>[S-2343] The selection whose flyout has opened on its own once: the same selection does not open it again.</summary>
    private string? _selectionShown;

    /// <summary>[S-3719] [S-3746] The lines built for the page as it was last drawn: one that is still reading asks for the page again.</summary>
    private readonly List<WidgetMenuLines> _pageLines = [];

    /// <summary>What the selection's page does for <paramref name="ids"/>, the selection: everything goes through the host, as one change each.</summary>
    private SelectionCommands SelectionCommands(IReadOnlyList<string> ids) => new(
        SetBackground: fill => _host.SetBackground(ids, fill),
        SetStyle: style => _host.SetStyle(ids, style),
        SetItemBackground: fill => _host.SetItemBackground(ids, fill),
        SetItemTextColor: hex => _host.SetItemTextColor(ids, hex),
        SetScale: scale => _host.SetScale(ids, scale),
        SetAlign: align => _host.SetAlign(ids, align),
        Cut: _host.CutSelected,
        Copy: _host.CopySelected,
        Delete: _host.DeleteSelected,
        WidgetLines: WidgetLinesOf,
        Preview: preview => PreviewWidgets(ids, preview),
        SetBackgroundImage: path => _host.SetBackgroundImage(ids, path))
    {
        SharedLines = SharedLinesOf,
    };

    /// <summary>[S-3719] One widget's own lines over its live config; [S-2351] what they ask they ask in place on the page.</summary>
    private WidgetMenuLines? WidgetLinesOf(string id)
    {
        try
        {
            return Kept(_host.WidgetMenuLines(id, link => RequestSetting(link)));
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "a widget's lines could not be built", ex);
            return null;
        }
    }

    /// <summary>[S-11751] A shared setting's lines, built over a scratch copy of one holder and set on every holder.</summary>
    private IReadOnlyList<SettingsLine> SharedLinesOf(SharedRow row)
    {
        if (ViewForThisMonitor(_host.State) is not { } view)
        {
            return [];
        }

        var holders = view.Widgets.Where(w => row.Holders.Contains(w.Id, StringComparer.Ordinal)).ToList();
        try
        {
            return SharedRowLines.Of(
                row.Setting,
                row.Mixed,
                [.. holders.Select(h => (h.Kind, h.Config))],
                (kind, config, save) => Services.ModuleHost.MenuLines(kind, config, save, link => RequestSetting(link)),
                scratch => _host.SetShared(row.Setting, row.Holders, scratch),
                built => Kept(built));
        }
        catch (Exception ex)
        {
            Log.Error(Scope, $"the shared {row.Setting} lines could not be built", ex);
            return [];
        }
    }

    /// <summary>[S-3746] Lines built for the page: a line still reading (an account's calendars) asks to be built again once its answer has come.</summary>
    private WidgetMenuLines? Kept(WidgetMenuLines? built)
    {
        if (built is null)
        {
            return null;
        }

        _pageLines.Add(built);
        built.Outdated += () => _root.DispatcherQueue?.TryEnqueue(() =>
        {
            if (_pageLines.Contains(built))
            {
                SyncBarMenu();
            }
        });
        return built;
    }

    /// <summary>
    /// [S-2340] The selection's page as the selection is now: every line built again on every change
    /// (their closures hold the config as it is now; an undo replaces it).
    /// </summary>
    private PageModel SelectionModel()
    {
        _pageLines.Clear();
        var model = _selectionModel ?? new SelectionModel(SelectionKind.One, QuickSetOffer.None, []);
        var selection = _host.Selection;
        var kinds = ViewForThisMonitor(_host.State).Widgets.Where(w => selection.Contains(w.Id)).Select(w => w.Kind).Distinct().ToList();
        return SelectionRows.Of(model, SelectionCommands([.. selection.Ids]), kinds);
    }

    /// <summary>[S-3731] The widgets' cards here show a value not yet kept (a fill's opacity); null shows what they hold again.</summary>
    private void PreviewWidgets(IReadOnlyList<string> ids, CardPreview? preview)
    {
        foreach (var card in _cards.Where(c => ids.Contains(c.Model.Id, StringComparer.Ordinal)))
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
        _selectionModel = EditMode ? _host.SelectionModelFor(CurrentViewId) : null;
        _selectionOffer = _selectionModel?.Offer ?? QuickSetOffer.None;
        if (!_selectionOffer.Shows)
        {
            _selectionShown = null;
            if (_selectionOpen)
            {
                CloseFlyout(focusEntry: false);
            }

            ShowEditBar();
            return;
        }

        var key = string.Join(",", _host.Selection.Ids.Order(StringComparer.Ordinal));
        (_selectionShown, var opens) = SelectionPage.Flyout(_selectionShown, key, _host.Placing is not null, _propertiesHeld);
        if (opens)
        {
            OpenSelectionPage(focus: false);
        }

        ShowEditBar();
        ShineSelection();
    }

    /// <summary>[S-2343] A widget selected again by a command (not the pointer): its flyout opened again.</summary>
    private void ReopenSelectionPage()
    {
        if (SelectionPage.OpensWhenReleased(_selectionOffer.Shows, _selectionOpen, _host.Placing is not null) && !_propertiesHeld)
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

    /// <summary>[S-2343] [S-3710] The pointer holds a widget (a press, a move, a resize): nothing opens until it lets go.</summary>
    private bool _propertiesHeld;

    private void HoldProperties(bool held) => _propertiesHeld = held;

    /// <summary>
    /// [S-2343] The pointer let go of a widget (a click, the end of a move or of a resize): in edit mode
    /// the selection's page opens, for a new selection and for one whose page was closed alike.
    /// </summary>
    public void PressEnded(WidgetCard card)
    {
        HoldProperties(false);
        SyncProperties();
        if (SelectionPage.OpensWhenReleased(_selectionOffer.Shows, _selectionOpen, _host.Placing is not null))
        {
            OpenSelectionPage(focus: false);
        }

        PlaceFlyout();
    }

    /// <summary>[S-2343] The widget under the pointer starts to move or to change its size: the selection's page, if open, goes until the pointer lets go.</summary>
    private void ClearForDrag()
    {
        HoldProperties(true);
        if (_selectionOpen)
        {
            CloseFlyout(focusEntry: false);
        }
    }
}
