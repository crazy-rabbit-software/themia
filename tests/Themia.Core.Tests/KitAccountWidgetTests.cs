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
}
