using Themia.Core.Accounts;
using Themia.Core.Localization;
using Themia.Core.Model;
using Themia.Core.Widgets;
using Themia.Sdk;
using Themia.Sdk.Kit;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-7064] [S-7040] [S-7050] What the account widgets need of the kit's header, screens and config.</summary>
public sealed class KitAccountWidgetTests
{
    [Fact]
    public void S7064_view_buttons_show_the_header_and_each_needs_its_width()
    {
        Assert.Equal(new KitHeaderParts(false, false, false, Buttons: true), KitHeaderParts.Of(0, busy: false, count: null, buttons: 2));
        Assert.True(KitHeaderParts.Of(0, busy: false, count: null, buttons: 1).Any);
        Assert.False(KitHeaderParts.Of(0, busy: false, count: null, buttons: 0).Any);

        Assert.True(KitHeaderParts.Fits(240, 240));
        Assert.False(KitHeaderParts.Fits(240, 239));
        Assert.False(KitHeaderParts.Fits(200, 0)); // unmeasured: none until the card has a width
    }

    [Fact]
    public void S7040_a_list_with_an_account_to_sign_in_again_offers_the_accounts_page()
    {
        KitAccount[] accounts = [new("work", "Work"), new("home", "Home", KitAccountState.SignInRequired)];
        KitOutcome[] outcomes = [new("Work", FetchStatus.Ok, HeldAt: DateTimeOffset.Now), new("Home", FetchStatus.SignInRequired)];

        var screen = KitScreens.Decide(Locale.En, accounts, [], outcomes, itemCount: 3, "Empty");

        Assert.Equal(KitScreenKind.Items, screen.Kind);
        Assert.True(screen.OffersAccounts);
        Assert.False(KitScreens.Decide(Locale.En, [new("work", "Work")], [], [outcomes[0]], 3, "Empty").OffersAccounts);
    }

    [Fact]
    public void S7050_a_kit_configs_choices_are_its_fields_by_default()
        => Assert.Equal(new KitChoices(KitShow.When), ((IKitConfig)new Fields()).Choices);

    private sealed class Fields : IKitConfig
    {
        public FeatureId? Needs => null;

        public bool Configured => true;

        public IReadOnlyList<string> AccountIds => [];

        public KitShow Shows => KitShow.When;
    }

    /// <summary>[S-7020] Alt+Shift+Up / Down move the focused item a place, not past the ends; with nothing focused they do nothing.</summary>
    [Fact]
    public void S7020_move_up_and_down_move_the_focused_item_within_the_list()
    {
        Assert.Equal(new KitKeyResult(KitKeyCommand.MoveUp, 2), KitKeys.Decide(WidgetKey.MoveUp, 2, 5));
        Assert.Equal(new KitKeyResult(KitKeyCommand.MoveDown, 2), KitKeys.Decide(WidgetKey.MoveDown, 2, 5));
        Assert.Equal(KitKeyCommand.None, KitKeys.Decide(WidgetKey.MoveUp, 0, 5).Command);
        Assert.Equal(KitKeyCommand.None, KitKeys.Decide(WidgetKey.MoveDown, 4, 5).Command);
        Assert.Equal(KitKeyCommand.None, KitKeys.Decide(WidgetKey.MoveDown, -1, 5).Command);
    }

    /// <summary>[S-7011] An item with nothing to open opens for editing: its Rename is the primary action (double-click, Enter).</summary>
    [Fact]
    public void S7011_an_item_without_an_open_has_its_rename_as_the_primary_action()
    {
        KitAction rename = new(KitActionKind.Rename, "Rename", Id: "rename");
        KitAction open = new(KitActionKind.Open, "Open", Id: "open");

        Assert.Same(rename, KitMenu.Primary([new(KitActionKind.Toggle, "Done", Id: "t"), rename]));
        Assert.Same(open, KitMenu.Primary([rename, open]));
        Assert.Null(KitMenu.Primary([new(KitActionKind.Open, "Open"), rename]));
    }

    /// <summary>[S-7011] A toggle whose label says what it does (Mark as done, Mark as not done) is a plain line, not a check.</summary>
    [Fact]
    public void S7011_a_toggle_without_a_state_is_a_plain_line()
        => Assert.Null(new KitAction(KitActionKind.Toggle, "Mark as done").Checked);
}
