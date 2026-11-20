using Themia.Core.Accounts;
using Themia.Core.Sync;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-2681] [S-2682] [S-2689] "The changes since the cursor, else everything", the one rule the mail folders, the calendar
/// windows, the address books and the task lists share (it was written four times).
/// </summary>
public sealed class DeltaSyncTests
{
    private int _changes;
    private int _full;

    private Task<DeltaChanges<string>> Changes(DeltaChanges<string> round)
    {
        _changes++;
        return Task.FromResult(round);
    }

    private Task<string> Full()
    {
        _full++;
        return Task.FromResult("full");
    }

    private Task<string> Read(CursorCheck check, Func<Task<DeltaChanges<string>>> changes)
        => DeltaSync.ChangesOrFullAsync(check, changes, Full, "test", "acct: list");

    [Fact]
    public async Task S2689_a_usable_cursor_reads_its_changes_alone()
    {
        Assert.Equal("changes", await Read(CursorCheck.Usable, () => Changes("changes")));
        Assert.Equal((1, 0), (_changes, _full));
    }

    [Theory]
    [InlineData(CursorCheck.Missing)]
    [InlineData(CursorCheck.Stale)]
    [InlineData(CursorCheck.OtherScope)]
    public async Task S2682_no_cursor_a_stale_or_a_foreign_one_reads_in_full_without_asking_for_changes(CursorCheck check)
    {
        Assert.Equal("full", await Read(check, () => Changes("changes")));
        Assert.Equal((0, 1), (_changes, _full));
    }

    [Fact]
    public async Task S2689_changes_that_leave_the_list_short_read_it_in_full()
    {
        Assert.Equal("full", await Read(CursorCheck.Usable, () => Changes(new DeltaChanges<string>("short", Short: true))));
        Assert.Equal((1, 1), (_changes, _full));
    }

    [Theory]
    [InlineData(410, null)]
    [InlineData(400, "Sync token is expired")]
    public async Task S2681_a_cursor_the_provider_refuses_reads_in_full_at_once(int status, string? said)
    {
        var refused = new AccountException(AccountError.BadResponse, "gone", status) { ProviderMessage = said };

        Assert.Equal("full", await Read(CursorCheck.Usable, () => throw refused));
        Assert.Equal(1, _full);
    }

    [Fact]
    public async Task S2681_a_failure_that_is_not_the_cursors_is_the_callers()
    {
        var expired = new AccountException(AccountError.SignInRequired, "sign in", 401);

        var thrown = await Assert.ThrowsAsync<AccountException>(() => Read(CursorCheck.Usable, () => throw expired));
        Assert.Same(expired, thrown);
        Assert.Equal(0, _full);
    }
}
