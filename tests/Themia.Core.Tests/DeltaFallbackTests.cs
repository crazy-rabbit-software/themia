using Themia.Core.Accounts;
using Themia.Core.Sync;
using Xunit;

namespace Themia.Core.Tests;

/// <summary>[S-2690] A refused change request degrades to the plain read; it never skips what it was for.</summary>
public sealed class DeltaFallbackTests
{
    /// <summary>What Graph answered the live <c>me/todo/lists/{id}/tasks/delta</c> request with (2026-10-11).</summary>
    private const string GraphInvalidRequest = """{"error":{"code":"invalidRequest","message":"Invalid request"}}""";

    /// <summary>The exception AccountService throws for that answer: the status, and the provider's words from the body.</summary>
    private static AccountException Answered(int status, string body) => new(AccountException.FromStatus(status), $"graph.microsoft.com answered HTTP {status}", status)
    {
        ProviderMessage = ErrorBody.Message(body),
    };

    [Fact]
    public void S2690_graphs_invalid_request_on_a_delta_is_unsupported_not_a_refused_cursor()
    {
        var ex = Answered(400, GraphInvalidRequest);

        Assert.Equal("Invalid request", ex.ProviderMessage);
        Assert.False(DeltaRules.Rejected(ex.HttpStatus, ex.ProviderMessage));
        Assert.True(DeltaRules.Unsupported(ex.HttpStatus));
    }

    [Theory]
    [InlineData(400, true)]
    [InlineData(405, true)]
    [InlineData(501, true)]
    [InlineData(401, false)]
    [InlineData(403, false)]
    [InlineData(404, false)]
    [InlineData(429, false)]
    [InlineData(500, false)]
    [InlineData(503, false)]
    [InlineData(0, false)]
    public void S2690_only_a_refusal_of_the_request_itself_is_unsupported(int status, bool unsupported)
        => Assert.Equal(unsupported, DeltaRules.Unsupported(status));

    [Fact]
    public async Task S2690_a_400_on_the_delta_falls_back_to_the_plain_read()
    {
        var fallback = new DeltaFallback("test");
        var plainReads = 0;

        var read = await fallback.ReadAsync("acc|list", "acc: list",
            () => Task.FromException<string[]>(Answered(400, GraphInvalidRequest)),
            () => { plainReads++; return Task.FromResult(new[] { "task 1", "task 2" }); });

        Assert.Equal(["task 1", "task 2"], read);
        Assert.Equal(1, plainReads);
        Assert.True(fallback.Refused("acc|list"));
    }

    [Fact]
    public async Task S2690_a_refused_delta_is_remembered_for_that_list_only()
    {
        var fallback = new DeltaFallback("test");
        var deltas = new List<string>();

        Task<string> Delta(string key)
        {
            deltas.Add(key);
            return key == "acc|flagged" ? Task.FromException<string>(Answered(400, GraphInvalidRequest)) : Task.FromResult("delta");
        }

        Assert.Equal("plain", await fallback.ReadAsync("acc|flagged", "f", () => Delta("acc|flagged"), () => Task.FromResult("plain")));
        Assert.Equal("plain", await fallback.ReadAsync("acc|flagged", "f", () => Delta("acc|flagged"), () => Task.FromResult("plain")));
        Assert.Equal("delta", await fallback.ReadAsync("acc|tasks", "t", () => Delta("acc|tasks"), () => Task.FromResult("plain")));

        // The refused list was asked by delta once; the next cycle went straight to the plain read.
        Assert.Equal(["acc|flagged", "acc|tasks"], deltas);
        Assert.False(fallback.Refused("acc|tasks"));
    }

    [Fact]
    public async Task S2690_a_400_on_the_plain_read_stays_an_error()
    {
        var fallback = new DeltaFallback("test");

        var ex = await Assert.ThrowsAsync<AccountException>(() => fallback.ReadAsync("acc|list", "l",
            () => Task.FromException<int>(Answered(400, GraphInvalidRequest)),
            () => Task.FromException<int>(Answered(400, GraphInvalidRequest))));

        Assert.Equal(400, ex.HttpStatus);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(429)]
    [InlineData(503)]
    public async Task S2690_any_other_failure_of_the_delta_is_not_masked_by_a_plain_read(int status)
    {
        var fallback = new DeltaFallback("test");
        var plainReads = 0;

        var ex = await Assert.ThrowsAsync<AccountException>(() => fallback.ReadAsync("acc|list", "l",
            () => Task.FromException<int>(Answered(status, "{}")),
            () => { plainReads++; return Task.FromResult(1); }));

        Assert.Equal(status, ex.HttpStatus);
        Assert.Equal(0, plainReads);
        Assert.False(fallback.Refused("acc|list"));
    }
}
