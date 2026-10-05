using Themia.Core.Accounts;
using Themia.Core.Storage;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>
/// The account model and <c>accounts.json</c>: what is kept, what is repaired, what is dropped, and
/// that the file is written whole.
/// </summary>
public sealed class AccountModelTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 4, 12, 0, 0, TimeSpan.Zero);

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "themia-tests", Guid.NewGuid().ToString("n"));

    public AccountModelTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException)
        {
            // ignore: temp cleanup is best effort and must not fail a test run
        }
    }

    [Fact]
    public void S500_every_provider_has_a_distinct_wire_name_that_parses_back()
    {
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (var provider in Enum.GetValues<AccountProvider>())
        {
            Assert.True(names.Add(provider.WireName()));
            Assert.Equal(provider, AccountProviders.TryParse(provider.WireName()));
            Assert.Contains(provider, AccountProviders.All);
        }
    }

    [Theory]
    [InlineData("rss")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("exchange")]
    [InlineData("github")] // [S-1901] the GitHub and Claude accounts went with their widgets
    [InlineData("claude")]
    public void S500_an_unknown_provider_is_not_guessed(string? wire)
        => Assert.Null(AccountProviders.TryParse(wire));

    [Fact]
    public void S501_wire_names_match_the_classic_app()
    {
        Assert.Equal("microsoft", AccountProvider.Microsoft.WireName());
        Assert.Equal("google", AccountProvider.Google.WireName());
        Assert.Equal("imap", AccountProvider.Imap.WireName());
        Assert.Equal("caldav", AccountProvider.CalDav.WireName());
        Assert.Equal("carddav", AccountProvider.CardDav.WireName());
    }

    [Fact]
    public void S502_features_follow_from_the_provider()
    {
        Assert.True(AccountProvider.Microsoft.Features().HasFlag(AccountFeatures.Tasks));
        Assert.True(AccountProvider.Google.Features().HasFlag(AccountFeatures.Tasks));
        Assert.Equal(AccountFeatures.Mail, AccountProvider.Imap.Features());
        Assert.Equal(AccountFeatures.Calendar, AccountProvider.CalDav.Features());
        Assert.Equal(AccountFeatures.Contacts, AccountProvider.CardDav.Features());
        Assert.Equal(AccountAuth.OAuthRefresh, AccountProvider.Google.Auth());
        Assert.Equal(AccountAuth.Password, AccountProvider.CardDav.Auth());
    }

    [Fact]
    public void S504_a_valid_entry_becomes_a_whole_account()
    {
        var repairs = new List<string>();
        var account = AccountRules.Validate(new StoredAccount
        {
            Id = "imap:bob@mail.example",
            Provider = "imap",
            Address = "bob@mail.example",
            Host = " mail.example ",
            Port = 993,
            Username = "bob",
            Password = "dpapi:v1:AAAA",
            Nickname = "  ",
        }, Now, repairs);

        Assert.NotNull(account);
        Assert.Empty(repairs);
        Assert.Equal(AccountProvider.Imap, account.Provider);
        Assert.Equal("mail.example", account.Host);
        Assert.Null(account.Nickname);
        Assert.Equal("dpapi:v1:AAAA", account.SealedPassword);
        Assert.Equal(Now, account.AddedAt);
    }

    [Fact]
    public void S504_an_imap_account_without_a_host_is_dropped_loudly()
    {
        var repairs = new List<string>();
        var account = AccountRules.Validate(new StoredAccount
        {
            Id = "imap:bob@",
            Provider = "imap",
            Address = "bob@x",
            Username = "bob",
        }, Now, repairs);

        Assert.Null(account);
        Assert.Contains(repairs, r => r.Contains("server", StringComparison.Ordinal));
    }

    [Fact]
    public void S504_a_bad_imap_port_is_repaired_not_dropped()
    {
        var repairs = new List<string>();
        var account = AccountRules.Validate(new StoredAccount
        {
            Id = "imap:bob@h",
            Provider = "imap",
            Address = "bob@h",
            Host = "h",
            Username = "bob",
            Port = 0,
        }, Now, repairs);

        Assert.Equal(AccountRules.DefaultImapPort, account?.Port);
        Assert.Single(repairs);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not a url")]
    [InlineData("ftp://dav.example/")]
    public void S504_a_dav_account_without_a_usable_address_is_dropped(string? url)
    {
        var repairs = new List<string>();
        var account = AccountRules.Validate(new StoredAccount
        {
            Id = "caldav:a@b",
            Provider = "caldav",
            Address = "a",
            Username = "a",
            ServerUrl = url,
        }, Now, repairs);

        Assert.Null(account);
        Assert.Single(repairs);
    }

    [Fact]
    public void S504_an_oauth_account_without_a_refresh_token_is_kept_for_sign_in_again()
    {
        var repairs = new List<string>();
        var account = AccountRules.Validate(new StoredAccount { Id = "x", Provider = "microsoft", Address = "a@b.c" }, Now, repairs);

        Assert.NotNull(account);
        Assert.Null(account.SealedRefreshToken);
        Assert.Empty(repairs);
    }

    [Fact]
    public void S504_duplicates_null_entries_and_unknown_providers_are_dropped_and_reported()
    {
        var document = new AccountDocument
        {
            Accounts =
            [
                new StoredAccount { Id = "a", Provider = "microsoft", Address = "first@x" },
                null,
                new StoredAccount { Id = "a", Provider = "microsoft", Address = "second@x" },
                new StoredAccount { Id = "b", Provider = "exchange", Address = "b@x" },
                new StoredAccount { Provider = "google", Address = "c@x" },
            ],
        };

        var repairs = new List<string>();
        var accounts = AccountRules.Validate(document, Now, repairs);

        var only = Assert.Single(accounts);
        Assert.Equal("first@x", only.Address);
        Assert.Equal(4, repairs.Count);
    }

    [Fact]
    public void S504_account_tostring_never_prints_sealed_fields()
    {
        var account = new Account
        {
            Id = "x",
            Provider = AccountProvider.Microsoft,
            Address = "a@b",
            SealedRefreshToken = "dpapi:v1:SECRET",
        };

        Assert.DoesNotContain("SECRET", account.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void S504_label_prefers_nickname_then_name_then_address()
    {
        var account = new Account { Id = "x", Provider = AccountProvider.Google, Address = "a@b" };
        Assert.Equal("a@b", account.Label);
        Assert.Equal("Ann", (account with { DisplayName = "Ann" }).Label);
        Assert.Equal("Work", (account with { DisplayName = "Ann", Nickname = "Work" }).Label);
    }

    [Fact]
    public void S505_ids_match_the_classic_scheme()
    {
        Assert.Equal("google:117", AccountIds.Google("117"));
        Assert.Equal("imap:bob@mail.example", AccountIds.Imap("bob", "mail.example"));
        Assert.Equal("caldav:al@cloud.example:8443", AccountIds.Dav(AccountProvider.CalDav, "al", new Uri("https://cloud.example:8443/dav/")));
        Assert.Equal("carddav:al@cloud.example", AccountIds.Dav(AccountProvider.CardDav, "al", new Uri("https://cloud.example/dav/")));
    }

    [Fact]
    public void S506_envelope_round_trips_and_tells_plaintext_from_damage()
    {
        var sealedText = SecretEnvelope.Wrap([1, 2, 3]);

        Assert.StartsWith("dpapi:v1:", sealedText, StringComparison.Ordinal);
        Assert.Equal(EnvelopeKind.Sealed, SecretEnvelope.TryUnwrap(sealedText, out var blob));
        Assert.Equal(new byte[] { 1, 2, 3 }, blob);

        Assert.Equal(EnvelopeKind.Absent, SecretEnvelope.TryUnwrap(null, out _));
        Assert.Equal(EnvelopeKind.Absent, SecretEnvelope.TryUnwrap(string.Empty, out _));
        Assert.Equal(EnvelopeKind.Plaintext, SecretEnvelope.TryUnwrap("untagged-token", out _));
        Assert.Equal(EnvelopeKind.Damaged, SecretEnvelope.TryUnwrap("dpapi:v1:***", out _));
        Assert.Equal(EnvelopeKind.Damaged, SecretEnvelope.TryUnwrap("dpapi:v1:", out _));
    }

    [Fact]
    public void S507_a_token_is_stale_inside_the_skew()
    {
        Assert.True(TokenExpiry.IsUsable(Now.AddMinutes(10), Now));
        Assert.False(TokenExpiry.IsUsable(Now.AddMinutes(4), Now));
        Assert.False(TokenExpiry.IsUsable(Now.AddMinutes(-1), Now));
        Assert.False(TokenExpiry.IsUsable(null, Now));
    }

    [Theory]
    [InlineData(3600L, 3600)]
    [InlineData(null, 3600)]
    [InlineData(0L, 3600)]
    [InlineData(-5L, 3600)]
    [InlineData(99_999_999_999L, 30 * 86400)]
    public void S507_expires_in_is_defaulted_and_clamped(long? expiresIn, int expectedSeconds)
        => Assert.Equal(Now.AddSeconds(expectedSeconds), TokenExpiry.FromExpiresIn(expiresIn, Now));

    [Fact]
    public void S530_a_stale_or_missing_token_is_refreshed_a_fresh_one_is_not()
    {
        Assert.True(RefreshRules.ShouldRefresh(Now.AddMinutes(1), "sealed-a", null, Now));
        Assert.False(RefreshRules.ShouldRefresh(Now.AddHours(1), "sealed-a", null, Now));
        Assert.True(RefreshRules.ShouldRefresh(Now.AddHours(1), null, null, Now));
    }

    [Fact]
    public void S530_a_rejected_token_is_refreshed_once_however_many_callers_saw_the_401()
    {
        // The first caller through the gate finds the rejected token still stored and refreshes.
        Assert.True(RefreshRules.ShouldRefresh(Now.AddHours(1), "sealed-a", "sealed-a", Now));

        // Everyone queued behind it finds a different token stored and uses that one.
        Assert.False(RefreshRules.ShouldRefresh(Now.AddHours(1), "sealed-b", "sealed-a", Now));
    }

    [Theory]
    [InlineData(400, "invalid_grant", true)]
    [InlineData(400, "interaction_required", true)]
    [InlineData(401, null, true)]
    [InlineData(401, "invalid_client", true)]
    [InlineData(400, null, false)]
    [InlineData(400, "invalid_request", false)]
    [InlineData(500, "invalid_grant", false)]
    [InlineData(503, null, false)]
    public void S530_only_a_lost_grant_asks_the_user_to_sign_in_again(int status, string? code, bool expected)
        => Assert.Equal(expected, RefreshRules.NeedsUser(status, code));

    [Fact]
    public async Task S524_round_trip_keeps_every_field()
    {
        var store = new AccountStore(Path.Combine(_dir, "accounts.json"));
        var account = new Account
        {
            Id = "caldav:al@cloud.example",
            Provider = AccountProvider.CalDav,
            Address = "al",
            DisplayName = "Al",
            Nickname = "Home",
            Username = "al",
            ServerUrl = "https://cloud.example/dav/calendars/al/personal/",
            SealedPassword = "dpapi:v1:AQID",
            ExpiresAt = Now.AddHours(1),
            AddedAt = Now,
        };

        await store.SaveAsync([account]);
        var load = store.Load(Now);

        Assert.Equal(LoadOutcome.Loaded, load.Outcome);
        Assert.Equal(account, Assert.Single(load.Accounts));
        Assert.Empty(Directory.GetFiles(_dir, "*.tmp"));
    }

    [Fact]
    public void S524_no_file_is_fresh_not_an_error()
    {
        var load = new AccountStore(Path.Combine(_dir, "absent.json")).Load(Now);

        Assert.Equal(LoadOutcome.Fresh, load.Outcome);
        Assert.Empty(load.Accounts);
        Assert.True(load.Writable);
    }

    [Fact]
    public void S524_a_corrupt_file_is_set_aside_never_deleted()
    {
        var path = Path.Combine(_dir, "accounts.json");
        File.WriteAllText(path, "{ not json");

        var load = new AccountStore(path).Load(Now);

        Assert.Equal(LoadOutcome.Recovered, load.Outcome);
        Assert.True(load.Writable);
        Assert.False(File.Exists(path));
        var aside = Assert.Single(Directory.GetFiles(_dir, "accounts.json.corrupt-*"));
        Assert.Equal("{ not json", File.ReadAllText(aside));
    }

    [Fact]
    public void S524_a_newer_schema_is_set_aside_rather_than_half_read()
    {
        var path = Path.Combine(_dir, "accounts.json");
        File.WriteAllText(path, """{ "schemaVersion": 99, "accounts": [] }""");

        var load = new AccountStore(path).Load(Now);

        Assert.Equal(LoadOutcome.Recovered, load.Outcome);
        Assert.Single(Directory.GetFiles(_dir, "accounts.json.corrupt-*"));
    }

    [Fact]
    public void S524_a_locked_file_makes_the_store_read_only()
    {
        var path = Path.Combine(_dir, "accounts.json");
        File.WriteAllText(path, """{ "schemaVersion": 1, "accounts": [] }""");

        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var load = new AccountStore(path).Load(Now);
            Assert.False(load.Writable);
            Assert.Equal(LoadOutcome.Recovered, load.Outcome);
        }

        Assert.True(File.Exists(path));
    }

    [Fact]
    public void S524_accounts_json_lives_beside_the_state_file()
        => Assert.Equal(
            Path.Combine("C:\\data\\Themia", "accounts.json"),
            AccountStore.PathBeside(Path.Combine("C:\\data\\Themia", "state.json")));
}
