using Themia.App.Widgets;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Sdk.WinUI;
using Xunit;

namespace Themia.Widget.NextMeeting.Tests;

/// <summary>[S-4826] Next meeting's options on its properties menu ([S-3719]).</summary>
public sealed class NextMeetingMenuTests
{
    [Fact]
    public void S4826_the_menu_is_appearance_then_the_calendar_accounts_and_no_birthdays()
    {
        var lines = new WidgetMenuLines(() => { }, _ => { });
        NextMeetingMenu.Lines(lines, new NextMeetingConfig(), office: null);

        Assert.Equal(0, lines.CardAt);
        Assert.Equal([Str.WidgetUi_AccountsLabel.Text()], lines.Lines.Select(l => l.Text));
        Assert.Empty(lines.StyleLines);
    }
}
