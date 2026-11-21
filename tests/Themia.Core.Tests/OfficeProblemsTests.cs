using Themia.Core.Localization;
using Themia.Core.Office;
using Themia.Core.Widgets;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-588] [S-9932] The calendar and contacts widgets' problems, in the one failure vocabulary.</summary>
public sealed class OfficeProblemsTests
{
    [Theory]
    [InlineData(FetchStatus.NotConfigured, true)]
    [InlineData(FetchStatus.SignInRequired, true)]
    [InlineData(FetchStatus.Forbidden, true)]
    [InlineData(FetchStatus.Offline, false)]
    [InlineData(FetchStatus.RateLimited, false)]
    [InlineData(FetchStatus.BadResponse, false)]
    public void S588_only_a_problem_the_user_can_mend_offers_the_accounts(FetchStatus status, bool needsUser)
    {
        Assert.Equal(needsUser, OfficeProblems.NeedsUser(status));
        Assert.True(OfficeProblems.IsProblem(status));
    }

    [Theory]
    [InlineData(FetchStatus.Ok)]
    [InlineData(FetchStatus.Empty)]
    [InlineData(FetchStatus.Loading)]
    public void S588_a_sync_that_worked_or_has_not_run_is_no_problem(FetchStatus status)
    {
        Assert.False(OfficeProblems.IsProblem(status));
        Assert.Equal(string.Empty, OfficeProblems.Message(status, SyncSubject.Calendar, Locale.En));
    }

    [Fact]
    public void S588_a_signed_out_account_and_a_refusal_say_their_own_sentences()
    {
        Assert.Equal(Strings.Get(Locale.En, Str.WidgetsA_Office_Problem_NoAccount), OfficeProblems.Message(FetchStatus.NotConfigured, SyncSubject.Calendar, Locale.En));
        Assert.Equal(Strings.Get(Locale.En, Str.WidgetsA_Office_Problem_ForbiddenContacts), OfficeProblems.Message(FetchStatus.Forbidden, SyncSubject.Contacts, Locale.En));
        Assert.Equal(Strings.Get(Locale.En, Str.WidgetsA_Office_Problem_BadResponse), OfficeProblems.Message(FetchStatus.NotFound, SyncSubject.Calendar, Locale.En));
    }
}
