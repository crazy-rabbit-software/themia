using Themia.Core.Diagnostics;
using Themia.Core.Localization;

namespace Themia.App.Shell;

/// <summary>
/// [S-1110] The language switching live on the desktop: one central rebuild, so no widget has to
/// follow the language itself.
/// <para>
/// Every card on the surface is recreated once — its view, its header, its menus — and the toolbar
/// (with the Add-widget picker inside it), the view bar and the placement hint are redrawn. A
/// language change is rare and deliberate, so restarting each widget's tick is an acceptable price
/// for having one path that cannot miss a string, rather than a subscription in every view.
/// </para>
/// </summary>
internal sealed partial class DesktopWindow
{
    private void OnLanguageChanged(Locale locale)
    {
        if (_closed)
        {
            return;
        }

        // Queued, not inline: the switch is raised from inside a settings handler, and tearing the
        // cards down under it would run their Detach in the middle of someone else's click.
        if (!_window.DispatcherQueue.TryEnqueue(ApplyLanguage))
        {
            Log.Warn(Scope, $"could not queue the language redraw for {_monitor.GdiName} ({LanguageResolver.Code(locale)})");
        }
    }

    private void ApplyLanguage()
    {
        if (_closed)
        {
            return;
        }

        ApplyFlow();
        RebuildToolbar();

        // A suspended surface has no cards; it builds them fresh, in the new language, when it resumes.
        if (!_suspended)
        {
            foreach (var card in _cards)
            {
                _host.KeyboardRemoved(card);
                card.Detach();
                _widgetLayer.Children.Remove(card);
            }

            _cards.Clear();
        }

        // Rebuilds the cards (none survive the clear above), the view bar and the hit region.
        Relayout();
        OnPlacementChanged();
        Log.Info(Scope, $"redrew {_monitor.GdiName} in {LanguageResolver.Code(Strings.Current)}");
    }

    /// <summary>
    /// [S-1113] The language on the whole surface, its direction only on the parts that are text:
    /// the edit bar, the properties strip, the view bar, the placement hint, the notices and the menu anchor (each card
    /// sets its own as it is built). The surface root and its canvases stay left to right — they
    /// are where things are on the screen, and a widget does not move when the language changes.
    /// </summary>
    private void ApplyFlow()
    {
        UiLanguage.ApplyLanguageOnly(_root);
        UiLanguage.ApplyFlow(_menuAnchor);
        UiLanguage.ApplyFlow(_toolbar);
        UiLanguage.ApplyFlow(_properties);
        foreach (var menu in _barMenus.Values)
        {
            UiLanguage.ApplyFlow(menu);
        }

        UiLanguage.ApplyFlow(_viewBar);
        UiLanguage.ApplyFlow(_placementHint);
        UiLanguage.ApplyFlow(_noticeLayer);
    }

    /// <summary>[S-1113] A point on the surface as the menu anchor counts it (mirrored when the language runs right to left).</summary>
    private Windows.Foundation.Point OnMenuAnchor(Windows.Foundation.Point at)
        => _root.TransformToVisual(_menuAnchor).TransformPoint(at);

    /// <summary>The edit bar's buttons and its add menu, the properties strip and the bar's open menu, built again in the current language.</summary>
    private void RebuildToolbar()
    {
        if (_toolbar.Built)
        {
            _toolbar.Fill(ToolbarCommands());
        }

        _properties.Fill(PropertiesCommands());

        RebuildBarMenu();
    }
}
