using System.Runtime.CompilerServices;
using Themia.Core.Accounts;
using Themia.Core.Storage;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// [S-503] An <c>accounts.json</c> of the shape users have: one account of every provider with every
/// field the app writes for it (Fixtures/accounts-every-provider.json, generated with the build from
/// before provider ids, made-up addresses and envelopes, not anyone's real file). Providers became
/// module ids ([S-6910]); this pins that such a file still loads whole, with no repair, and writes
/// back byte for byte, so a real file keeps working without a migration.
/// </summary>
public sealed class AccountsFixtureTests
{
    private static string Fixture([CallerFilePath] string here = "")
        => Path.Combine(Path.GetDirectoryName(here)!, "Fixtures", "accounts-every-provider.json");

    private static readonly DateTimeOffset Now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void S503_an_accounts_file_with_every_provider_loads_whole_without_a_repair()
    {
        var load = new AccountStore(Fixture()).Load(Now);

        Assert.Equal(LoadOutcome.Loaded, load.Outcome);
        Assert.Null(load.Detail);
        Assert.Equal(
            ["microsoft", "google", "imap", "caldav", "carddav"],
            load.Accounts.Select(a => StoredAccount.From(a).Provider));
        Assert.Equal(993, load.Accounts[2].Port);
        Assert.Equal("https://dav.example.com/cal/user/", load.Accounts[3].ServerUrl);
    }

    [Fact]
    public async Task S503_an_accounts_file_with_every_provider_writes_back_byte_for_byte()
    {
        var load = new AccountStore(Fixture()).Load(Now);
        var copy = Path.Combine(Path.GetTempPath(), $"themia-accounts-{Guid.NewGuid():N}.json");
        try
        {
            await new AccountStore(copy).SaveAsync(load.Accounts);
            Assert.Equal(File.ReadAllText(Fixture()).ReplaceLineEndings("\n"), File.ReadAllText(copy).ReplaceLineEndings("\n"));
        }
        finally
        {
            File.Delete(copy);
        }
    }
}
