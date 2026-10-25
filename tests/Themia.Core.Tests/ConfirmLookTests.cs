using Themia.Core.Shell;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-3727] How an "are you sure?" draws its buttons: a destructive one as Windows' destructive confirm.</summary>
public sealed class ConfirmLookTests
{
    [Fact]
    public void S3727_a_dangerous_question_is_critical_never_the_accent_and_starts_on_cancel()
    {
        var look = ConfirmLook.For(danger: true);

        Assert.True(look.Critical);
        Assert.False(look.Accent);
        Assert.True(look.CancelFirst);
    }

    [Fact]
    public void S3727_any_other_question_offers_its_action_as_the_accent()
    {
        var look = ConfirmLook.For(danger: false);

        Assert.True(look.Accent);
        Assert.False(look.Critical);
        Assert.False(look.CancelFirst);
    }
}
