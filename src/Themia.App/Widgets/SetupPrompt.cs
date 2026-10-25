using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Themia.App.Shell;
using Themia.Core.Localization;
using Themia.Core.Widgets;
using Windows.Foundation;

namespace Themia.App.Widgets;

/// <summary>
/// [S-1862] The body of a widget that cannot show anything until it is set up: the widget's glyph
/// over one accent "Set up" button that goes where what it waits for is set (its properties menu, or
/// ⚙ › Accounts: <c>DesktopWindow.RequestSetup</c>). No sentence — the button is the way
/// forward, and a paragraph about where to find an option is what it replaces.
/// <para>
/// The card puts it over the view (<c>WidgetCard.Setup</c>) when <see cref="WidgetSetup.Needed"/>
/// says so, for every widget alike. Laid out by hand like <see cref="EmptyStatePanel"/>: the glyph
/// is dropped on a box too short for both, the button always stays, so it is there at XS.
/// </para>
/// </summary>
internal sealed partial class SetupPrompt : Panel
{
    private readonly FontIcon _icon;
    private readonly Button _button;
    private bool _showIcon = true;

    public SetupPrompt(string glyph, Action open)
    {
        ArgumentNullException.ThrowIfNull(open);

        _icon = Fluent.Icon(glyph, "TextFillColorTertiaryBrush", Fluent.Space6);
        _icon.HorizontalAlignment = HorizontalAlignment.Center;
        _icon.Margin = new Thickness(0, 0, 0, Fluent.Space3);

        var label = Str.Shell_Setup_Button.Text();
        _button = new Button
        {
            Content = label,
            HorizontalAlignment = HorizontalAlignment.Center,

            // The surface is WS_EX_NOACTIVATE outside edit mode: a click must not try to focus it.
            AllowFocusOnInteraction = false,
        };
        FluentLayout.ApplyStyle(_button, "AccentButtonStyle");
        ToolTipService.SetToolTip(_button, Str.Shell_Setup_Hint.Text());
        AutomationProperties.SetName(_button, label);
        AutomationProperties.SetHelpText(_button, Str.Shell_Setup_Hint.Text());
        _button.Click += (_, _) => open();

        Children.Add(_icon);
        Children.Add(_button);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = Math.Max(0, availableSize.Width - (2 * Fluent.Space3));
        _icon.Measure(new Size(width, double.PositiveInfinity));
        _button.Measure(new Size(width, double.PositiveInfinity));

        var iconBlock = _icon.DesiredSize.Height;
        _showIcon = EmptyStateFit.ShowsIcon(availableSize.Height, iconBlock, _button.DesiredSize.Height);

        return new Size(
            double.IsFinite(availableSize.Width) ? availableSize.Width : Math.Max(_icon.DesiredSize.Width, _button.DesiredSize.Width),
            double.IsFinite(availableSize.Height) ? availableSize.Height : (_showIcon ? iconBlock : 0) + _button.DesiredSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var iconHeight = _showIcon ? _icon.DesiredSize.Height : 0;
        var buttonHeight = _button.DesiredSize.Height;
        var top = Math.Max(0, (finalSize.Height - iconHeight - buttonHeight) / 2);

        // A glyph with no room is arranged into nothing rather than collapsed, as in EmptyStatePanel.
        _icon.Arrange(_showIcon ? new Rect(0, top, finalSize.Width, iconHeight) : new Rect(0, top, 0, 0));

        var buttonWidth = Math.Min(_button.DesiredSize.Width, finalSize.Width);
        _button.Arrange(new Rect((finalSize.Width - buttonWidth) / 2, top + iconHeight, buttonWidth, buttonHeight));
        return finalSize;
    }
}
