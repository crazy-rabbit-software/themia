using Microsoft.UI.Xaml;
using Themia.App.Widgets;
using Themia.Core.Diagnostics;
using Themia.Core.Model;
using Themia.Core.Shell;
using Themia.Sdk.WinUI;

namespace Themia.App.Shell;

/// <summary>
/// [S-2340]..[S-2343] [S-11754]..[S-11757] The selection's entry on this display's edit bar and its page:
/// selecting a widget (or a group) adds its entry and opens its flyout, once per selection (closed by the
/// user it stays closed; a click on the widget again opens it again).
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

    /// <summary>
    /// What the selection's page does for <paramref name="ids"/> (the selection, or one member of a group on
    /// its own page): everything goes through the host, as one change each. <paramref name="whole"/>: the
    /// selection itself, with its Cut, Copy, Remove and its group's commands.
    /// </summary>
    private SelectionCommands SelectionCommands(IReadOnlyList<string> ids, bool whole) => new(
        SetHeader: header => _host.SetHeader(ids, header),
        SetBackground: fill => _host.SetBackground(ids, fill),
        SetTitle: _host.SetTitle,
        SetStyle: style => _host.SetStyle(ids, style),
        SetItemBackground: fill => _host.SetItemBackground(ids, fill),
        SetItemTextColor: hex => _host.SetItemTextColor(ids, hex),
        SetScale: scale => _host.SetScale(ids, scale),
        SetAlign: align => _host.SetAlign(ids, align),
        Cut: whole ? _host.CutSelected : null,
        Copy: whole ? _host.CopySelected : null,
        Delete: whole ? _host.DeleteSelected : null,
        WidgetLines: WidgetLinesOf,
        Preview: preview => PreviewWidgets(ids, preview),
        SetBackgroundImage: path => _host.SetBackgroundImage(ids, path))
    {
        SharedLines = SharedLinesOf,
        RenameGroup = whole ? _host.RenameGroup : null,
        Ungroup = whole ? _host.UngroupSelected : null,
        RemoveFromGroup = whole ? _host.RemoveFromGroup : null,
        OpenGroup = whole ? _host.SelectGroupOfEntered : null,
        Member = whole ? MemberPage : null,
    };

    /// <summary>[S-11755] A member's own page: its model as a lone selection of it, acting on it alone.</summary>
    private (SelectionModel Model, SelectionCommands Commands)? MemberPage(string id)
        => _host.WidgetModel(id) is { } model ? (model, SelectionCommands([id], whole: false)) : null;

    /// <summary>[S-3719] One widget's own lines over its live config, asking on the flyout.</summary>
    private WidgetMenuLines? WidgetLinesOf(string id)
    {
        try
        {
            return Kept(_host.WidgetMenuLines(id, AskOnPage, ConfirmOnPage, link => RequestSetting(link)));
        }
        catch (Exception ex)
        {
            Log.Error(Scope, "a widget's lines could not be built", ex);
            return null;
        }
    }

    /// <summary>[S-11751] A shared setting's lines, built over a scratch copy of one holder and set on every holder.</summary>
    private IReadOnlyList<ItemMenuEntry> SharedLinesOf(SharedRow row)
    {
        if (ViewForThisMonitor(_host.State) is not { } view)
        {
            return [];
        }

        var holders = view.Widgets.Where(w => row.Holders.Contains(w.Id, StringComparer.Ordinal)).ToList();
        try
        {
            return SharedRowLines.Of(
                row,
                holders,
                (kind, config, save, ask, confirm) => Services.ModuleHost.MenuLines(kind, config, save, ask, confirm, link => RequestSetting(link)),
                AskOnPage,
                ConfirmOnPage,
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

    private void AskOnPage(WidgetTextAsk ask) => AskOnFlyout(new PageEdit(ask.Placeholder, ask.Initial ?? string.Empty, ask.Done)
    {
        Refuse = ask.Refuse,
        AllowEmpty = ask.AllowEmpty,
        Accept = ask.Accept,
        Title = ask.Title,
    });

    private void ConfirmOnPage(WidgetConfirm confirm)
        => _flyout?.Page.Confirm(new PageQuestion(confirm.Question, confirm.Accept, confirm.Done, confirm.Danger, confirm.Detail));

    /// <summary>
    /// [S-2340] The selection's page as the selection is now: every line built again on every change
    /// (their closures hold the config as it is now; an undo replaces it).
    /// </summary>
    private PageModel SelectionModel()
    {
        _pageLines.Clear();
        var model = _selectionModel ?? new SelectionModel(SelectionKind.One, QuickSetOffer.None, [], []);
        var selection = _host.Selection;
        var kinds = ViewForThisMonitor(_host.State).Widgets.Where(w => selection.Contains(w.Id)).Select(w => w.Kind).Distinct().ToList();
        return SelectionRows.Of(model, SelectionCommands([.. selection.Ids], whole: true), kinds);
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

            ShowToolbar();
            return;
        }

        var key = string.Join(",", _host.Selection.Ids.Order(StringComparer.Ordinal)) + "|" + _host.Selection.Entered;
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
