using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Themia.Core.Localization;
using Themia.Sdk.Kit;
using Themia.Sdk.WinUI.Kit;

namespace Themia.App.Widgets;

/// <summary>
/// [S-1862] The body of a widget that cannot show anything until it is set up: the Status block's set-up
/// row ([S-6714]) where the content's first row would be — the widget's glyph, one sentence saying what
/// it waits for ("Choose a place"), and the accent Set up button that goes where it is set (its
/// properties menu, or ⚙ › Accounts: <c>DesktopWindow.RequestSetup</c>). On a row too narrow for both
/// (XS, S) the sentence goes and the glyph and the button stay.
/// <para>
/// The card puts it over the view (<c>WidgetCard.Setup</c>) when <see cref="Themia.Core.Widgets.WidgetSetup.Needed"/>
/// says so, for every widget alike, under the header (or at the top inset when the header is not above: the card's margin on it, [S-5700]).
/// </para>
/// </summary>
internal sealed partial class SetupPrompt : Grid
{
    private readonly StatusBlock _status = new();

    public SetupPrompt(string glyph, string sentence, Action open)
    {
        ArgumentNullException.ThrowIfNull(open);

        var label = Str.Shell_Setup_Button.Text();
        _status.Show(StatusKind.Setup, glyph, sentence, label, open);
        if (_status.ActionButton is { } button)
        {
            AutomationProperties.SetName(button, label);
            AutomationProperties.SetHelpText(button, sentence);
        }

        Children.Add(_status);
    }
}
