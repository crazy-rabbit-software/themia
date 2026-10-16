using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Themia.Core.Diagnostics;
using Themia.Core.Localization;

namespace Themia.App.Shell.Config;

/// <summary>[S-1110] Following a language switch while the settings window is open.</summary>
internal sealed partial class ConfigWindow
{
    /// <summary>
    /// Redraw in the new language: the navigation tree, the open page and the search index are all
    /// built from strings, so all three are rebuilt (nothing else changed, so nothing else is).
    /// <para>
    /// Queued rather than done inline because the usual sender is the language box on the Language
    /// &amp; region page: replacing that page inside the box's own SelectionChanged would tear the
    /// control down mid-event. The queue runs it straight after, in order — no timing involved.
    /// </para>
    /// </summary>
    private void OnLanguageChanged(Locale locale)
    {
        if (_closed)
        {
            return;
        }

        var queued = _window.DispatcherQueue.TryEnqueue(() =>
        {
            if (_closed)
            {
                return;
            }

            ApplyChromeText();
            ApplyCaptionInset();
            RebuildNavigation();
            _index.Invalidate();
            RenderBody();
        });

        if (!queued)
        {
            Log.Warn(Scope, $"could not queue the redraw for language {LanguageResolver.Code(locale)}");
        }
    }

    /// <summary>
    /// The window's own words — its title, the caption buttons' names, the search box — set when the
    /// chrome is built and again on a language switch (the chrome itself is never rebuilt).
    /// </summary>
    private void ApplyChromeText()
    {
        if (_window is not null)
        {
            _window.Title = Str.Settings_Title.Text();
            UiLanguage.Apply(_window.Content as Microsoft.UI.Xaml.FrameworkElement);
        }

        ToolTipService.SetToolTip(_back, Str.Common_Back.Text());
        AutomationProperties.SetName(_back, Str.Common_Back.Text());
        ToolTipService.SetToolTip(_paneToggle, Str.Shell_Settings_Navigation.Text());
        AutomationProperties.SetName(_paneToggle, Str.Shell_Settings_OpenNavigation.Text());
        _search.PlaceholderText = Str.Shell_Settings_FindSetting.Text();
        AutomationProperties.SetName(_search, Str.Shell_Settings_FindSetting.Text());
    }
}
